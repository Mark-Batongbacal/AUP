using backend.Models.Valhalla;
using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace backend.Tests.Services.Routing;

public sealed class RoutingServiceTests
{
    [Fact]
    public void RoutingOptions_RejectsInvalidSafetyLimits()
    {
        var options = new RoutingOptions
        {
            MaxTripOptions = 0,
            WalkingSpeedMetersPerSecond = 0
        };

        Assert.False(options.IsValid(out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RoutingOptions_RejectsNegativeWalkingFatigue()
    {
        var options = new RoutingOptions
        {
            WalkingFatiguePesosPerKilometer = -0.01
        };

        Assert.False(options.IsValid(out _));
    }

    [Fact]
    public void Scoring_UsesConfiguredValueOfTimeAndWalkingFatigue()
    {
        var configured = CreateService(
            new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
            options: new RoutingOptions
            {
                ValueOfTimePesosPerMinute = 3,
                WalkingFatiguePesosPerKilometer = 4
            });
        var highValueOfTime = CreateService(
            new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
            options: new RoutingOptions
            {
                ValueOfTimePesosPerMinute = 10,
                WalkingFatiguePesosPerKilometer = 4
            });
        var noFatigue = CreateService(
            new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
            options: new RoutingOptions
            {
                ValueOfTimePesosPerMinute = 3,
                WalkingFatiguePesosPerKilometer = 0
            });

        Assert.Equal(143.6, configured.GeneralizedCostFromTimeAndFare(972, 95), 6);
        Assert.Equal(257, highValueOfTime.GeneralizedCostFromTimeAndFare(972, 95), 6);
        Assert.Equal(40.092, configured.GeneralizedCostFromWalking(726, 948), 6);
        Assert.Equal(36.3, noFatigue.GeneralizedCostFromWalking(726, 948), 6);
    }

    [Fact]
    public async Task PlanTripsAsync_FinalTotalsAlwaysMatchPhysicalLegs()
    {
        var service = CreateService(new FakeValhallaService((source, target, _) =>
            DistanceMeters(source, target)));

        var plans = await service.PlanTripsAsync(
            15.109698583445889,
            120.58240903543013,
            15.110100,
            120.582900);

        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            Assert.Equal(plan.Legs.Sum(leg => leg.DurationSeconds), plan.TotalTimeSeconds, 6);
            Assert.Equal(plan.Legs.Sum(leg => leg.FarePesos), plan.TotalFarePesos, 6);
            Assert.Equal(plan.Legs.Sum(leg => leg.GeneralizedCostPesos), plan.GeneralizedCostPesos, 6);
            Assert.DoesNotContain(plan.Legs, leg => leg.DistanceMeters <= 0);
            Assert.All(
                plan.Legs.Where(leg => leg.Mode == backend.Models.Routing.AccessMode.Walk),
                leg => Assert.True(
                    leg.GeneralizedCostPesos > leg.DurationSeconds / 60.0 * 3));
        }
    }

    [Fact]
    public async Task PlanTripsAsync_PreservesDistinctObjectiveAndAccessModePlans()
    {
        var service = CreateService(new FakeValhallaService((source, target, _) =>
            DistanceMeters(source, target)));

        var plans = await service.PlanTripsAsync(
            15.109698583445889,
            120.58240903543013,
            15.139582098206548,
            120.60108373338038);

        Assert.Contains(plans, plan =>
            plan.RecommendationType.Split(',').Contains("efficient"));
        Assert.Contains(plans, plan =>
            plan.RecommendationType.Split(',').Contains("cheapest"));
        Assert.Contains(plans, plan =>
            plan.RecommendationType.Split(',').Contains("fastest"));
        Assert.Contains(plans, plan => plan.OriginAccess.Mode ==
            backend.Models.Routing.AccessMode.Trike);
        Assert.Contains(plans, plan => plan.OriginAccess.Mode ==
            backend.Models.Routing.AccessMode.Walk);
    }

    [Fact]
    public async Task PlanTripsAsync_GraphSearchFindsLegitimateThreeTransferJourney()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tuki-three-transfer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "TestData"));
        try
        {
            var routes = new List<StaticJeepneyRoute>
            {
                Route("A", (15.0000, 120.5000), (15.0000, 120.5100)),
                Route("B", (15.0000, 120.5100), (15.0100, 120.5100)),
                Route("C", (15.0100, 120.5100), (15.0100, 120.5200)),
                Route("D", (15.0100, 120.5200), (15.0200, 120.5200))
            };
            File.WriteAllText(Path.Combine(root, "TestData", "jeepney-routes.json"),
                JsonSerializer.Serialize(routes));
            var service = CreateService(
                new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
                root,
                new RoutingOptions
                {
                    MaxTransfers = 3,
                    MaxWalkAccessDistanceMeters = 150,
                    MaxTransferWalkMeters = 100,
                    MaxWalkOnlyTripDistanceMeters = 100,
                    MaxWalkTrikeTripDistanceMeters = 100,
                    MaxRouteSamples = 20,
                    DefaultSampleIntervalMeters = 100
                });
            var plans = await service.PlanTripsAsync(
                15.0000, 120.5000, 15.0200, 120.5200);
            Assert.Contains(plans, plan => plan.TransferCount == 3 &&
                plan.Legs.Count(leg => leg.Mode == AccessMode.Jeepney) == 4);
        }
        finally
        {
            Directory.Delete(root, true);
        }

        static StaticJeepneyRoute Route(string id,
            (double Lat, double Lon) from, (double Lat, double Lon) to) => new()
        {
            RouteId = id, RouteName = id,
            Coordinates = [[from.Lon, from.Lat], [to.Lon, to.Lat]]
        };
    }

    [Fact]
    public async Task FindConnectingRoutesAsync_IncludesJeepneyTimeAndFareInFinalCost()
    {
        var service = CreateService(new FakeValhallaService((source, target, _) =>
            DistanceMeters(source, target)));

        var options = await service.FindConnectingRoutesAsync(
            15.109698583445889,
            120.58240903543013,
            15.117165904241862,
            120.56865220184025);

        Assert.NotEmpty(options);
        foreach (var option in options)
        {
            var accessTime = option.BoardAccess.TotalTimeSeconds + option.AlightAccess.TotalTimeSeconds;
            var accessCost = option.BoardAccess.GeneralizedCostPesos + option.AlightAccess.GeneralizedCostPesos;
            Assert.True(option.TotalTimeSeconds > accessTime);
            Assert.True(option.GeneralizedCostPesos > accessCost);
            Assert.Equal(
                option.BoardAccess.TotalFarePesos + option.AlightAccess.TotalFarePesos + 13,
                option.TotalFarePesos,
                6);
        }
    }

    [Fact]
    public async Task FindConnectingRoutesAsync_UsesFullGeometryForFinalJeepneyDistance()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var testData = Path.Combine(root, "TestData");
        Directory.CreateDirectory(testData);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(testData, "jeepney-routes.json"), """
                [{"routeId":"curve","routeName":"Curve","coordinates":[[120.5000,15.0000],[120.5000,15.1000],[120.6000,15.1000]]}]
                """);

            var service = CreateService(
                new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
                root,
                new RoutingOptions
                {
                    MaxRouteSamples = 2,
                    MaxStaticRouteSegmentJumpMeters = 20_000
                });

            var options = await service.FindConnectingRoutesAsync(
                15.0000, 120.5000, 15.1000, 120.6000);

            var option = Assert.Single(options);
            // The authoritative L-shaped route is about 22km, whereas its
            // sparse start/end chord is only about 16km.
            Assert.True(option.TotalTimeSeconds > 3_600);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FindConnectingRoutesAsync_ProjectsBoardAndAlightInsideSearchRegions()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var testData = Path.Combine(root, "TestData");
        Directory.CreateDirectory(testData);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(testData, "jeepney-routes.json"), """
                [{"routeId":"curve","routeName":"Curve","coordinates":[[120.5000,15.0000],[120.5000,15.1000],[120.6000,15.1000]]}]
                """);

            var service = CreateService(
                new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
                root,
                new RoutingOptions
                {
                    MaxRouteSamples = 2,
                    MaxStaticRouteSegmentJumpMeters = 20_000
                });

            var option = Assert.Single(await service.FindConnectingRoutesAsync(
                15.0500, 120.5000,
                15.1000, 120.0500 + 0.5000));

            Assert.Equal(15.0500, option.BoardLatitude, 4);
            Assert.Equal(120.5500, option.AlightLongitude, 4);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RoutingService CreateService(
        IValhallaService valhalla,
        string? contentRootPath = null,
        RoutingOptions? options = null)
    {
        var root = contentRootPath ?? Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../backend"));
        var staticRoutes = JsonSerializer.Deserialize<List<StaticJeepneyRoute>>(
            File.ReadAllText(Path.Combine(root, "TestData", "jeepney-routes.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var routes = staticRoutes.Select((route, routeIndex) => new TransportRoute
        {
            RouteId = routeIndex + 1,
            RouteCode = route.RouteId,
            RouteName = route.RouteName,
            OriginName = "Test origin",
            DestinationName = "Test destination",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = route.Coordinates.Select((coordinate, pointIndex) =>
                new RoutePoint
                {
                    RouteId = routeIndex + 1,
                    PointOrder = pointIndex,
                    Longitude = coordinate[0],
                    Latitude = coordinate[1]
                }).ToList()
        }).ToList();

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        var tricyclePath = Path.Combine(root, "TestData", "trike-points.json");
        var tricyclePoints = File.Exists(tricyclePath)
            ? (JsonSerializer.Deserialize<List<backend.Models.Routing.TrikePoint>>(
                File.ReadAllText(tricyclePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
                .Select((point, index) => new TricyclePoint
                {
                    TricyclePointId = index + 1,
                    PointCode = point.Id,
                    PointName = point.Name,
                    CenterLatitude = point.Latitude,
                    CenterLongitude = point.Longitude,
                    IsActive = true
                }).ToList()
            : [];
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tricyclePoints);

        return new RoutingService(
            valhalla,
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options ?? new RoutingOptions()));
    }

    private static double DistanceMeters(ValhallaLocation source, ValhallaLocation target) =>
        Math.Sqrt(
            Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
            Math.Pow((source.Lon - target.Lon) * 111_000, 2));

    private sealed class FakeValhallaService(
        Func<ValhallaLocation, ValhallaLocation, string, double> distance)
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
            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) => new ValhallaMatrixResult
                {
                    FromIndex = 0,
                    ToIndex = index,
                    Distance = distance(source, target, costing) / 1_000,
                    Time = Math.Max(1, distance(source, target, costing) / 1.2)
                })
                .ToList();
            return Task.FromResult(results);
        }
    }
}
