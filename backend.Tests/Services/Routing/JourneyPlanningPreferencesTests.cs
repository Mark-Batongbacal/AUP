using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class JourneyPlanningPreferencesTests
{
    [Fact]
    public async Task PlanTripsAsync_ExplicitNeutralPreferencesMatchesOrdinaryPipeline()
    {
        var service = ProductionNetworkFixture.CreateService();
        const double originLatitude = 15.1140403;
        const double originLongitude = 120.5831296;
        const double destinationLatitude = 15.144311680416919;
        const double destinationLongitude = 120.595954648114059;

        var ordinary = await service.PlanTripsAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude);
        var explicitNeutral = await service.PlanTripsAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            new JourneyPlanningPreferences());

        Assert.NotEmpty(ordinary);
        Assert.Equal(
            ordinary.Select(PlanIdentity),
            explicitNeutral.Select(PlanIdentity));
    }

    [Fact]
    public void SelectObjectivePlans_ExplicitPreferencesChangeFinalOrder()
    {
        var service = new RoutingService(
            new Mock<IValhallaService>().Object,
            new Mock<ITransportRouteRepository>().Object,
            new Mock<ITricyclePointRepository>().Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions { MaxTripOptions = 2 }));
        var cheapest = Plan("cheap", fare: 20, time: 1_800, walking: 800, cost: 48);
        var fastest = Plan("fast", fare: 70, time: 600, walking: 100, cost: 80);

        var cheapFirst = service.SelectObjectivePlans(
            [cheapest, fastest],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Cheapest));
        var fastFirst = service.SelectObjectivePlans(
            [cheapest, fastest],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Fastest));

        Assert.Same(cheapest, cheapFirst[0]);
        Assert.Same(fastest, fastFirst[0]);
    }

    [Fact]
    public void SelectObjectivePlans_WithoutPreferencesPreservesObjectiveSet()
    {
        var service = new RoutingService(
            new Mock<IValhallaService>().Object,
            new Mock<ITransportRouteRepository>().Object,
            new Mock<ITricyclePointRepository>().Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions { MaxTripOptions = 3 }));
        var cheapest = Plan("cheap", fare: 20, time: 1_800, walking: 800, cost: 48);
        var fastest = Plan("fast", fare: 70, time: 600, walking: 100, cost: 80);

        var selected = service.SelectObjectivePlans([cheapest, fastest]);

        Assert.Contains(cheapest, selected);
        Assert.Contains(fastest, selected);
    }

    private static JeepneyTripPlan Plan(
        string routeId,
        double fare,
        double time,
        double walking,
        double cost) => new()
    {
        OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        TotalFarePesos = fare,
        TotalTimeSeconds = time,
        GeneralizedCostPesos = cost,
        Legs =
        [
            new JeepneyTripLeg
            {
                Mode = AccessMode.Walk,
                RouteId = routeId,
                DistanceMeters = walking,
                DurationSeconds = walking,
                OriginLatitude = 15,
                OriginLongitude = 120,
                DestinationLatitude = 15,
                DestinationLongitude = 120
            }
        ]
    };

    private static string PlanIdentity(JeepneyTripPlan plan) =>
        $"{plan.RecommendationType}|{plan.TotalFarePesos:F2}|" +
        $"{plan.TotalTimeSeconds:F3}|{plan.GeneralizedCostPesos:F3}|" +
        string.Join(';', plan.Legs.Select(leg =>
            $"{leg.Mode}:{leg.RouteId}:{leg.OriginLatitude:F7}:" +
            $"{leg.OriginLongitude:F7}:{leg.DestinationLatitude:F7}:" +
            $"{leg.DestinationLongitude:F7}"));
}
