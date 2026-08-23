using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class NavigationPhase2OptimizationTests
{
    [Fact]
    public void RoutingOptions_RejectsInvalidPhase2Tuning()
    {
        Assert.False(new RoutingOptions
        {
            BoardingDiversityBucketMeters = 0
        }.IsValid(out _));

        Assert.False(new RoutingOptions
        {
            JourneyLegContinuityToleranceMeters = 0
        }.IsValid(out _));
    }

    [Fact]
    public async Task PlanTripsAsync_ReturnedTransferPlanHasContinuousPhysicalLegs()
    {
        var service = CreateTransferService();

        var plans = await service.PlanTripsAsync(
            15.0000,
            120.5000,
            15.0100,
            120.5100);

        var plan = Assert.Single(plans.Where(candidate => candidate.TransferCount == 1));
        Assert.True(plan.Legs.Count >= 2);

        for (var index = 0; index < plan.Legs.Count - 1; index++)
        {
            var current = plan.Legs[index];
            var next = plan.Legs[index + 1];
            var gap = DistanceMeters(
                current.DestinationLatitude,
                current.DestinationLongitude,
                next.OriginLatitude,
                next.OriginLongitude);

            Assert.True(
                gap <= 25,
                $"Leg {index} -> {index + 1} has a {gap:F1}m continuity gap.");
        }
    }

    [Fact]
    public async Task GeometryEnrichment_AnchorsEveryShapeToItsPhysicalLegEndpoints()
    {
        var service = CreateTransferService();
        var plans = await service.PlanTripsAsync(
            15.0000,
            120.5000,
            15.0100,
            120.5100);
        var plan = Assert.Single(plans.Where(candidate => candidate.TransferCount == 1));

        await service.EnrichSelectedPlanGeometryAsync([plan]);

        foreach (var leg in plan.Legs)
        {
            Assert.True(leg.Geometry.Count >= 2);
            Assert.Equal(leg.OriginLatitude, leg.Geometry[0].Latitude, 7);
            Assert.Equal(leg.OriginLongitude, leg.Geometry[0].Longitude, 7);
            Assert.Equal(leg.DestinationLatitude, leg.Geometry[^1].Latitude, 7);
            Assert.Equal(leg.DestinationLongitude, leg.Geometry[^1].Longitude, 7);
        }
    }

    private static RoutingService CreateTransferService()
    {
        var routes = new List<TransportRoute>
        {
            BuildRoute(
                1,
                "A",
                "West-East",
                [(15.0000, 120.5000), (15.0000, 120.5100)]),
            BuildRoute(
                2,
                "B",
                "South-North",
                [(15.0000, 120.5100), (15.0100, 120.5100)])
        };

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 100,
            MaxRouteSamples = 50,
            MaxTransfers = 1,
            MaxInterchangesPerRoutePair = 4,
            MaxTransferWalkMeters = 100,
            MaxWalkAccessDistanceMeters = 50,
            MaxTotalWalkingMetersPerJourney = 500,
            MaxWalkOnlyTripDistanceMeters = 20,
            MaxWalkTrikeTripDistanceMeters = 20,
            MaxCandidatesToConfirm = 50,
            MaxTripOptions = 10,
            BoardingDiversityBucketMeters = 250,
            JourneyLegContinuityToleranceMeters = 25
        };

        return new RoutingService(
            new StraightLineValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private static TransportRoute BuildRoute(
        int id,
        string code,
        string name,
        IReadOnlyList<(double Latitude, double Longitude)> coordinates) =>
        new()
        {
            RouteId = id,
            RouteCode = code,
            RouteName = name,
            OriginName = "Origin",
            DestinationName = "Destination",
            IsActive = true,
            TransportMode = new TransportMode
            {
                Code = "JEEPNEY",
                Name = "Jeepney"
            },
            RoutePoints = coordinates
                .Select((coordinate, index) => new RoutePoint
                {
                    RouteId = id,
                    PointOrder = index,
                    Latitude = coordinate.Latitude,
                    Longitude = coordinate.Longitude
                })
                .ToList()
        };

    private static double DistanceMeters(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude) =>
        Math.Sqrt(
            Math.Pow((fromLatitude - toLatitude) * 111_000, 2) +
            Math.Pow((fromLongitude - toLongitude) * 111_000, 2));

    private sealed class StraightLineValhallaService : IValhallaService
    {
        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
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
                    var distance = DistanceMeters(
                        source.Lat,
                        source.Lon,
                        target.Lat,
                        target.Lon);
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