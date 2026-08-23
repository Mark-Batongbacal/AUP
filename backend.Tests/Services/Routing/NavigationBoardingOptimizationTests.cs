using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class NavigationBoardingOptimizationTests
{
    [Fact]
    public void RoutingOptions_RejectsInvalidFeederShadowingRatio()
    {
        var options = new RoutingOptions
        {
            FeederShadowingAccessDistanceRatio = 1.01
        };

        Assert.False(options.IsValid(out _));
    }

    [Fact]
    public async Task PlanTripsAsync_DoesNotChaseSameJeepneyDownstreamForTinyGain()
    {
        // Candidate generation thinks the tricycle is very fast, deliberately
        // making a downstream board look attractive. Valhalla confirmation is
        // slower and must be authoritative before the final choice is made.
        var service = CreateStraightRouteService(
            confirmedTrikeSpeedMetersPerSecond: 5.6,
            provisionalTrikeSpeedMetersPerSecond: 30);

        var plans = await service.PlanTripsAsync(
            OriginLatitude,
            OriginLongitude,
            DestinationLatitude,
            DestinationLongitude);

        var efficient = Assert.Single(plans.Where(plan =>
            plan.RecommendationType.Split(',').Contains("efficient")));
        var firstJeepney = efficient.Legs.First(leg => leg.Mode == AccessMode.Jeepney);

        Assert.Equal(AccessMode.Trike, efficient.OriginAccess.Mode);
        Assert.True(
            firstJeepney.BoardLongitude < 120.5060,
            $"Expected a nearby board, got longitude {firstJeepney.BoardLongitude:F6}.");
    }

    [Fact]
    public async Task PlanTripsAsync_PreservesFartherBoardWhenFastestGainIsSubstantial()
    {
        var service = CreateStraightRouteService(
            confirmedTrikeSpeedMetersPerSecond: 30,
            provisionalTrikeSpeedMetersPerSecond: 30);

        var plans = await service.PlanTripsAsync(
            OriginLatitude,
            OriginLongitude,
            DestinationLatitude,
            DestinationLongitude);

        var fastest = Assert.Single(plans.Where(plan =>
            plan.RecommendationType.Split(',').Contains("fastest")));
        var firstJeepney = fastest.Legs.First(leg => leg.Mode == AccessMode.Jeepney);

        Assert.Equal(AccessMode.Trike, fastest.OriginAccess.Mode);
        Assert.True(
            firstJeepney.BoardLongitude > 120.5300,
            $"Expected fastest to retain a genuinely useful downstream board, got {firstJeepney.BoardLongitude:F6}.");
    }

    private const double OriginLatitude = 15.0018;
    private const double OriginLongitude = 120.5000;
    private const double DestinationLatitude = 15.0000;
    private const double DestinationLongitude = 120.5500;

    private static RoutingService CreateStraightRouteService(
        double confirmedTrikeSpeedMetersPerSecond,
        double provisionalTrikeSpeedMetersPerSecond)
    {
        var route = new TransportRoute
        {
            RouteId = 1,
            RouteCode = "STRAIGHT",
            RouteName = "Straight test jeepney",
            OriginName = "Start",
            DestinationName = "End",
            IsActive = true,
            TransportMode = new TransportMode
            {
                Code = "JEEPNEY",
                Name = "Jeepney"
            },
            RoutePoints = Enumerable.Range(0, 11)
                .Select(index => new RoutePoint
                {
                    RouteId = 1,
                    PointOrder = index,
                    Latitude = 15.0000,
                    Longitude = 120.5000 + index * 0.005
                })
                .ToList()
        };

        var terminal = new TricyclePoint
        {
            TricyclePointId = 1,
            PointCode = "ORIGIN-TODA",
            PointName = "Origin TODA",
            CenterLatitude = OriginLatitude,
            CenterLongitude = OriginLongitude,
            IsActive = true
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
            .ReturnsAsync([terminal]);

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 100,
            MaxRouteSamples = 80,
            MaxTransfers = 0,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 100,
            MaxWalkAccessDistanceMeters = 100,
            MaxWalkToTrikePointMeters = 50,
            MaxTotalWalkingMetersPerJourney = 1_000,
            MaxWalkOnlyTripDistanceMeters = 50,
            MaxWalkTrikeTripDistanceMeters = 50,
            TrikeSpeedMetersPerSecond = provisionalTrikeSpeedMetersPerSecond,
            FeederShadowingMinProgressMeters = 300,
            FeederShadowingAccessDistanceRatio = 0.60,
            FeederShadowingRequiredTimeSavingsSeconds = 120,
            FeederShadowingRequiredFareSavingsPesos = 10
        };

        return new RoutingService(
            new CostingAwareValhallaService(confirmedTrikeSpeedMetersPerSecond),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private static double DistanceMeters(
        ValhallaLocation source,
        ValhallaLocation target) =>
        Math.Sqrt(
            Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
            Math.Pow((source.Lon - target.Lon) * 111_000, 2));

    private sealed class CostingAwareValhallaService(
        double trikeSpeedMetersPerSecond) : IValhallaService
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
            var speed = string.Equals(costing, "pedestrian", StringComparison.OrdinalIgnoreCase)
                ? 1.2
                : trikeSpeedMetersPerSecond;

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    var distance = DistanceMeters(source, target);
                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = distance / 1_000,
                        Time = Math.Max(1, distance / speed)
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }
}
