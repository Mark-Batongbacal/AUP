using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// A deterministic, production-derived multi-route topology.
///
/// The previous synthetic fixtures were a single straight corridor, which
/// cannot express the shapes that actually broke in production: a feeder
/// riding ALONGSIDE a corridor to board much farther down it, a feeder
/// swallowing an entire upstream jeepney leg, and a token jeepney hop
/// bracketed by large feeders. This fixture keeps the real characteristics:
/// three chained jeepney routes with genuine transfer areas, corridors that
/// bend rather than run straight, an off-corridor origin served by its own
/// TODA, and TODAs placed along the corridor the way real terminals are.
///
/// Nothing here touches the live database: routes, terminals and the
/// Valhalla network are all fixed values.
/// </summary>
internal static class ProductionTopologyFixture
{
    // Origin sits in a barangay roughly 1.2 km south of corridor A.
    public static readonly (double Latitude, double Longitude) Origin =
        (15.0850, 120.5560);

    // Destination sits just off corridor C near its eastern end.
    public static readonly (double Latitude, double Longitude) Destination =
        (15.1368, 120.6162);

    public const string RouteA = "A-PORAC";
    public const string RouteB = "B-HIGHWAY";
    public const string RouteC = "C-TOWN";

    public const string OriginToda = "TODA-ORIGIN";
    public const string CorridorMidToda = "TODA-CORRIDOR-MID";
    public const string CorridorLateToda = "TODA-CORRIDOR-LATE";
    public const string TransferToda = "TODA-TRANSFER";
    public const string DestinationToda = "TODA-DESTINATION";

    private static readonly (double Latitude, double Longitude)[] RouteAWaypoints =
    [
        (15.0955, 120.5450),
        (15.0968, 120.5520),
        (15.0962, 120.5590),
        (15.0975, 120.5660),
        (15.0998, 120.5730),
        (15.1020, 120.5800),
        (15.1045, 120.5870)
    ];

    private static readonly (double Latitude, double Longitude)[] RouteBWaypoints =
    [
        (15.1058, 120.5885),
        (15.1120, 120.5940),
        (15.1185, 120.5990),
        (15.1240, 120.6040)
    ];

    private static readonly (double Latitude, double Longitude)[] RouteCWaypoints =
    [
        (15.1252, 120.6055),
        (15.1310, 120.6100),
        (15.1360, 120.6150),
        (15.1410, 120.6200)
    ];

    public static List<TransportRoute> BuildRoutes() =>
    [
        BuildDenseRoute(1, RouteA, "Porac corridor", RouteAWaypoints),
        BuildDenseRoute(2, RouteB, "Highway corridor", RouteBWaypoints),
        BuildDenseRoute(3, RouteC, "Town corridor", RouteCWaypoints)
    ];

    public static List<TricyclePoint> BuildTrikePoints() =>
    [
        // Serves the origin barangay.
        BuildToda(1, OriginToda, 15.0862, 120.5566),
        // Sits beside corridor A about a third of the way along it.
        BuildToda(2, CorridorMidToda, 15.0972, 120.5665),
        // Sits beside corridor A near its eastern end.
        BuildToda(3, CorridorLateToda, 15.1042, 120.5866),
        // Sits in the B/C transfer area.
        BuildToda(4, TransferToda, 15.1245, 120.6046),
        // Serves the destination neighbourhood.
        BuildToda(5, DestinationToda, 15.1358, 120.6152)
    ];

    /// <summary>
    /// Densifies waypoints into a polyline with roughly 30 m spacing, which
    /// is how stored route points actually look.
    /// </summary>
    public static TransportRoute BuildDenseRoute(
        int routeId,
        string routeCode,
        string routeName,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints)
    {
        const double stepMeters = 30;
        var points = new List<RoutePoint>();
        var order = 0;

        void Add(double latitude, double longitude) =>
            points.Add(new RoutePoint
            {
                RouteId = routeId,
                PointOrder = order++,
                Latitude = Math.Round(latitude, 6),
                Longitude = Math.Round(longitude, 6)
            });

        Add(waypoints[0].Latitude, waypoints[0].Longitude);

        for (var index = 0; index < waypoints.Count - 1; index++)
        {
            var from = waypoints[index];
            var to = waypoints[index + 1];
            var segmentMeters = Haversine(from, to);
            var steps = Math.Max(1, (int)Math.Round(segmentMeters / stepMeters));

            for (var step = 1; step <= steps; step++)
            {
                var fraction = (double)step / steps;
                Add(
                    from.Latitude + (to.Latitude - from.Latitude) * fraction,
                    from.Longitude + (to.Longitude - from.Longitude) * fraction);
            }
        }

        return new TransportRoute
        {
            RouteId = routeId,
            RouteCode = routeCode,
            RouteName = routeName,
            OriginName = "Start",
            DestinationName = "End",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = points
        };
    }

    public static TricyclePoint BuildToda(
        int id,
        string code,
        double latitude,
        double longitude) => new()
        {
            TricyclePointId = id,
            PointCode = code,
            PointName = code,
            CenterLatitude = latitude,
            CenterLongitude = longitude,
            IsActive = true
        };

    /// <summary>
    /// Production-like routing configuration. Everything not named here keeps
    /// the shipped default so the regressions exercise real tuning.
    /// </summary>
    public static RoutingOptions DefaultOptions(
        int maxTransfers = 2,
        double maxWalkAccessDistanceMeters = 1_500,
        double maxWalkToTrikePointMeters = 1_000,
        int maxBoardingVariantsPerRoute = 5) => new()
    {
        DefaultSampleIntervalMeters = 150,
        MaxRouteSamples = 60,
        MaxTransfers = maxTransfers,
        MaxTripOptions = 10,
        MaxCandidatesToConfirm = 300,
        MaxInterchangesPerRoutePair = 4,
        MaxTransferWalkMeters = 400,
        MaxWalkAccessDistanceMeters = maxWalkAccessDistanceMeters,
        MaxWalkToTrikePointMeters = maxWalkToTrikePointMeters,
        MaxNearbyTrikeCandidates = 3,
        MaxTotalWalkingMetersPerJourney = 2_500,
        MaxWalkOnlyTripDistanceMeters = 2_000,
        MaxWalkTrikeTripDistanceMeters = 5_000,
        MaxStaticRouteSegmentJumpMeters = 15_000,
        MaxBoardingVariantsPerRoute = maxBoardingVariantsPerRoute,
        BoardingDiversityBucketMeters = 500,
        FeederShadowingMinProgressMeters = 300,
        FeederShadowingAccessDistanceRatio = 0.60
    };

    public static RoutingService CreateService(
        RoutingOptions? options = null,
        IValhallaService? valhalla = null,
        List<TransportRoute>? routes = null,
        List<TricyclePoint>? trikePoints = null,
        IValhallaResultCache? resultCache = null,
        RoutingNetworkSnapshotProvider? snapshotProvider = null)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes ?? BuildRoutes());

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(trikePoints ?? BuildTrikePoints());

        var authoritativeValhalla = valhalla ?? new RoadNetworkValhallaService();
        var routingOptions = Options.Create(options ?? DefaultOptions());
        if (resultCache is null)
        {
            return new RoutingService(
                authoritativeValhalla,
                routeRepository.Object,
                tricycleRepository.Object,
                NullLogger<RoutingService>.Instance,
                routingOptions);
        }

        return new RoutingService(
            authoritativeValhalla,
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            routingOptions,
            tripAreaValidator: null,
            telemetry: null,
            networkSnapshotProvider:
                snapshotProvider ?? new RoutingNetworkSnapshotProvider(),
            valhallaResultCache: resultCache);
    }

    public static double Haversine(
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to)
    {
        const double earthRadiusMeters = 6_371_000;
        var fromLatitude = from.Latitude * Math.PI / 180;
        var toLatitude = to.Latitude * Math.PI / 180;
        var deltaLatitude = (to.Latitude - from.Latitude) * Math.PI / 180;
        var deltaLongitude = (to.Longitude - from.Longitude) * Math.PI / 180;
        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                Math.Cos(fromLatitude) * Math.Cos(toLatitude) *
                Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

/// <summary>
/// A deterministic Valhalla stand-in that behaves like a road network rather
/// than a ruler: distances carry a detour factor over the straight line, and
/// specific areas can be made unreachable on foot. Confirmed distances are
/// therefore never equal to the planner's straight-line estimates, which is
/// exactly the condition the confirmation stage exists to handle.
/// </summary>
internal sealed class RoadNetworkValhallaService(
    double pedestrianDetourFactor = 1.15,
    double autoDetourFactor = 1.20,
    double autoSpeedMetersPerSecond = 8.0,
    Func<ValhallaLocation, ValhallaLocation, double?>? pedestrianOverride = null,
    Func<ValhallaLocation, ValhallaLocation, double?>? autoOverride = null)
    : IValhallaService
{
    public Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isPedestrian = string.Equals(
            costing, "pedestrian", StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<ValhallaMatrixResult> results = targets
            .Select((target, index) =>
            {
                var overridden = isPedestrian
                    ? pedestrianOverride?.Invoke(source, target)
                    : autoOverride?.Invoke(source, target);

                double meters;
                if (overridden is { } value)
                {
                    if (double.IsPositiveInfinity(value))
                    {
                        return new ValhallaMatrixResult
                        {
                            FromIndex = 0,
                            ToIndex = index,
                            Distance = null,
                            Time = null
                        };
                    }

                    meters = value;
                }
                else
                {
                    meters = ProductionTopologyFixture.Haversine(
                        (source.Lat, source.Lon),
                        (target.Lat, target.Lon)) *
                        (isPedestrian ? pedestrianDetourFactor : autoDetourFactor);
                }

                var speed = isPedestrian ? 1.2 : autoSpeedMetersPerSecond;

                return new ValhallaMatrixResult
                {
                    FromIndex = 0,
                    ToIndex = index,
                    Distance = meters / 1_000,
                    Time = Math.Max(1, meters / speed)
                };
            })
            .ToList();

        return Task.FromResult(results);
    }
}
