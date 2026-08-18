using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.Telemetry;

namespace backend.Services.TripSessions;

public sealed class TripSessionService(
    ITripSessionRepository sessions,
    IRouteRecommendationRepository recommendations,
    ITripSearchRepository searches,
    ITripSessionStateMachine stateMachine,
    INavigationInstructionService navigationInstructions,
    ILandmarkCorridorPrefetchService landmarkPrefetch,
    ITukiTelemetry? telemetry = null) : ITripSessionService
{
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    public async Task<TripSessionOperation> CreateAsync(
        Guid userId, CreateTripSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request.RecommendationId == Guid.Empty)
            return Fail("INVALID_REQUEST");
        if (await sessions.GetActiveOwnedAsync(userId, cancellationToken) is not null)
            return Fail("ACTIVE_TRIP_EXISTS");

        var recommendation = await recommendations.GetByIdAsync(request.RecommendationId, cancellationToken);
        if (recommendation is null) return Fail("JOURNEY_NOT_FOUND");
        var search = await searches.GetByIdAsync(recommendation.TripSearchId, cancellationToken);
        if (search?.UserId != userId) return Fail("JOURNEY_NOT_FOUND");

        var now = DateTime.UtcNow;
        var session = new TripSession
        {
            UserId = userId,
            RecommendationId = recommendation.RecommendationId,
            OriginLatitude = search.OriginLatitude,
            OriginLongitude = search.OriginLongitude,
            DestinationLatitude = search.DestinationLatitude,
            DestinationLongitude = search.DestinationLongitude,
            DestinationName = search.DestinationName,
            OriginalBudget = search.Budget,
            OriginalPreference = search.Preference,
            CurrentNavigationState = TripNavigationState.Planned,
            CreatedAt = now,
            UpdatedAt = now
        };
        _telemetry.Event("TripPlanned");
        return new(await sessions.AddAsync(session, cancellationToken));
    }

    public async Task<TripSessionOperation> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        Wrap(await sessions.GetOwnedAsync(sessionId, userId, cancellationToken));

    public async Task<TripSessionOperation> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Wrap(await sessions.GetActiveOwnedAsync(userId, cancellationToken), "NO_ACTIVE_TRIP");

    public async Task<TripSessionOperation> StartAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = await TransitionAsync(userId, sessionId, TripNavigationState.Starting, cancellationToken,
            session => session.StartedAt = DateTime.UtcNow);
        if (result.Session is not null)
        {
            await navigationInstructions.GenerateAsync(result.Session, cancellationToken);
            await landmarkPrefetch.PrefetchAsync(result.Session, cancellationToken);
            var legs = await recommendations.GetOrderedLegsAsync(
                result.Session.RecommendationId, cancellationToken);
            var first = legs.OrderBy(leg => leg.LegOrder).FirstOrDefault();
            if (first is null) return Fail("JOURNEY_HAS_NO_LEGS");
            var firstState = IsWalking(first)
                ? (legs.Count == 1
                    ? TripNavigationState.WalkingToDestination
                    : TripNavigationState.WalkingToPickup)
                : TripNavigationState.WaitingToBoard;
            result = await TransitionAsync(
                userId, sessionId, firstState, cancellationToken);
            _telemetry.Event("TripStarted", sessionId);
        }
        return result;
    }

    public async Task<TripSessionOperation> CancelAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = await TransitionAsync(userId, sessionId, TripNavigationState.Cancelled, cancellationToken,
            session => session.CancelledAt = DateTime.UtcNow);
        if (result.Succeeded) _telemetry.Event("TripCancelled", sessionId);
        return result;
    }

    public async Task<TripSessionOperation> ConfirmBoardingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var current = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (current is null) return Fail("TRIP_SESSION_NOT_FOUND");
        var legs = await recommendations.GetOrderedLegsAsync(current.RecommendationId, cancellationToken);
        var mode = legs.FirstOrDefault(leg => leg.LegOrder == current.CurrentLegIndex)
            ?.TransportMode?.Code;
        var state = mode is "TRICYCLE" or "TRIKE"
            ? TripNavigationState.OnTricycle
            : TripNavigationState.OnJeepney;
        var result = await TransitionAsync(userId, sessionId, state, cancellationToken);
        if (result.Succeeded) _telemetry.Event("BoardingConfirmed", sessionId, state.ToString());
        return result;
    }

    public async Task<TripSessionOperation> ConfirmAlightingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var current = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (current is null) return Fail("TRIP_SESSION_NOT_FOUND");
        var legs = await recommendations.GetOrderedLegsAsync(current.RecommendationId, cancellationToken);
        var nextIndex = current.CurrentLegIndex + 1;
        var nextLeg = legs.FirstOrDefault(leg => leg.LegOrder == nextIndex);
        var nextState = nextLeg is null
            ? TripNavigationState.WalkingToDestination
            : IsWalking(nextLeg)
                ? (nextIndex == legs.Max(leg => leg.LegOrder)
                    ? TripNavigationState.WalkingToDestination
                    : TripNavigationState.Transferring)
                : TripNavigationState.WaitingToBoard;
        var result = await TransitionAsync(userId, sessionId, nextState, cancellationToken,
            session => session.CurrentLegIndex = Math.Min(nextIndex, legs.Count));
        if (result.Succeeded) _telemetry.Event("AlightingConfirmed", sessionId);
        return result;
    }

    private async Task<TripSessionOperation> TransitionAsync(
        Guid userId, Guid sessionId, TripNavigationState target,
        CancellationToken cancellationToken, Action<TripSession>? apply = null)
    {
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return Fail("TRIP_SESSION_NOT_FOUND");
        if (!stateMachine.CanTransition(session.CurrentNavigationState, target))
            return Fail("INVALID_STATE_TRANSITION");
        apply?.Invoke(session);
        session.CurrentNavigationState = target;
        session.UpdatedAt = DateTime.UtcNow;
        return new(await sessions.UpdateAsync(session, cancellationToken));
    }

    private static TripSessionOperation Wrap(TripSession? session, string error = "TRIP_SESSION_NOT_FOUND") =>
        session is null ? Fail(error) : new(session);
    private static TripSessionOperation Fail(string error) => new(null, error);
    private static bool IsWalking(RecommendationLeg leg) =>
        leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";
}
