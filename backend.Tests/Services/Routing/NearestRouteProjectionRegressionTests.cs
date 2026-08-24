using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class NearestRouteProjectionRegressionTests
{
    [Fact]
    public async Task FindNearbyRoutesAsync_UsesInteriorFullGeometryProjection_WhenSamplesAreOnlyEndpoints()
    {
        var service = CreateService(
            maxRouteSamples: 2,
            maxWalkAccessDistanceMeters: 1_500,
            includeOriginTrike: false);

        var nearby = await service.FindNearbyRoutesAsync(
            latitude: 15.0010,
            longitude: 120.5500);

        var route = Assert.Single(nearby);

        Assert.InRange(route.NearestPointLongitude, 120.5490, 120.5510);
        Assert.InRange(route.NearestPointLatitude, 14.9999, 15.0001);
        Assert.True(
            route.NearestPointLongitude is > 120.5010 and < 120.5990,
            $"Expected an interior nearest point, got endpoint-like longitude {route.NearestPointLongitude:F6}.");
    }

    [Fact]
    public async Task PlanTripsAsync_ExactNearestBoardCanUseTrike_WhenWalkingCapWouldRejectIt()
    {
        var service = CreateService(
            maxRouteSamples: 2,
            maxWalkAccessDistanceMeters: 100,
            includeOriginTrike: true);

        var plans = await service.PlanTripsAsync(
            originLatitude: 15.0100,
            originLongitude: 120.5500,
            destinationLatitude: 15.0000,
            destinationLongitude: 120.5900);

        Assert.NotEmpty(plans);

        var nearestBoardPlan = plans
            .Where(plan => plan.Legs.Any(leg => leg.Mode == AccessMode.Jeepney))
            .OrderBy(plan => Math.Abs(
                plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney).BoardLongitude - 120.5500))
            .First();
        var firstJeepney = nearestBoardPlan.Legs
            .First(leg => leg.Mode == AccessMode.Jeepney);

        Assert.Equal(AccessMode.Trike, nearestBoardPlan.OriginAccess.Mode);
        Assert.InRange(firstJeepney.BoardLongitude, 120.5490, 120.5510);
        Assert.True(
            firstJeepney.BoardLongitude is > 120.5010 and < 120.5990,
            $"Expected exact interior trike-to-jeepney boarding, got endpoint-like longitude {firstJeepney.BoardLongitude:F6}.");
    }

    private static RoutingService CreateService(
        int maxRouteSamples,
        double maxWalkAccessDistanceMeters,
        bool includeOriginTrike)
    {
        var route = new TransportRoute
        {
            RouteId = 1,
            RouteCode = "SPARSE",
            RouteName = "Sparse straight route",
            OriginName = "West",
            DestinationName = "East",
            IsActive = true,
            TransportMode = new TransportMode
            {
                Code = "JEEPNEY",
                Name = "Jeepney"
            },
            RoutePoints =
            [
                new RoutePoint
                {
                    RouteId = 1,
                    PointOrder = 0,
                    Latitude = 15.0000,
                    Longitude = 120.5000
                },
                new RoutePoint
                {
                    RouteId = 1,
                    PointOrder = 1,
                    Latitude = 15.0000,
                    Longitude = 120.6000
                }
            ]
        };

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([route]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(includeOriginTrike
                ?
                [
                    new TricyclePoint
                    {
                        TricyclePointId = 1,
                        PointCode = "ORIGIN-TODA",
                        PointName = "Origin TODA",
                        CenterLatitude = 15.0100,
                        CenterLongitude = 120.5500,
                        IsActive = true
                    }
                ]
                : []);

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 100,
            MaxRouteSamples = maxRouteSamples,
            MaxNearbyRoutes = 10,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 100,
            MaxTransfers = 0,
            MaxWalkAccessDistanceMeters = maxWalkAccessDistanceMeters,
            MaxWalkToTrikePointMeters = 100,
            MaxTotalWalkingMetersPerJourney = 2_500,
            MaxWalkOnlyTripDistanceMeters = 50,
            MaxWalkTrikeTripDistanceMeters = 50,
            // The fixture is deliberately a single ~10.7km segment so that
            // capping MaxRouteSamples at 2 leaves nothing but the endpoints.
            // Without this the segment trips the malformed-route guard and the
            // route is dropped before any of the behaviour under test runs.
            MaxStaticRouteSegmentJumpMeters = 15_000
        };

        return new RoutingService(
            new EuclideanValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private sealed class EuclideanValhallaService : IValhallaService
    {
        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            var distance = DistanceMeters(
                new ValhallaLocation { Lat = startLatitude, Lon = startLongitude },
                new ValhallaLocation { Lat = endLatitude, Lon = endLongitude });

            return Task.FromResult(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Summary = new ValhallaSummary
                    {
                        Length = distance / 1_000,
                        Time = Math.Max(1, distance / Speed(costing))
                    },
                    Legs =
                    [
                        new ValhallaLeg
                        {
                            Points =
                            [
                                [startLongitude, startLatitude],
                                [endLongitude, endLatitude]
                            ]
                        }
                    ]
                }
            });
        }

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
                    var distance = DistanceMeters(source, target);
                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = distance / 1_000,
                        Time = Math.Max(1, distance / Speed(costing))
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }

        private static double Speed(string costing) =>
            string.Equals(costing, "pedestrian", StringComparison.OrdinalIgnoreCase)
                ? 1.2
                : 5.6;

        private static double DistanceMeters(
            ValhallaLocation source,
            ValhallaLocation target) =>
            Math.Sqrt(
                Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
                Math.Pow((source.Lon - target.Lon) * 111_000, 2));
    }
}
