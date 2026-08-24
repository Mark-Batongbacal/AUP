using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// A network at production scale: sixteen jeepney routes and twenty-five
/// tricycle terminals, matching what the live service actually loads.
///
/// The useful journey needs exactly two transfers:
///
///     origin -> LOCAL-WEST -> (transfer) -> LINK-NORTHEAST
///            -> (transfer) -> FINAL-NORTH -> destination
///
/// Thirteen further routes leave the same western neighbourhood and end at a
/// midtown terminal. Each of them makes a perfectly plausible ONE-transfer
/// journey (ride out, then a long tricycle from midtown), so they crowd the
/// candidate pool with shallow alternatives -- and, because they sit earlier
/// in the route list, their interchange edges are enumerated before the link
/// route's. That is the ordinary production situation: which of a route's
/// sixty-odd interchange edges happens to come first is an accident of the
/// database, not a statement about usefulness.
/// </summary>
internal static class TransferChainTopologyFixture
{
    public static readonly (double Latitude, double Longitude) Origin =
        (15.0500, 120.4980);

    public static readonly (double Latitude, double Longitude) Destination =
        (15.1100, 120.5900);

    public const string LocalWest = "R01-LOCAL-WEST";
    public const string LinkNortheast = "R15-LINK-NORTHEAST";
    public const string FinalNorth = "R16-FINAL-NORTH";

    public const string MidtownToda = "TODA-MIDTOWN";
    public const string OriginToda = "TODA-ORIGIN";

    /// <summary>
    /// Route order deliberately puts the two routes the useful chain needs at
    /// the very end of the list, which is where the interchange graph then
    /// files their edges. Pass <paramref name="chainRoutesFirst"/> to put them
    /// at the front instead: the planner's answer must not depend on it.
    /// </summary>
    public static List<TransportRoute> BuildRoutes(bool chainRoutesFirst = false)
    {
        var routes = new List<TransportRoute>
        {
            // The origin's local route: straight east along the barangay road.
            Route(1, LocalWest, "Local west corridor",
            [
                (15.0500, 120.4950),
                (15.0500, 120.5400)
            ])
        };

        // Thirteen decoys. Each starts beside the local corridor, crosses it,
        // and runs to the midtown terminal, so each produces real interchange
        // edges with the local route and a real one-transfer journey.
        for (var index = 0; index < 13; index++)
        {
            var startLongitude = 120.4990 + index * 0.0022;
            routes.Add(Route(
                index + 2,
                $"R{index + 2:D2}-DECOY",
                $"Decoy corridor {index + 2}",
                [
                    (15.0460, startLongitude),
                    (15.0500, startLongitude + 0.0010),
                    (15.0560, startLongitude + 0.0060),
                    (15.0600, 120.5250)
                ]));
        }

        routes.Add(Route(14 + 1, LinkNortheast, "Link north-east corridor",
        [
            (15.0527, 120.5395),
            (15.0700, 120.5600),
            (15.0890, 120.5765)
        ]));

        routes.Add(Route(16, FinalNorth, "Final north corridor",
        [
            (15.0908, 120.5752),
            (15.1120, 120.5930)
        ]));

        if (!chainRoutesFirst)
            return routes;

        // Same network, chain routes first. Interchange edges are filed by
        // route order, so this is the only thing that changes.
        var chain = routes.Where(route =>
            route.RouteCode is LocalWest or LinkNortheast or FinalNorth).ToList();
        var rest = routes.Except(chain).ToList();
        return [.. chain, .. rest];
    }

    /// <summary>
    /// Twenty-five terminals. The midtown one is what makes every decoy a
    /// usable one-transfer journey; the rest are ordinary neighbourhood
    /// terminals scattered through the western half of the network.
    /// </summary>
    public static List<TricyclePoint> BuildTrikePoints()
    {
        var points = new List<TricyclePoint>
        {
            Toda(1, OriginToda, 15.0495, 120.4975),
            Toda(2, MidtownToda, 15.0605, 120.5255)
        };

        for (var index = 0; index < 23; index++)
        {
            points.Add(Toda(
                index + 3,
                $"TODA-{index + 3:D2}",
                15.0450 + index % 5 * 0.0030,
                120.4990 + index % 8 * 0.0040));
        }

        return points;
    }

    /// <summary>
    /// The shipped routing configuration. Nothing here is tuned for the test:
    /// these are the production defaults that produce the candidate budget
    /// under investigation.
    /// </summary>
    public static RoutingOptions DefaultOptions() => new()
    {
        MaxTransfers = 2,
        MaxCandidatesToConfirm = 100,
        MaxInterchangesPerRoutePair = 4,
        MaxTransferWalkMeters = 400,
        DefaultSampleIntervalMeters = 150,
        MaxRouteSamples = 40,
        MaxWalkAccessDistanceMeters = 1_500,
        MaxWalkToTrikePointMeters = 1_000,
        MaxNearbyTrikeCandidates = 3,
        MaxTotalWalkingMetersPerJourney = 2_500,
        MaxWalkOnlyTripDistanceMeters = 2_000,
        MaxWalkTrikeTripDistanceMeters = 5_000,
        MaxStaticRouteSegmentJumpMeters = 15_000,
        MaxTripOptions = 10
    };

    public static RoutingService CreateService(
        RoutingOptions? options = null,
        bool chainRoutesFirst = false)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildRoutes(chainRoutesFirst));

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildTrikePoints());

        return new RoutingService(
            new RoadNetworkValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options ?? DefaultOptions()));
    }

    private static TransportRoute Route(
        int routeId,
        string routeCode,
        string routeName,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints) =>
        ProductionTopologyFixture.BuildDenseRoute(routeId, routeCode, routeName, waypoints);

    private static TricyclePoint Toda(
        int id,
        string code,
        double latitude,
        double longitude) =>
        ProductionTopologyFixture.BuildToda(id, code, latitude, longitude);
}
