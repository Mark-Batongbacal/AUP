using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Options;
using backend.Services.Telemetry;

namespace backend.Services.Navigation;

public sealed record RerouteResult(bool Succeeded, string Status, Guid? RecommendationId = null);
public interface IReroutingService
{
    Task<RerouteResult> RerouteAsync(
        Guid userId,
        Guid sessionId,
        NavigationRerouteRequest request,
        CancellationToken cancellationToken = default);

    Task<RerouteResult> ApplyRecommendationAsync(
        Guid userId,
        Guid sessionId,
        Guid recommendationId,
        CancellationToken cancellationToken = default);
}

public sealed class ReroutingService(
    ITripSessionRepository sessions, IRoutingService routing,
    ITripSearchRepository searches, IRouteRecommendationRepository recommendations,
    IRecommendationLegRepository legs, ITransportModeRepository modes,
    ITransportRouteRepository routes, INavigationInstructionService instructions,
    ILandmarkCorridorPrefetchService landmarkPrefetch,
    backend.Services.TripSessions.ITripSessionStateMachine stateMachine,
    IGpsQualityValidator gpsValidator,
    IOptions<NavigationOptions> options,
    IOptions<RoutingOptions>? routingOptions = null,
    ITukiTelemetry? telemetry = null) : IReroutingService
{
    private static readonly TimeSpan AssistantProposalLifetime = TimeSpan.FromMinutes(10);
    private const double AssistantProposalMaxOriginDriftMeters = 1_500;

    private readonly NavigationOptions _options = options.Value;
    private readonly RoutingOptions _routingOptions = routingOptions?.Value ?? new RoutingOptions();
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;

    public async Task<RerouteResult> RerouteAsync(Guid userId, Guid sessionId,
        NavigationRerouteRequest request, CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("Rerouting");
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return new(false, "TRIP_SESSION_NOT_FOUND");
        if (session.CurrentNavigationState is TripNavigationState.Planned or TripNavigationState.Arrived or TripNavigationState.Cancelled)
            return new(false, "TRIP_NOT_ACTIVE");
        if (session.CurrentNavigationState == TripNavigationState.Rerouting)
            return new(false, "REROUTE_IN_PROGRESS");

        var normalizedReason = string.IsNullOrWhiteSpace(request.Reason)
            ? "MANUAL"
            : request.Reason.Trim().ToUpperInvariant();
        var automaticRecovery = normalizedReason is "OFF_ROUTE" or "MISSED_ALIGHT" or "MISSED_LEG_TARGET";
        if (automaticRecovery && session.LastRerouteAt is { } last &&
            last.AddSeconds(_options.RerouteCooldownSeconds) > DateTime.UtcNow)
            return new(false, "REROUTE_COOLDOWN");
        var hasAnyCurrentLocationField = request.Latitude is not null ||
            request.Longitude is not null || request.AccuracyMeters is not null ||
            request.Timestamp is not null || request.SpeedMetersPerSecond is not null ||
            request.BearingDegrees is not null;
        var hasCompleteCurrentLocation = request.Latitude is not null &&
            request.Longitude is not null && request.AccuracyMeters is not null &&
            request.Timestamp is not null;
        if (hasAnyCurrentLocationField && !hasCompleteCurrentLocation)
            return new(false, "INVALID_LOCATION");

        LocationUpdate? suppliedLocation = null;
        double latitude;
        double longitude;
        if (hasCompleteCurrentLocation)
        {
            suppliedLocation = new LocationUpdate(
                request.Latitude!.Value,
                request.Longitude!.Value,
                request.AccuracyMeters!.Value,
                request.Timestamp!.Value,
                request.SpeedMetersPerSecond,
                request.BearingDegrees);
            var qualityError = gpsValidator.Validate(suppliedLocation, session, DateTime.UtcNow);
            if (qualityError is not null) return new(false, qualityError);
            latitude = suppliedLocation.Latitude;
            longitude = suppliedLocation.Longitude;
        }
        else if (session.LastLatitude is { } lastLatitude && session.LastLongitude is { } lastLongitude)
        {
            latitude = lastLatitude;
            longitude = lastLongitude;
        }
        else
        {
            return new(false, "NO_RELIABLE_LOCATION");
        }

        var preference = NormalizePreference(request.Preference ?? session.OriginalPreference);
        if (request.Preference is not null && preference is null)
            return new(false, "INVALID_PREFERENCE");
        var budget = request.ClearBudget ? null : request.Budget ?? session.OriginalBudget;
        if (budget.HasValue && budget.Value <= 0)
            return new(false, "INVALID_BUDGET");

        var avoidTransportMode = NormalizeTransportMode(request.AvoidTransportMode);
        if (request.AvoidTransportMode is not null && avoidTransportMode is null)
            return new(false, "INVALID_AVOID_TRANSPORT_MODE");

        var hasAnyDestinationField = request.DestinationName is not null ||
            request.DestinationLatitude is not null || request.DestinationLongitude is not null;
        var hasCompleteDestination = !string.IsNullOrWhiteSpace(request.DestinationName) &&
            request.DestinationLatitude is not null && request.DestinationLongitude is not null;
        if (hasAnyDestinationField && !hasCompleteDestination)
            return new(false, "INVALID_DESTINATION");

        var destinationName = hasCompleteDestination ? request.DestinationName!.Trim() : session.DestinationName;
        var destinationLatitude = hasCompleteDestination ? request.DestinationLatitude!.Value : session.DestinationLatitude;
        var destinationLongitude = hasCompleteDestination ? request.DestinationLongitude!.Value : session.DestinationLongitude;

        var previousState = session.CurrentNavigationState;
        if (!stateMachine.CanTransition(previousState, TripNavigationState.Rerouting))
            return new(false, "INVALID_STATE_TRANSITION");

        session.CurrentNavigationState = TripNavigationState.Rerouting;
        session.LastNavigationStatus = "REROUTING";
        if (suppliedLocation is not null)
        {
            session.LastLatitude = suppliedLocation.Latitude;
            session.LastLongitude = suppliedLocation.Longitude;
            session.LastAccuracyMeters = suppliedLocation.AccuracyMeters;
            session.LastLocationAt = suppliedLocation.Timestamp;
        }
        session.UpdatedAt = DateTime.UtcNow;
        _telemetry.Event("RerouteStarted", sessionId, normalizedReason);
        await sessions.UpdateAsync(session, cancellationToken);

        try
        {
            var plans = await routing.PlanTripsAsync(latitude, longitude,
                destinationLatitude, destinationLongitude, cancellationToken);
            var eligible = plans
                .Where(plan => RoutingPlanSafety.HasValidTransitAccess(
                    plan,
                    _routingOptions.MaxWalkAccessDistanceMeters))
                .Where(plan => budget is null || (decimal)plan.TotalFarePesos <= budget.Value)
                .Where(plan => !UsesTransportMode(plan, avoidTransportMode))
                .ToList();
            var selected = Select(eligible, preference);
            if (selected is null)
                return await RestoreAfterFailureAsync(session, previousState,
                    "NO_REROUTE_AVAILABLE", "NO_ROUTE", cancellationToken);

            var recommendation = await PersistAsync(session, selected, latitude, longitude,
                destinationName, destinationLatitude, destinationLongitude, budget, preference,
                normalizedReason, avoidTransportMode, cancellationToken);

            session.RecommendationId = recommendation.RecommendationId;
            session.DestinationName = destinationName;
            session.DestinationLatitude = destinationLatitude;
            session.DestinationLongitude = destinationLongitude;
            session.OriginalBudget = budget;
            session.OriginalPreference = preference;
            session.CurrentLegIndex = 0;
            session.CurrentProgressMeters = 0;
            session.CurrentRouteProgressMeters = null;
            session.ConsecutiveStateConfirmationSamples = 0;
            session.ConsecutiveOffRouteSamples = 0;
            session.OffRouteSuspectedAt = null;
            session.LastRerouteAt = DateTime.UtcNow;
            session.LastRerouteReason = normalizedReason;
            session.LastNavigationStatus = "REROUTE_SUCCEEDED";
            session.RerouteCount++;

            if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.Starting))
                throw new InvalidOperationException("Reroute produced an invalid session transition.");
            session.CurrentNavigationState = TripNavigationState.Starting;
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);

            await instructions.GenerateAsync(session, cancellationToken);
            await landmarkPrefetch.PrefetchAsync(session, cancellationToken);
            var reroutedLegs = await recommendations.GetOrderedLegsAsync(
                session.RecommendationId, cancellationToken);
            var firstLeg = reroutedLegs.OrderBy(item => item.LegOrder).FirstOrDefault();
            if (firstLeg is null)
                throw new InvalidOperationException("Reroute produced no journey legs.");

            var resumedState = IsWalking(firstLeg)
                ? (reroutedLegs.Count == 1
                    ? TripNavigationState.WalkingToDestination
                    : TripNavigationState.WalkingToPickup)
                : TripNavigationState.WaitingToBoard;
            if (!stateMachine.CanTransition(session.CurrentNavigationState, resumedState))
                throw new InvalidOperationException("Reroute produced an invalid resumed state.");
            session.CurrentNavigationState = resumedState;
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);
            _telemetry.Event("RerouteSucceeded", sessionId, normalizedReason);
            return new(true, "REROUTE_SUCCEEDED", recommendation.RecommendationId);
        }
        catch (RoutingValidationException exception)
        {
            return await RestoreAfterFailureAsync(session, previousState,
                exception.ErrorCode, exception.ErrorCode, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await RestoreAfterFailureAsync(session, previousState,
                "NO_REROUTE_AVAILABLE", "ERROR", cancellationToken);
        }
    }

    public async Task<RerouteResult> ApplyRecommendationAsync(
        Guid userId,
        Guid sessionId,
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("Rerouting.ApplyProposal");
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return new(false, "TRIP_SESSION_NOT_FOUND");
        if (session.CurrentNavigationState is TripNavigationState.Planned or
            TripNavigationState.Arrived or TripNavigationState.Cancelled)
            return new(false, "TRIP_NOT_ACTIVE");
        if (session.CurrentNavigationState == TripNavigationState.Rerouting)
            return new(false, "REROUTE_IN_PROGRESS");

        var recommendation = await recommendations.GetByIdAsync(
            recommendationId, cancellationToken);
        if (recommendation is null)
            return new(false, "REPLAN_PROPOSAL_NOT_FOUND");

        var search = await searches.GetByIdAsync(
            recommendation.TripSearchId, cancellationToken);
        if (search?.UserId != userId)
            return new(false, "REPLAN_PROPOSAL_NOT_FOUND");

        if (search.RequestedAt < DateTime.UtcNow - AssistantProposalLifetime)
            return new(false, "REPLAN_PROPOSAL_STALE");

        if (session.LastLatitude is not { } currentLatitude ||
            session.LastLongitude is not { } currentLongitude)
            return new(false, "NO_RELIABLE_LOCATION");

        var originDrift = Geo.DistanceMeters(
            currentLatitude,
            currentLongitude,
            search.OriginLatitude,
            search.OriginLongitude);
        if (originDrift > AssistantProposalMaxOriginDriftMeters)
            return new(false, "REPLAN_PROPOSAL_STALE");

        var proposedLegs = await recommendations.GetOrderedLegsAsync(
            recommendationId, cancellationToken);
        var firstLeg = proposedLegs.OrderBy(item => item.LegOrder).FirstOrDefault();
        if (firstLeg is null)
            return new(false, "JOURNEY_HAS_NO_LEGS");

        var previousState = session.CurrentNavigationState;
        if (!stateMachine.CanTransition(previousState, TripNavigationState.Rerouting) ||
            !stateMachine.CanTransition(TripNavigationState.Rerouting, TripNavigationState.Starting))
            return new(false, "INVALID_STATE_TRANSITION");

        var resumedState = IsWalking(firstLeg)
            ? (proposedLegs.Count == 1
                ? TripNavigationState.WalkingToDestination
                : TripNavigationState.WalkingToPickup)
            : TripNavigationState.WaitingToBoard;
        if (!stateMachine.CanTransition(TripNavigationState.Starting, resumedState))
            return new(false, "INVALID_STATE_TRANSITION");

        var previousRecommendationId = session.RecommendationId;
        var previousDestinationName = session.DestinationName;
        var previousDestinationLatitude = session.DestinationLatitude;
        var previousDestinationLongitude = session.DestinationLongitude;
        var previousBudget = session.OriginalBudget;
        var previousPreference = session.OriginalPreference;
        var previousLegIndex = session.CurrentLegIndex;
        var previousProgress = session.CurrentProgressMeters;
        var previousRouteProgress = session.CurrentRouteProgressMeters;
        var previousStatus = session.LastNavigationStatus;
        var previousRerouteReason = session.LastRerouteReason;
        var previousRerouteAt = session.LastRerouteAt;
        var previousRerouteCount = session.RerouteCount;

        try
        {
            session.RecommendationId = recommendationId;
            session.DestinationName = search.DestinationName;
            session.DestinationLatitude = search.DestinationLatitude;
            session.DestinationLongitude = search.DestinationLongitude;
            session.OriginalBudget = search.Budget;
            session.OriginalPreference = NormalizePreference(search.Preference);
            session.CurrentLegIndex = 0;
            session.CurrentProgressMeters = 0;
            session.CurrentRouteProgressMeters = null;
            session.ConsecutiveStateConfirmationSamples = 0;
            session.ConsecutiveOffRouteSamples = 0;
            session.OffRouteSuspectedAt = null;
            session.LastRerouteAt = DateTime.UtcNow;
            session.LastRerouteReason = "AI_CONFIRMED_REPLAN";
            session.LastNavigationStatus = "REROUTE_SUCCEEDED";
            session.RerouteCount++;
            session.CurrentNavigationState = TripNavigationState.Starting;
            session.UpdatedAt = DateTime.UtcNow;

            await sessions.UpdateAsync(session, cancellationToken);
            await instructions.GenerateAsync(session, cancellationToken);
            await landmarkPrefetch.PrefetchAsync(session, cancellationToken);

            session.CurrentNavigationState = resumedState;
            session.UpdatedAt = DateTime.UtcNow;
            await sessions.UpdateAsync(session, cancellationToken);

            _telemetry.Event("RerouteSucceeded", sessionId, "AI_CONFIRMED_REPLAN");
            return new(true, "REROUTE_SUCCEEDED", recommendationId);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            session.RecommendationId = previousRecommendationId;
            session.DestinationName = previousDestinationName;
            session.DestinationLatitude = previousDestinationLatitude;
            session.DestinationLongitude = previousDestinationLongitude;
            session.OriginalBudget = previousBudget;
            session.OriginalPreference = previousPreference;
            session.CurrentLegIndex = previousLegIndex;
            session.CurrentProgressMeters = previousProgress;
            session.CurrentRouteProgressMeters = previousRouteProgress;
            session.LastNavigationStatus = previousStatus;
            session.LastRerouteReason = previousRerouteReason;
            session.LastRerouteAt = previousRerouteAt;
            session.RerouteCount = previousRerouteCount;
            session.CurrentNavigationState = previousState;
            session.UpdatedAt = DateTime.UtcNow;

            await sessions.UpdateAsync(session, cancellationToken);
            try
            {
                await instructions.GenerateAsync(session, cancellationToken);
                await landmarkPrefetch.PrefetchAsync(session, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The original session state is authoritative even if refreshing
                // its cached guidance also fails.
            }

            _telemetry.Event("RerouteFailed", sessionId, "AI_REPLAN_APPLY_ERROR");
            return new(false, "REPLAN_APPLY_FAILED");
        }
    }

    private async Task<RerouteResult> RestoreAfterFailureAsync(
        TripSession session, TripNavigationState previousState, string status,
        string telemetryReason, CancellationToken cancellationToken)
    {
        if (stateMachine.CanTransition(session.CurrentNavigationState, previousState))
            session.CurrentNavigationState = previousState;
        session.LastNavigationStatus = status;
        session.UpdatedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(session, cancellationToken);
        _telemetry.Event("RerouteFailed", session.TripSessionId, telemetryReason);
        return new(false, status);
    }

    private static string? NormalizePreference(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference)) return null;
        return preference.Trim().ToLowerInvariant() switch
        {
            "balanced" or "efficient" => "efficient",
            "fastest" => "fastest",
            "cheapest" => "cheapest",
            _ => null
        };
    }

    private static string? NormalizeTransportMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        return mode.Trim().ToUpperInvariant() switch
        {
            "TRIKE" or "TRICYCLE" or "TODA" => "TRICYCLE",
            "WALK" or "WALKING" or "PEDESTRIAN" => "WALK",
            "JEEP" or "JEEPNEY" => "JEEPNEY",
            _ => null
        };
    }

    private static bool UsesTransportMode(JeepneyTripPlan plan, string? mode) => mode switch
    {
        "TRICYCLE" => plan.Legs.Any(item => item.Mode == AccessMode.Trike),
        "WALK" => plan.Legs.Any(item => item.Mode == AccessMode.Walk),
        "JEEPNEY" => plan.Legs.Any(item => item.Mode != AccessMode.Walk && item.Mode != AccessMode.Trike),
        _ => false
    };

    private static JeepneyTripPlan? Select(List<JeepneyTripPlan> plans, string? preference) =>
        plans.FirstOrDefault(plan => preference is not null &&
            plan.RecommendationType.Split(',').Any(item =>
                string.Equals(item, preference, StringComparison.OrdinalIgnoreCase))) ??
        plans.OrderBy(plan => plan.GeneralizedCostPesos).FirstOrDefault();

    private async Task<RouteRecommendation> PersistAsync(
        TripSession session, JeepneyTripPlan plan, double latitude, double longitude,
        string? destinationName, double destinationLatitude, double destinationLongitude,
        decimal? budget, string? preference, string rerouteReason, string? avoidTransportMode,
        CancellationToken cancellationToken)
    {
        var search = await searches.AddAsync(new TripSearch
        {
            UserId = session.UserId,
            OriginName = "Current location",
            OriginLatitude = latitude,
            OriginLongitude = longitude,
            DestinationName = destinationName,
            DestinationLatitude = destinationLatitude,
            DestinationLongitude = destinationLongitude,
            Budget = budget,
            Preference = preference,
            PassengerCount = 1,
            RequestedAt = DateTime.UtcNow
        }, cancellationToken);
        var recommendation = await recommendations.AddAsync(new RouteRecommendation
        {
            TripSearchId = search.TripSearchId,
            RecommendationType = plan.RecommendationType,
            RankNumber = 1,
            TotalFare = (decimal)plan.TotalFarePesos,
            TotalMinutes = (decimal)(plan.TotalTimeSeconds / 60),
            TotalDistanceMeters = (decimal)plan.Legs.Sum(item => item.DistanceMeters),
            WalkingDistanceMeters = (decimal)plan.Legs.Where(item => item.Mode == AccessMode.Walk)
                .Sum(item => item.DistanceMeters),
            TransferCount = plan.TransferCount,
            RecommendationScore = (decimal)plan.GeneralizedCostPesos,
            Explanation = avoidTransportMode is null
                ? $"Rerouted from current reliable location ({rerouteReason})"
                : $"Rerouted from current reliable location ({rerouteReason}); avoided {avoidTransportMode}",
            GeneratedAt = DateTime.UtcNow
        }, cancellationToken);
        foreach (var (leg, index) in plan.Legs.Select((value, index) => (value, index)))
        {
            var mode = await modes.GetByCodeAsync(leg.Mode switch
            {
                AccessMode.Walk => "WALK",
                AccessMode.Trike => "TRICYCLE",
                _ => "JEEPNEY"
            }, cancellationToken);
            var route = leg.RouteId is null
                ? null
                : await routes.GetByRouteCodeAsync(leg.RouteId, cancellationToken);
            if (mode is null)
                throw new InvalidOperationException("Required transport mode is not configured.");
            await legs.AddAsync(new RecommendationLeg
            {
                RecommendationId = recommendation.RecommendationId,
                LegOrder = index,
                TransportModeId = mode.TransportModeId,
                RouteId = route?.RouteId,
                FromName = leg.RouteName,
                ToName = destinationName,
                StartLatitude = leg.OriginLatitude != 0 ? leg.OriginLatitude : leg.BoardLatitude,
                StartLongitude = leg.OriginLongitude != 0 ? leg.OriginLongitude : leg.BoardLongitude,
                EndLatitude = leg.DestinationLatitude != 0 ? leg.DestinationLatitude : leg.AlightLatitude,
                EndLongitude = leg.DestinationLongitude != 0 ? leg.DestinationLongitude : leg.AlightLongitude,
                DistanceMeters = (decimal)leg.DistanceMeters,
                EstimatedMinutes = (decimal)(leg.DurationSeconds / 60),
                EstimatedFare = (decimal)leg.FarePesos,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        return recommendation;
    }

    private static bool IsWalking(RecommendationLeg leg) =>
        leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";
}
