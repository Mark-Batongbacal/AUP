using backend.Models.Database;
using backend.Services.Navigation;

namespace backend.Tests.Services.Navigation;

public sealed class NavigationTripRulesTests
{
    [Fact]
    public void CanConfirmAlighting_UsesDedicated75MeterThreshold()
    {
        var leg = Leg(0, "JEEPNEY", 1000, 13);
        var session = Session(TripNavigationState.ApproachingAlightPoint, progress: 924);
        var options = new NavigationOptions { ConfirmAlightDistanceMeters = 75 };

        Assert.False(NavigationTripRules.CanConfirmAlighting(session, leg, options));

        session.CurrentProgressMeters = 925;
        Assert.True(NavigationTripRules.CanConfirmAlighting(session, leg, options));
    }

    [Fact]
    public void EstimatedRemainingFare_UsesOnlyCurrentAndFutureLegs()
    {
        var session = Session(TripNavigationState.WalkingToDestination, currentLeg: 2);
        var legs = new List<RecommendationLeg>
        {
            Leg(0, "JEEPNEY", 1000, 13),
            Leg(1, "WALK", 250, 0),
            Leg(2, "TRICYCLE", 900, 30),
            Leg(3, "WALK", 100, 0)
        };

        Assert.Equal(30, NavigationTripRules.EstimatedRemainingFare(session, legs));
    }

    [Fact]
    public void IsPaidTransport_ExcludesWalkingEvenWhenFareDataIsUnexpected()
    {
        Assert.True(NavigationTripRules.IsPaidTransport(Leg(0, "JEEPNEY", 500, 13)));
        Assert.True(NavigationTripRules.IsPaidTransport(Leg(0, "TRICYCLE", 500, 25)));
        Assert.False(NavigationTripRules.IsPaidTransport(Leg(0, "WALK", 500, 5)));
    }

    private static TripSession Session(
        TripNavigationState state,
        double progress = 0,
        int currentLeg = 0) => new()
    {
        CurrentNavigationState = state,
        CurrentProgressMeters = progress,
        CurrentLegIndex = currentLeg
    };

    private static RecommendationLeg Leg(
        int order,
        string mode,
        decimal distance,
        decimal fare) => new()
    {
        LegOrder = order,
        TransportMode = new TransportMode { Code = mode },
        DistanceMeters = distance,
        EstimatedFare = fare
    };
}
