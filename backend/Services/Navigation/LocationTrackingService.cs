using backend.Models.Database;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Options;
using backend.Services.Telemetry;

namespace backend.Services.Navigation;

public interface ILocationTrackingService
{
    Task<LocationUpdateResult> ProcessAsync(Guid userId, Guid sessionId, LocationUpdate update, CancellationToken cancellationToken = default);
}

public sealed class LocationTrackingService(
    ITripSessionRepository sessions,
    IRouteRecommendationRepository recommendations,
    IRoutePointRepository routePoints,
    IValhallaService valhalla,
    IGpsQualityValidator gpsValidator,
    IMapMatchingService matcher,
    ILandmarkService landmarkService,
    IOffRouteDetector offRouteDetector,
    backend.Services.TripSessions.ITripSessionStateMachine stateMachine,
    IOptions<NavigationOptions> options,
    ITukiTelemetry? telemetry = null) : ILocationTrackingService
{
    private readonly NavigationOptions _options = options.Value;
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;

    public async Task<LocationUpdateResult> ProcessAsync(Guid userId, Guid sessionId,
        LocationUpdate update, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return new(false, "TRIP_SESSION_NOT_FOUND");
        if (session.CurrentNavigationState is TripNavigationState.Planned or TripNavigationState.Arrived or TripNavigationState.Cancelled)
            return new(false, "TRIP_NOT_ACTIVE");
        var qualityError = gpsValidator.Validate(update, session, DateTime.UtcNow);
        if (qualityError is not null) return new(false, qualityError);

        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var leg = legs.FirstOrDefault(item => item.LegOrder == session.CurrentLegIndex);
        if (leg is null) return new(false, "CURRENT_LEG_NOT_FOUND");
        var geometry = await GeometryAsync(leg, cancellationToken);
        if (geometry.Count < 2) return new(false, "LEG_GEOMETRY_UNAVAILABLE");
        var legStart = leg.StartRouteProgressMeters ?? matcher.ProjectProgress(
            geometry,
            leg.StartLatitude ?? geometry[0].Latitude,
            leg.StartLongitude ?? geometry[0].Longitude);
        var legEnd = leg.EndRouteProgressMeters ?? matcher.ProjectProgress(
            geometry,
            leg.EndLatitude ?? geometry[^1].Latitude,
            leg.EndLongitude ?? geometry[^1].Longitude);
        if (legEnd < legStart) (legStart, legEnd) = (legEnd, legStart);
        var match = matcher.Match(update, geometry, legStart, legEnd, session.CurrentRouteProgressMeters);
        var fullEnd = matcher.ProjectProgress(geometry, geometry[^1].Latitude, geometry[^1].Longitude);
        var expectedMatch = matcher.Match(update, geometry, legStart, fullEnd, session.CurrentRouteProgressMeters);

        // Routine trip progress is intentionally kept local on the client. That means the server's
        // previous route progress can be several kilometres behind when the client finally sends a
        // meaningful leg-end sync. First try reacquiring inside the final approach of the current
        // authoritative transit leg without the stale previous-progress window.
        if (expectedMatch is null && IsTransitNavigationState(session) && NavigationTripRules.IsTransit(leg))
        {
            var reacquireMinimum = Math.Max(legStart,
                legEnd - _options.PrepareToAlightDistanceMeters);
            var reacquired = matcher.MatchWithinRange(
                update,
                geometry,
                legStart,
                reacquireMinimum,
                legEnd);
            var remainingAfterReacquire = reacquired is null
                ? double.PositiveInfinity
                : Math.Max(0, legEnd - reacquired.DistanceFromRouteStartMeters);
            var insideTransitCorridor = reacquired is not null &&
                reacquired.DistanceFromGeometryMeters <= _options.TransitOffRouteMeters + update.AccuracyMeters;

            if (insideTransitCorridor && remainingAfterReacquire <= _options.ConfirmAlightDistanceMeters)
            {
                match = reacquired;
                expectedMatch = reacquired;
                _telemetry.Event("LegEndProgressReacquired", sessionId);
            }

            // Some long/looping stored route geometries can still disagree with the persisted leg
            // progress enough for the range projection above to return null. The alight endpoint is
            // a stronger final authority for this one state transition: if a quality-validated fix
            // is physically inside the confirmed alight zone, accept it as the end of THIS current
            // transit leg. This only opens the Alight Now confirmation; it never auto-alights.
            if (expectedMatch is null)
            {
                var endpoint = ResolveLegEndCoordinate(leg, geometry, legEnd);
                var endpointDistance = Geo.DistanceMeters(
                    update.Latitude,
                    update.Longitude,
                    endpoint.Latitude,
                    endpoint.Longitude);
                var endpointTolerance = _options.ConfirmAlightDistanceMeters +
                    Math.Min(Math.Max(0, update.AccuracyMeters), 25d);

                if (endpointDistance <= endpointTolerance)
                {
                    var endpointMatch = new RouteMatch(
                        endpoint.Latitude,
                        endpoint.Longitude,
                        endpointDistance,
                        Math.Max(0, legEnd - legStart),
                        legEnd,
                        Math.Max(0, geometry.Count - 2),
                        1d);
                    match = endpointMatch;
                    expectedMatch = endpointMatch;
                    _telemetry.Event("AlightEndpointReacquired", sessionId);
                }
            }
        }

        if (expectedMatch is null)
        {
            if (!IsUnconfirmedAlightCandidate(session, leg))
                return new(false, "LOCATION_NOT_MATCHED");

            var unmatchedStatus = offRouteDetector.Evaluate(
                session, leg, double.PositiveInfinity, update.AccuracyMeters, update.Timestamp);
            if (unmatchedStatus == OffRouteStatus.Confirmed)
                return await MarkAlightStatusUnknownAsync(session, update, cancellationToken);

            session.LastNavigationStatus = unmatchedStatus == OffRouteStatus.UncertainGps
                ? "UNCERTAIN_GPS" : "OFF_ROUTE_SUSPECTED";
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);
            return new(false, session.LastNavigationStatus);
        }
        if (session.CurrentNavigationState is TripNavigationState.OnJeepney or TripNavigationState.OnTricycle or TripNavigationState.ApproachingAlightPoint &&
            expectedMatch.DistanceFromGeometryMeters <= _options.TransitOffRouteMeters + update.AccuracyMeters &&
            expectedMatch.DistanceFromRouteStartMeters > legEnd + _options.MissedAlightDistanceMeters)
        {
            SaveLocation(session, update);
            session.CurrentProgressMeters = Math.Max(0,
                expectedMatch.DistanceFromRouteStartMeters - legStart);
            session.CurrentRouteProgressMeters = expectedMatch.DistanceFromRouteStartMeters;
            session.LastRerouteReason = "MISSED_ALIGHT";
            session.LastNavigationStatus = "MISSED_ALIGHT";
            session.ConsecutiveOffRouteSamples = 0;
            session.OffRouteSuspectedAt = null;
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);
            _telemetry.Event("MissedAlightDetected", sessionId);
            return new(true, "MISSED_ALIGHT",
                session.CurrentProgressMeters,
                expectedMatch.DistanceFromRouteStartMeters,
                expectedMatch.DistanceFromGeometryMeters);
        }
        var offRoute = offRouteDetector.Evaluate(
            session, leg, expectedMatch.DistanceFromGeometryMeters, update.AccuracyMeters, update.Timestamp);
        if (offRoute == OffRouteStatus.Confirmed)
        {
            if (IsUnconfirmedAlightCandidate(session, leg))
                return await MarkAlightStatusUnknownAsync(session, update, cancellationToken,
                    expectedMatch.DistanceFromGeometryMeters);

            SaveLocation(session, update);
            if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.OffRoute))
                return new(false, "INVALID_STATE_TRANSITION");
            session.CurrentNavigationState = TripNavigationState.OffRoute;
            session.LastNavigationStatus = "OFF_ROUTE";
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);
            _telemetry.Event("OffRouteConfirmed", sessionId);
            return new(true, "OFF_ROUTE", DistanceFromGeometryMeters: expectedMatch.DistanceFromGeometryMeters);
        }
        if (match is null)
        {
            session.LastNavigationStatus = offRoute == OffRouteStatus.UncertainGps
                ? "UNCERTAIN_GPS" : "OFF_ROUTE_SUSPECTED";
            await sessions.UpdateAsync(session, cancellationToken);
            if (offRoute == OffRouteStatus.Suspected)
                _telemetry.Event("OffRouteSuspected", sessionId);
            return new(false, offRoute == OffRouteStatus.UncertainGps ? "UNCERTAIN_GPS" : "OFF_ROUTE_SUSPECTED",
                DistanceFromGeometryMeters: expectedMatch.DistanceFromGeometryMeters);
        }

        var previousProgress = session.CurrentRouteProgressMeters ?? match.DistanceFromRouteStartMeters;
        SaveLocation(session, update);
        session.CurrentProgressMeters = match.DistanceFromLegStartMeters;
        session.CurrentRouteProgressMeters = match.DistanceFromRouteStartMeters;
        session.UpdatedAt = DateTime.UtcNow;
        session.LastNavigationStatus = "ON_ROUTE";

        var remaining = Math.Max(0, legEnd - match.DistanceFromRouteStartMeters);
        var preparingToAlight = session.CurrentNavigationState is TripNavigationState.OnJeepney or TripNavigationState.OnTricycle &&
            remaining <= _options.PrepareToAlightDistanceMeters;
        var finalWalking = session.CurrentLegIndex == legs.Max(item => item.LegOrder) &&
            IsWalking(leg) && remaining <= _options.ArrivalDistanceMeters;
        var approachingBoard = session.CurrentNavigationState is
                TripNavigationState.WalkingToPickup or TripNavigationState.Transferring &&
            IsWalking(leg) && remaining <= _options.PrepareToBoardDistanceMeters;
        var atBoardPoint = session.CurrentNavigationState is
                TripNavigationState.WalkingToPickup or TripNavigationState.Transferring or TripNavigationState.ApproachingBoardPoint &&
            IsWalking(leg) && remaining <= _options.ConfirmBoardDistanceMeters;
        session.ConsecutiveStateConfirmationSamples = preparingToAlight || finalWalking || approachingBoard || atBoardPoint
            ? session.ConsecutiveStateConfirmationSamples + 1 : 0;
        if (session.ConsecutiveStateConfirmationSamples >= _options.StateConfirmationSamples)
        {
            if (preparingToAlight && stateMachine.CanTransition(
                    session.CurrentNavigationState, TripNavigationState.ApproachingAlightPoint))
            {
                session.CurrentNavigationState = TripNavigationState.ApproachingAlightPoint;
                _telemetry.Event("PrepareToAlightTriggered", sessionId);
            }
            if (atBoardPoint)
            {
                if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.WaitingToBoard))
                    return new(false, "INVALID_STATE_TRANSITION");
                session.CurrentNavigationState = TripNavigationState.WaitingToBoard;
                session.CurrentLegIndex++;
                session.CurrentProgressMeters = 0;
                session.CurrentRouteProgressMeters = null;
                _telemetry.Event("BoardPointReached", sessionId);
            }
            else if (approachingBoard && stateMachine.CanTransition(
                         session.CurrentNavigationState, TripNavigationState.ApproachingBoardPoint))
            {
                session.CurrentNavigationState = TripNavigationState.ApproachingBoardPoint;
            }
            if (finalWalking)
            {
                if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.Arrived))
                    return new(false, "INVALID_STATE_TRANSITION");
                session.CurrentNavigationState = TripNavigationState.Arrived;
                session.CompletedAt = DateTime.UtcNow;
                session.LastNavigationStatus = "ARRIVED";
                _telemetry.Event("TripArrived", sessionId);
            }
            session.ConsecutiveStateConfirmationSamples = 0;
        }
        var landmarkInstructions = await landmarkService.EvaluateAsync(
            session, leg, previousProgress, match.DistanceFromRouteStartMeters, cancellationToken);
        foreach (var _ in landmarkInstructions) _telemetry.Event("LandmarkTriggered", sessionId);
        await sessions.UpdateAsync(session, cancellationToken);
        return new(true, session.CurrentNavigationState.ToString(), match.DistanceFromLegStartMeters,
            match.DistanceFromRouteStartMeters, match.DistanceFromGeometryMeters, landmarkInstructions);
    }

    private async Task<List<(double Latitude, double Longitude)>> GeometryAsync(
        RecommendationLeg leg, CancellationToken cancellationToken)
    {
        if (leg.RouteId is { } routeId)
        {
            var stored = (await routePoints.GetOrderedByRouteAsync(routeId, cancellationToken))
                .Select(point => (point.Latitude, point.Longitude)).ToList();
            if (stored.Count >= 2) return stored;
        }

        if (leg.StartLatitude is not { } startLat || leg.StartLongitude is not { } startLon ||
            leg.EndLatitude is not { } endLat || leg.EndLongitude is not { } endLon)
            return [];

        var mode = leg.TransportMode?.Code?.ToUpperInvariant();
        var costing = mode is "TRICYCLE" or "TRIKE" ? _options.TricycleRoadCosting : "pedestrian";
        try
        {
            var route = await valhalla.GetRouteAsync(
                startLat, startLon, endLat, endLon, costing, cancellationToken);
            var points = route.Trip?.Legs
                .SelectMany(item => item.Points)
                .Where(point => point.Length >= 2)
                .Select(point => (Latitude: point[1], Longitude: point[0]))
                .ToList() ?? [];
            return points.Count >= 2 ? points : [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }
    }

    private static (double Latitude, double Longitude) ResolveLegEndCoordinate(
        RecommendationLeg leg,
        IReadOnlyList<(double Latitude, double Longitude)> geometry,
        double legEndRouteProgressMeters)
    {
        if (leg.EndLatitude is { } endLat && leg.EndLongitude is { } endLon)
            return (endLat, endLon);
        if (geometry.Count == 0) return default;
        if (geometry.Count == 1 || legEndRouteProgressMeters <= 0) return geometry[0];

        var accumulated = 0d;
        for (var index = 0; index < geometry.Count - 1; index++)
        {
            var from = geometry[index];
            var to = geometry[index + 1];
            var segmentLength = Geo.DistanceMeters(
                from.Latitude, from.Longitude, to.Latitude, to.Longitude);
            if (segmentLength <= 0) continue;
            if (accumulated + segmentLength >= legEndRouteProgressMeters)
            {
                var fraction = Math.Clamp(
                    (legEndRouteProgressMeters - accumulated) / segmentLength,
                    0d,
                    1d);
                return (
                    from.Latitude + (to.Latitude - from.Latitude) * fraction,
                    from.Longitude + (to.Longitude - from.Longitude) * fraction);
            }
            accumulated += segmentLength;
        }

        return geometry[^1];
    }

    private static bool IsWalking(RecommendationLeg leg) =>
        leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";

    private static bool IsTransitNavigationState(TripSession session) =>
        session.CurrentNavigationState is TripNavigationState.OnJeepney or
            TripNavigationState.OnTricycle or TripNavigationState.ApproachingAlightPoint;

    private static bool IsUnconfirmedAlightCandidate(
        TripSession session,
        RecommendationLeg leg) =>
        session.CurrentNavigationState == TripNavigationState.ApproachingAlightPoint &&
        NavigationTripRules.IsTransit(leg);

    private async Task<LocationUpdateResult> MarkAlightStatusUnknownAsync(
        TripSession session,
        LocationUpdate update,
        CancellationToken cancellationToken,
        double? distanceFromGeometryMeters = null)
    {
        SaveLocation(session, update);
        session.LastNavigationStatus = "ALIGHT_STATUS_UNKNOWN";
        session.ConsecutiveOffRouteSamples = 0;
        session.OffRouteSuspectedAt = null;
        session.UpdatedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(session, cancellationToken);
        _telemetry.Event("AlightStatusUnknown", session.TripSessionId);
        return new(true, "ALIGHT_STATUS_UNKNOWN",
            DistanceFromGeometryMeters: distanceFromGeometryMeters);
    }

    private static void SaveLocation(TripSession session, LocationUpdate update)
    {
        session.LastLatitude = update.Latitude;
        session.LastLongitude = update.Longitude;
        session.LastAccuracyMeters = update.AccuracyMeters;
        session.LastLocationAt = update.Timestamp;
    }
}
