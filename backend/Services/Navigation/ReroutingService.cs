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
    Task<RerouteResult> RerouteAsync(Guid userId, Guid sessionId, string reason, CancellationToken cancellationToken = default);
}

public sealed class ReroutingService(
    ITripSessionRepository sessions, IRoutingService routing,
    ITripSearchRepository searches, IRouteRecommendationRepository recommendations,
    IRecommendationLegRepository legs, ITransportModeRepository modes,
    ITransportRouteRepository routes, INavigationInstructionService instructions,
    ILandmarkCorridorPrefetchService landmarkPrefetch,
    backend.Services.TripSessions.ITripSessionStateMachine stateMachine,
    IOptions<NavigationOptions> options,
    ITukiTelemetry? telemetry = null) : IReroutingService
{
    private readonly NavigationOptions _options = options.Value;
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;

    public async Task<RerouteResult> RerouteAsync(Guid userId, Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("Rerouting");
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return new(false, "TRIP_SESSION_NOT_FOUND");
        if (session.CurrentNavigationState != TripNavigationState.OffRoute)
            return new(false, "TRIP_NOT_OFF_ROUTE");
        if (session.LastRerouteAt is { } last && last.AddSeconds(_options.RerouteCooldownSeconds) > DateTime.UtcNow)
            return new(false, "REROUTE_COOLDOWN");
        if (session.LastLatitude is not { } latitude || session.LastLongitude is not { } longitude)
            return new(false, "NO_RELIABLE_LOCATION");

        if (!stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.Rerouting))
            return new(false, "INVALID_STATE_TRANSITION");
        session.CurrentNavigationState = TripNavigationState.Rerouting;
        _telemetry.Event("RerouteStarted", sessionId, reason);
        await sessions.UpdateAsync(session, cancellationToken);
        try
        {
            var plans = await routing.PlanTripsAsync(latitude, longitude,
                session.DestinationLatitude, session.DestinationLongitude, cancellationToken);
            var eligible = plans.Where(plan => session.OriginalBudget is null ||
                (decimal)plan.TotalFarePesos <= session.OriginalBudget.Value).ToList();
            var selected = Select(eligible, session.OriginalPreference);
            if (selected is null)
            {
                if (stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.OffRoute))
                    session.CurrentNavigationState = TripNavigationState.OffRoute;
                await sessions.UpdateAsync(session, cancellationToken);
                _telemetry.Event("RerouteFailed", sessionId, "NO_ROUTE");
                return new(false, "OFF_ROUTE_NO_REROUTE_AVAILABLE");
            }
            var recommendation = await PersistAsync(session, selected, latitude, longitude, cancellationToken);
            session.RecommendationId = recommendation.RecommendationId;
            session.CurrentLegIndex = 0;
            session.CurrentProgressMeters = 0;
            session.CurrentRouteProgressMeters = null;
            session.ConsecutiveOffRouteSamples = 0;
            session.OffRouteSuspectedAt = null;
            session.LastRerouteAt = DateTime.UtcNow;
            session.LastRerouteReason = string.IsNullOrWhiteSpace(reason) ? "OFF_ROUTE" : reason.Trim();
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
            _telemetry.Event("RerouteSucceeded", sessionId);
            return new(true, "REROUTE_SUCCEEDED", recommendation.RecommendationId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (stateMachine.CanTransition(session.CurrentNavigationState, TripNavigationState.OffRoute))
                session.CurrentNavigationState = TripNavigationState.OffRoute;
            await sessions.UpdateAsync(session, cancellationToken);
            _telemetry.Event("RerouteFailed", sessionId, "ERROR");
            return new(false, "OFF_ROUTE_NO_REROUTE_AVAILABLE");
        }
    }

    private static JeepneyTripPlan? Select(List<JeepneyTripPlan> plans, string? preference) =>
        plans.FirstOrDefault(plan => plan.RecommendationType.Split(',').Any(item =>
            string.Equals(item, preference, StringComparison.OrdinalIgnoreCase))) ??
        plans.OrderBy(plan => plan.GeneralizedCostPesos).FirstOrDefault();

    private async Task<RouteRecommendation> PersistAsync(TripSession session, JeepneyTripPlan plan,
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        var search = await searches.AddAsync(new TripSearch
        {
            UserId = session.UserId, OriginName = "Current location",
            OriginLatitude = latitude, OriginLongitude = longitude,
            DestinationName = session.DestinationName,
            DestinationLatitude = session.DestinationLatitude,
            DestinationLongitude = session.DestinationLongitude,
            Budget = session.OriginalBudget, Preference = session.OriginalPreference,
            PassengerCount = 1, RequestedAt = DateTime.UtcNow
        }, cancellationToken);
        var recommendation = await recommendations.AddAsync(new RouteRecommendation
        {
            TripSearchId = search.TripSearchId, RecommendationType = plan.RecommendationType,
            RankNumber = 1, TotalFare = (decimal)plan.TotalFarePesos,
            TotalMinutes = (decimal)(plan.TotalTimeSeconds / 60),
            TotalDistanceMeters = (decimal)plan.Legs.Sum(item => item.DistanceMeters),
            WalkingDistanceMeters = (decimal)plan.Legs.Where(item => item.Mode == AccessMode.Walk).Sum(item => item.DistanceMeters),
            TransferCount = plan.TransferCount, RecommendationScore = (decimal)plan.GeneralizedCostPesos,
            Explanation = "Rerouted from current reliable location", GeneratedAt = DateTime.UtcNow
        }, cancellationToken);
        foreach (var (leg, index) in plan.Legs.Select((value, index) => (value, index)))
        {
            var mode = await modes.GetByCodeAsync(leg.Mode switch
            { AccessMode.Walk => "WALK", AccessMode.Trike => "TRICYCLE", _ => "JEEPNEY" }, cancellationToken);
            var route = leg.RouteId is null ? null : await routes.GetByRouteCodeAsync(leg.RouteId, cancellationToken);
            if (mode is null) throw new InvalidOperationException("Required transport mode is not configured.");
            await legs.AddAsync(new RecommendationLeg
            {
                RecommendationId = recommendation.RecommendationId, LegOrder = index,
                TransportModeId = mode.TransportModeId, RouteId = route?.RouteId,
                FromName = leg.RouteName, ToName = session.DestinationName,
                StartLatitude = leg.OriginLatitude != 0 ? leg.OriginLatitude : leg.BoardLatitude,
                StartLongitude = leg.OriginLongitude != 0 ? leg.OriginLongitude : leg.BoardLongitude,
                EndLatitude = leg.DestinationLatitude != 0 ? leg.DestinationLatitude : leg.AlightLatitude,
                EndLongitude = leg.DestinationLongitude != 0 ? leg.DestinationLongitude : leg.AlightLongitude,
                DistanceMeters = (decimal)leg.DistanceMeters,
                EstimatedMinutes = (decimal)(leg.DurationSeconds / 60),
                EstimatedFare = (decimal)leg.FarePesos, CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        return recommendation;
    }

    private static bool IsWalking(RecommendationLeg leg) =>
        leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";
}
