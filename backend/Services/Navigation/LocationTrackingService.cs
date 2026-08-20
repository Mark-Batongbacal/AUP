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
        var legStart = matcher.ProjectProgress(geometry, leg.StartLatitude ?? geometry[0].Latitude, leg.StartLongitude ?? geometry[0].Longitude);
        var legEnd = matcher.ProjectProgress(geometry, leg.EndLatitude ?? geometry[^1].Latitude, leg.EndLongitude ?? geometry[^1].Longitude);
        if (legEnd < legStart) (legStart, legEnd) = (legEnd, legStart);
        var match = matcher.Match(update, geometry, legStart, legEnd, session.CurrentRouteProgressMeters);
        var fullEnd = matcher.ProjectProgress(geometry, geometry[^1].Latitude, geometry[^1].Longitude);
        var expectedMatch = matcher.Match(update, geometry, legStart, fullEnd, session.CurrentRouteProgressMeters);
        if (expectedMatch is null) return new(false, "LOCATION_NOT_MATCHED");
        if (session.CurrentNavigationState is TripNavigationState.OnJeepney or TripNavigationState.ApproachingAlightPoint &&
            expectedMatch.DistanceFromGeometryMeters <= _options.TransitOffRouteMeters + update.AccuracyMeters &&
            expectedMatch.DistanceFromRouteStartMeters > legEnd + _options.MissedAlightDistanceMeters)
        {
            session.LastLatitude = update.Latitude;
            session.LastLongitude = update.Longitude;
            session.LastAccuracyMeters = update.AccuracyMeters;
            session.LastLocationAt = update.Timestamp;
            session.LastRerouteReason = "MISSED_ALIGHT";
            session.LastNavigationStatus = "MISSED_ALIGHT";
            if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.OffRoute))
                return new(false, "INVALID_STATE_TRANSITION");
            session.CurrentNavigationState = TripNavigationState.OffRoute;
            session.LastNavigationStatus = "OFF_ROUTE";
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);
            _telemetry.Event("MissedAlightDetected", sessionId);
            return new(true, "MISSED_ALIGHT", DistanceFromGeometryMeters: expectedMatch.DistanceFromGeometryMeters);
        }
        var offRoute = offRouteDetector.Evaluate(
            session, leg, expectedMatch.DistanceFromGeometryMeters, update.AccuracyMeters, update.Timestamp);
        if (offRoute == OffRouteStatus.Confirmed)
        {
            session.LastLatitude = update.Latitude;
            session.LastLongitude = update.Longitude;
            session.LastAccuracyMeters = update.AccuracyMeters;
            session.LastLocationAt = update.Timestamp;
            if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.OffRoute))
                return new(false, "INVALID_STATE_TRANSITION");
            session.CurrentNavigationState = TripNavigationState.OffRoute;
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
        session.LastLatitude = update.Latitude;
        session.LastLongitude = update.Longitude;
        session.LastAccuracyMeters = update.AccuracyMeters;
        session.LastLocationAt = update.Timestamp;
        session.CurrentProgressMeters = match.DistanceFromLegStartMeters;
        session.CurrentRouteProgressMeters = match.DistanceFromRouteStartMeters;
        session.UpdatedAt = DateTime.UtcNow;
        session.LastNavigationStatus = "ON_ROUTE";

        var remaining = Math.Max(0, legEnd - match.DistanceFromRouteStartMeters);
        var candidate = session.CurrentNavigationState is TripNavigationState.OnJeepney or TripNavigationState.OnTricycle &&
            remaining <= _options.PrepareToAlightDistanceMeters;
        var finalWalking = session.CurrentLegIndex == legs.Max(item => item.LegOrder) &&
            IsWalking(leg) && remaining <= _options.ArrivalDistanceMeters;
        var walkingToBoard = session.CurrentNavigationState is
                TripNavigationState.WalkingToPickup or TripNavigationState.Transferring &&
            IsWalking(leg) && remaining <= _options.ArrivalDistanceMeters;
        var waitingToBoard = session.CurrentNavigationState == TripNavigationState.ApproachingBoardPoint &&
            IsWalking(leg) && remaining <= _options.ArrivalDistanceMeters;
        session.ConsecutiveStateConfirmationSamples = candidate || finalWalking || walkingToBoard || waitingToBoard
            ? session.ConsecutiveStateConfirmationSamples + 1 : 0;
        if (session.ConsecutiveStateConfirmationSamples >= _options.StateConfirmationSamples)
        {
            if (candidate && stateMachine.CanTransition(
                    session.CurrentNavigationState, TripNavigationState.ApproachingAlightPoint))
                session.CurrentNavigationState = TripNavigationState.ApproachingAlightPoint;
            if (candidate) _telemetry.Event("PrepareToAlightTriggered", sessionId);
            if (walkingToBoard && stateMachine.CanTransition(
                    session.CurrentNavigationState, TripNavigationState.ApproachingBoardPoint))
                session.CurrentNavigationState = TripNavigationState.ApproachingBoardPoint;
            if (waitingToBoard && stateMachine.CanTransition(
                    session.CurrentNavigationState, TripNavigationState.WaitingToBoard))
            {
                session.CurrentNavigationState = TripNavigationState.WaitingToBoard;
                session.CurrentLegIndex++;
                session.CurrentProgressMeters = 0;
                session.CurrentRouteProgressMeters = null;
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

    private static bool IsWalking(RecommendationLeg leg) =>
        leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";
}
