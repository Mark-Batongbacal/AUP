using backend.Models.Database;

namespace backend.Services.TripSessions;

public interface ITripSessionStateMachine
{
    bool CanTransition(TripNavigationState from, TripNavigationState to);
}

public sealed class TripSessionStateMachine : ITripSessionStateMachine
{
    private static readonly IReadOnlyDictionary<TripNavigationState, HashSet<TripNavigationState>> Valid =
        new Dictionary<TripNavigationState, HashSet<TripNavigationState>>
        {
            [TripNavigationState.Planned] = [TripNavigationState.Starting, TripNavigationState.Cancelled],
            [TripNavigationState.Starting] = [TripNavigationState.WalkingToPickup, TripNavigationState.WaitingToBoard, TripNavigationState.WalkingToDestination, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.WalkingToPickup] = [TripNavigationState.ApproachingBoardPoint, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.ApproachingBoardPoint] = [TripNavigationState.WaitingToBoard, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.WaitingToBoard] = [TripNavigationState.OnJeepney, TripNavigationState.OnTricycle, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.OnJeepney] = [TripNavigationState.ApproachingAlightPoint, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.OnTricycle] = [TripNavigationState.ApproachingAlightPoint, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.ApproachingAlightPoint] = [TripNavigationState.Transferring, TripNavigationState.WalkingToDestination, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.Transferring] = [TripNavigationState.WalkingToPickup, TripNavigationState.ApproachingBoardPoint, TripNavigationState.WaitingToBoard, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.WalkingToDestination] = [TripNavigationState.Arrived, TripNavigationState.OffRoute, TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.OffRoute] = [TripNavigationState.Rerouting, TripNavigationState.Cancelled],
            [TripNavigationState.Rerouting] = [
                TripNavigationState.Starting,
                TripNavigationState.WalkingToPickup,
                TripNavigationState.ApproachingBoardPoint,
                TripNavigationState.WaitingToBoard,
                TripNavigationState.OnJeepney,
                TripNavigationState.OnTricycle,
                TripNavigationState.ApproachingAlightPoint,
                TripNavigationState.Transferring,
                TripNavigationState.WalkingToDestination,
                TripNavigationState.OffRoute,
                TripNavigationState.Cancelled
            ],
            [TripNavigationState.Arrived] = [],
            [TripNavigationState.Cancelled] = []
        };

    public bool CanTransition(TripNavigationState from, TripNavigationState to) =>
        Valid.TryGetValue(from, out var next) && next.Contains(to);
}
