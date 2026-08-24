using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// A jeepney whose only purpose is to reach another jeepney the passenger
/// could already have boarded where they started is not a transfer, it is a
/// wasted fare, a wasted boarding wait and a pointless walk.
///
/// Both scenarios below have route A and route B meeting at the passenger's
/// initial boarding area AND meeting again downstream, so ordinary graph
/// search can build A -> B in each. They differ only in which OCCURRENCE of B
/// sits at the initial area:
///
///   * in the first, that occurrence can still carry the passenger to the
///     destination, so riding A first buys nothing and must not be offered as
///     a recommendation;
///   * in the second, B only passes the initial area on its return leg, after
///     the section the passenger needs, so boarding there leads away from the
///     destination and A -> B is genuinely required.
///
/// Coordinates alone cannot tell those apart -- in both cases the routes touch
/// at the same place. Only full-route progress can.
/// </summary>
public sealed class RedundantTransitPrefixRegressionTests
{
    private const string ShortHop = "A-SHORT-HOP";
    private const string ThroughRoute = "B-THROUGH";

    // -----------------------------------------------------------------
    // Route B runs straight past the origin and on to the destination. Route A
    // also passes the origin, loops north, and rejoins B about 800 m along it.
    // Riding A costs a fare, a boarding wait and a transfer walk to end up on
    // the same vehicle the passenger could have caught at the start.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_DoesNotRideOneJeepneyJustToReachAnother()
    {
        var plans = await PlanUsableOccurrenceAsync();

        Assert.NotEmpty(plans);

        // The simple journey must exist and be the recommendation.
        var objectives = plans
            .Where(plan => plan.RecommendationType != "alternative")
            .ToList();
        Assert.NotEmpty(objectives);
        Assert.All(objectives, plan => Assert.Equal(
            [ThroughRoute],
            JeepneyRouteIds(plan)));

        // And the redundant prefix must not be offered at all: there is a
        // confirmed journey that drops it and is no worse on every count.
        Assert.DoesNotContain(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([ShortHop, ThroughRoute]));
    }

    // -----------------------------------------------------------------
    // Same physical meeting point, wrong occurrence. B runs east past the
    // destination first, then turns and comes back west past the origin near
    // the very end of its run, so the pass beside the origin is 7.9 km into
    // the route and cannot reach a destination that sits at 3.2 km. Route A is
    // the only way onto B's useful section, and this journey must survive.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_KeepsTheTransferWhenTheNearOccurrenceLeadsAway()
    {
        var plans = await PlanWrongOccurrenceAsync();

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([ShortHop, ThroughRoute]));
    }

    // -----------------------------------------------------------------
    // Scenarios
    // -----------------------------------------------------------------

    private static Task<List<JeepneyTripPlan>> PlanUsableOccurrenceAsync()
    {
        // B: straight east, passing ~22 m south of the origin, on to the
        // destination.
        var through = Route(2, ThroughRoute, "Through corridor",
        [
            (15.0498, 120.4980),
            (15.0498, 120.5400)
        ]);

        // A: runs right past the origin -- ~6 m, closer than B -- then arcs
        // north and comes back to touch B about 800 m along it.
        //
        // Those few metres are the whole point. They give A -> B slightly less
        // walking and slightly less access time than boarding B directly, so
        // Pareto pruning cannot dominate it even though it costs an extra
        // fare, an extra boarding wait and four more minutes. This is the real
        // network's failure mode reproduced: there the margin was two metres.
        var shortHop = Route(1, ShortHop, "Short hop",
        [
            (15.05005, 120.4980),
            (15.05005, 120.5002),
            (15.0530, 120.5022),
            (15.04995, 120.5061),
            (15.04985, 120.5064)
        ]);

        return PlanAsync(
            [shortHop, through],
            origin: (15.0500, 120.4995),
            destination: (15.0496, 120.5395));
    }

    private static Task<List<JeepneyTripPlan>> PlanWrongOccurrenceAsync()
    {
        // B: east along the northern road (the useful section), then back west
        // along the southern road, ending beside the origin.
        var through = Route(2, ThroughRoute, "Through corridor",
        [
            (15.0520, 120.5100),
            (15.0520, 120.5400),
            (15.0498, 120.5400),
            (15.0498, 120.4980)
        ]);

        // A: from beside the origin, arcing north so it does not shadow B's
        // return leg, then down to B's starting area.
        var shortHop = Route(1, ShortHop, "Short hop",
        [
            (15.0502, 120.4985),
            (15.0545, 120.5040),
            (15.0524, 120.5104)
        ]);

        return PlanAsync(
            [shortHop, through],
            origin: (15.0500, 120.4995),
            destination: (15.0522, 120.5395));
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static async Task<List<JeepneyTripPlan>> PlanAsync(
        List<TransportRoute> routes,
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new RoutingService(
            new RoadNetworkValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions
            {
                MaxTransfers = 2,
                MaxCandidatesToConfirm = 200,
                MaxInterchangesPerRoutePair = 4,
                MaxTransferWalkMeters = 400,
                DefaultSampleIntervalMeters = 150,
                MaxRouteSamples = 60,
                MaxWalkAccessDistanceMeters = 600,
                MaxWalkOnlyTripDistanceMeters = 400,
                MaxWalkTrikeTripDistanceMeters = 400,
                MaxTotalWalkingMetersPerJourney = 2_500,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                MaxTripOptions = 10
            }));

        return await service.PlanTripsAsync(
            origin.Latitude, origin.Longitude,
            destination.Latitude, destination.Longitude);
    }

    private static TransportRoute Route(
        int routeId,
        string routeCode,
        string routeName,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints) =>
        ProductionTopologyFixture.BuildDenseRoute(routeId, routeCode, routeName, waypoints);

    private static string[] JeepneyRouteIds(JeepneyTripPlan plan) =>
        plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Select(leg => leg.RouteId!)
            .ToArray();
}
