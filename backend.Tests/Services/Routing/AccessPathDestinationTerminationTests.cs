using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Road-geometry regressions for terminating an origin tricycle path at the
/// destination. Geometric proximity is necessary but not sufficient: Valhalla
/// must also prove that legal routing to the destination is the path prefix,
/// rather than a separate detour around a barrier or one-way restriction.
/// </summary>
public sealed class AccessPathDestinationTerminationTests
{
    private const string RouteId = "B-RETURN-FROM-BOARD";
    private const string TodaId = "TODA-ACCESS-PREFIX";

    private static readonly (double Latitude, double Longitude) Origin =
        (15.0010, 120.5000);
    private static readonly (double Latitude, double Longitude) Toda =
        (15.0005, 120.5000);
    private static readonly (double Latitude, double Longitude) Destination =
        (15.0005, 120.5100);
    private static readonly (double Latitude, double Longitude) Board =
        (15.0005, 120.5200);

    [Fact]
    public async Task PlanTripsAsync_TerminatesWhenConfirmedTrikePathPassesDestination()
    {
        var valhalla = new AccessPathValhallaService(
            directRouteIsPrefix: true);
        var service = CreateService(valhalla);

        var plans = await service.PlanTripsAsync(
            Origin.Latitude,
            Origin.Longitude,
            Destination.Latitude,
            Destination.Longitude);

        var accessOnly = Assert.Single(plans, plan =>
            plan.Legs.All(leg => leg.Mode != AccessMode.Jeepney));
        Assert.Equal(AccessMode.Trike, accessOnly.OriginAccess.Mode);
        var trike = Assert.Single(accessOnly.Legs,
            leg => leg.Mode == AccessMode.Trike);
        Assert.Equal(TodaId, trike.TrikePointId);
        Assert.InRange(trike.DistanceMeters, 599, 601);
        Assert.True(DistanceMeters(
                trike.DestinationLatitude,
                trike.DestinationLongitude,
                Destination.Latitude,
                Destination.Longitude) < 1);

        // The ordinary direct-mode cap is deliberately below this completed
        // walk+trike journey. It exists only because Valhalla proved the
        // destination is a legal prefix of an already accepted access path.
        Assert.True(accessOnly.Legs.Sum(leg => leg.DistanceMeters) >
                    AccessOptions().MaxWalkTrikeTripDistanceMeters);
        Assert.DoesNotContain(plans, plan =>
            plan.Legs.Any(leg => leg.Mode == AccessMode.Jeepney));
        Assert.True(valhalla.BoardRouteRequests > 0);
        Assert.True(valhalla.DirectRouteRequests > 0);
    }

    [Fact]
    public async Task PlanTripsAsync_KeepsTransitWhenLegalTerminationRequiresDetour()
    {
        var valhalla = new AccessPathValhallaService(
            directRouteIsPrefix: false);
        var service = CreateService(valhalla);

        var plans = await service.PlanTripsAsync(
            Origin.Latitude,
            Origin.Longitude,
            Destination.Latitude,
            Destination.Longitude);

        Assert.NotEmpty(plans);
        Assert.DoesNotContain(plans, plan =>
            plan.Legs.All(leg => leg.Mode != AccessMode.Jeepney));
        Assert.Contains(plans, plan =>
            plan.Legs.Any(leg =>
                leg.Mode == AccessMode.Jeepney && leg.RouteId == RouteId));
        Assert.True(valhalla.BoardRouteRequests > 0);
        Assert.True(valhalla.DirectRouteRequests > 0);
    }

    private static RoutingService CreateService(IValhallaService valhalla)
    {
        var route = ProductionTopologyFixture.BuildDenseRoute(
            1,
            RouteId,
            RouteId,
            [
                Board,
                Destination,
                (15.0005, 120.5050)
            ]);
        var toda = ProductionTopologyFixture.BuildToda(
            1, TodaId, Toda.Latitude, Toda.Longitude);
        return ProductionTopologyFixture.CreateService(
            AccessOptions(),
            valhalla,
            [route],
            [toda]);
    }

    private static RoutingOptions AccessOptions() => new()
    {
        DefaultSampleIntervalMeters = 100,
        MaxRouteSamples = 40,
        MaxTransfers = 0,
        MaxTripOptions = 10,
        MaxCandidatesToConfirm = 100,
        MaxBoardingVariantsPerRoute = 6,
        MaxInterchangesPerRoutePair = 2,
        MaxTransferWalkMeters = 100,
        MaxWalkAccessDistanceMeters = 100,
        MaxWalkToTrikePointMeters = 100,
        MaxNearbyTrikeCandidates = 1,
        MaxTotalWalkingMetersPerJourney = 1_000,
        MaxWalkOnlyTripDistanceMeters = 500,
        MaxWalkTrikeTripDistanceMeters = 500,
        MaxStaticRouteSegmentJumpMeters = 5_000,
        BoardingDiversityBucketMeters = 250
    };

    private static double DistanceMeters(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude) =>
        ProductionTopologyFixture.Haversine(
            (fromLatitude, fromLongitude),
            (toLatitude, toLongitude));

    private sealed class AccessPathValhallaService(bool directRouteIsPrefix)
        : IValhallaService
    {
        public int BoardRouteRequests { get; private set; }
        public int DirectRouteRequests { get; private set; }

        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var start = (Latitude: startLatitude, Longitude: startLongitude);
            var end = (Latitude: endLatitude, Longitude: endLongitude);
            var isOriginToda = DistanceMeters(
                start.Latitude, start.Longitude,
                Toda.Latitude, Toda.Longitude) < 5;
            var isDestination = DistanceMeters(
                end.Latitude, end.Longitude,
                Destination.Latitude, Destination.Longitude) < 5;

            if (isOriginToda && isDestination)
            {
                DirectRouteRequests++;
                // The counterexample must be worse than every legal prefix of
                // the loop-shaped access path. A shorter 900m route is itself
                // a valid completion for some retained boards and therefore
                // cannot represent an inaccessible destination.
                var directMeters = directRouteIsPrefix ? 600.0 : 1_800.0;
                return Task.FromResult(Response(
                    directMeters,
                    directRouteIsPrefix ? 60 : 180,
                    [start, Destination]));
            }

            if (isOriginToda)
            {
                BoardRouteRequests++;
                var progressFraction = Math.Clamp(
                    (end.Longitude - Toda.Longitude) /
                    (Board.Longitude - Toda.Longitude),
                    0.01,
                    1.0);
                var boardMeters = 1_200 * progressFraction;
                var nearDestination = directRouteIsPrefix
                    ? Destination
                    : (Destination.Latitude + 0.00005, Destination.Longitude);
                return Task.FromResult(Response(
                    boardMeters,
                    Math.Max(1, boardMeters / 10),
                    [start, nearDestination, end]));
            }

            var fallbackMeters = DistanceMeters(
                start.Latitude, start.Longitude,
                end.Latitude, end.Longitude) * 1.1;
            return Task.FromResult(Response(
                fallbackMeters,
                Math.Max(1, fallbackMeters / 8),
                [start, end]));
        }

        public Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
            ValhallaLocation source,
            IReadOnlyList<ValhallaLocation> targets,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pedestrian = string.Equals(
                costing, "pedestrian", StringComparison.OrdinalIgnoreCase);
            var sourceIsToda = DistanceMeters(
                source.Lat, source.Lon,
                Toda.Latitude, Toda.Longitude) < 5;

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    double meters;
                    double seconds;
                    if (!pedestrian && sourceIsToda &&
                        target.Lon >= Destination.Longitude)
                    {
                        var fraction = Math.Clamp(
                            (target.Lon - Toda.Longitude) /
                            (Board.Longitude - Toda.Longitude),
                            0.5,
                            1.0);
                        meters = 1_200 * fraction;
                        seconds = meters / 10;
                    }
                    else
                    {
                        meters = DistanceMeters(
                            source.Lat, source.Lon,
                            target.Lat, target.Lon) *
                            (pedestrian ? 1.1 : 1.2);
                        seconds = Math.Max(1, meters / (pedestrian ? 1.2 : 8));
                    }

                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = meters / 1_000,
                        Time = seconds
                    };
                })
                .ToList();
            return Task.FromResult(results);
        }

        private static ValhallaRouteResponse Response(
            double distanceMeters,
            double timeSeconds,
            IReadOnlyList<(double Latitude, double Longitude)> points) =>
            new()
            {
                Trip = new ValhallaTrip
                {
                    Summary = new ValhallaSummary
                    {
                        Length = distanceMeters / 1_000,
                        Time = timeSeconds
                    },
                    Legs =
                    [
                        new ValhallaLeg
                        {
                            Points = points
                                .Select(point => new[]
                                {
                                    point.Longitude,
                                    point.Latitude
                                })
                                .ToList()
                        }
                    ]
                }
            };
    }
}
