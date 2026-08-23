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
/// Stored route-point order is the vehicle's actual traversal order, and a
/// service may deliberately retrace the same road later in that order --
/// A-B-C-D-E-F-G-H-D-C-B-A. The repeated points are not duplicates to be
/// cleaned up: they are separate boarding opportunities, and which one the
/// passenger wants depends on where they are going, not on which is
/// physically nearest.
///
/// The route below runs east from A to H, turns, and comes back west along
/// the very same road -- so every stop between A and H appears twice at
/// identical coordinates.
/// </summary>
public sealed class RetracedRouteOccurrenceTests
{
    // The service retraces the SAME road on its way back, so the outbound
    // and return passes share coordinates exactly and can only be told
    // apart by how far along the route they sit.
    private const double LaneLatitude = 15.0000;

    // Longitudes for the lettered stops. C is west of D, so on the outbound
    // lane the vehicle reaches C before D, and on the return lane after it.
    private const double LongitudeC = 120.5020;
    private const double LongitudeD = 120.5030;

    [Fact]
    public async Task PlanTripsAsync_BoardsReturnOccurrence_WhenDestinationIsBehindOnTheOutboundLane()
    {
        // The passenger is beside D and their destination C lies west.
        // Westbound is only served on the return pass, so the return
        // occurrence of D is the one to board -- boarding the outbound
        // occurrence means riding all the way to H and back.
        var service = CreateService();

        var plans = await service.PlanTripsAsync(
            15.00018, LongitudeD,   // beside D, which the vehicle passes twice
            LaneLatitude, LongitudeC);

        Assert.NotEmpty(plans);

        var best = plans.First();
        var jeepney = best.Legs.First(leg => leg.Mode == AccessMode.Jeepney);

        // The return-pass ride is ~110m; going out to H and back is well
        // over a kilometre.
        Assert.True(
            jeepney.DistanceMeters < 400,
            $"Expected the short return-pass ride, got {jeepney.DistanceMeters:F0}m " +
            "-- the planner collapsed the two occurrences of D and rode the " +
            "long way round.");
    }

    [Fact]
    public async Task PlanTripsAsync_BoardsOutboundOccurrence_WhenDestinationIsAheadOnTheOutboundLane()
    {
        // Mirror of the case above: the destination is east, so the OUTBOUND
        // occurrence is correct. This is the control -- it proves the fix is
        // not simply "always prefer the later occurrence".
        var service = CreateService();

        var plans = await service.PlanTripsAsync(
            15.00018, LongitudeC,
            LaneLatitude, LongitudeD);

        Assert.NotEmpty(plans);

        var jeepney = plans.First().Legs.First(leg => leg.Mode == AccessMode.Jeepney);

        Assert.True(
            jeepney.DistanceMeters < 400,
            $"Expected the short outbound-pass ride, got {jeepney.DistanceMeters:F0}m.");
    }

    /// <summary>
    /// Builds A-B-C-D-E-F-G-H then H-G-F-E-D-C-B-A back down the same road,
    /// as a dense polyline in traversal order.
    /// </summary>
    private static TransportRoute BuildRetracedRoute()
    {
        var points = new List<RoutePoint>();
        var order = 0;

        void Run(double latitude, double fromLongitude, double toLongitude)
        {
            const int steps = 60;
            for (var i = 0; i <= steps; i++)
            {
                var t = (double)i / steps;
                points.Add(new RoutePoint
                {
                    RouteId = 1,
                    PointOrder = order++,
                    Latitude = latitude,
                    Longitude = fromLongitude + (toLongitude - fromLongitude) * t
                });
            }
        }

        // Outbound: A (120.5000) east to H (120.5070).
        Run(LaneLatitude, 120.5000, 120.5070);
        // Return: H back west to A, retracing the same road.
        Run(LaneLatitude, 120.5070, 120.5000);

        return new TransportRoute
        {
            RouteId = 1,
            RouteCode = "RETRACED",
            RouteName = "Retraced loop service",
            OriginName = "A",
            DestinationName = "A",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = points
        };
    }

    private static RoutingService CreateService()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(r => r.GetAllActiveWithOrderedPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildRetracedRoute()]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = new RoutingOptions
        {
            // Fine sampling so both lanes are represented distinctly.
            DefaultSampleIntervalMeters = 60,
            MaxRouteSamples = 60,
            MaxTransfers = 0,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 200,
            MaxBoardingVariantsPerRoute = 12,
            MaxWalkAccessDistanceMeters = 150,
            MaxWalkOnlyTripDistanceMeters = 30,
            MaxWalkTrikeTripDistanceMeters = 30,
            MaxTotalWalkingMetersPerJourney = 500,
            MaxNearbyTrikeCandidates = 0,
            MaxStaticRouteSegmentJumpMeters = 15_000
        };

        return new RoutingService(
            new StraightLineValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private sealed class StraightLineValhallaService : IValhallaService
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

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    var distance = Math.Sqrt(
                        Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
                        Math.Pow((source.Lon - target.Lon) * 111_000, 2));

                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = distance / 1_000,
                        Time = Math.Max(1, distance / 1.2)
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }
}
