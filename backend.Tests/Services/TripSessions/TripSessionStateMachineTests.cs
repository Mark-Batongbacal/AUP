using backend.Models.Database;
using backend.Services.TripSessions;

namespace backend.Tests.Services.TripSessions;

public sealed class TripSessionStateMachineTests
{
    private readonly TripSessionStateMachine _machine = new();

    [Theory]
    [InlineData(TripNavigationState.Planned, TripNavigationState.Starting)]
    [InlineData(TripNavigationState.Planned, TripNavigationState.Cancelled)]
    [InlineData(TripNavigationState.WaitingToBoard, TripNavigationState.OnJeepney)]
    [InlineData(TripNavigationState.WalkingToDestination, TripNavigationState.Arrived)]
    [InlineData(TripNavigationState.WalkingToPickup, TripNavigationState.Rerouting)]
    [InlineData(TripNavigationState.WaitingToBoard, TripNavigationState.Rerouting)]
    [InlineData(TripNavigationState.OnJeepney, TripNavigationState.Rerouting)]
    [InlineData(TripNavigationState.OnTricycle, TripNavigationState.Rerouting)]
    [InlineData(TripNavigationState.WalkingToDestination, TripNavigationState.Rerouting)]
    public void ValidTransitions_AreAllowed(TripNavigationState from, TripNavigationState to) =>
        Assert.True(_machine.CanTransition(from, to));

    [Theory]
    [InlineData(TripNavigationState.Planned, TripNavigationState.Arrived)]
    [InlineData(TripNavigationState.OnJeepney, TripNavigationState.Planned)]
    [InlineData(TripNavigationState.Cancelled, TripNavigationState.OnJeepney)]
    [InlineData(TripNavigationState.Arrived, TripNavigationState.Rerouting)]
    [InlineData(TripNavigationState.Cancelled, TripNavigationState.Rerouting)]
    public void InvalidAndTerminalTransitions_AreRejected(TripNavigationState from, TripNavigationState to) =>
        Assert.False(_machine.CanTransition(from, to));
}
