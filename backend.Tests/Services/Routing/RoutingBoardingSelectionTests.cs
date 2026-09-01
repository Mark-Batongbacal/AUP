using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class RoutingBoardingSelectionTests
{
    [Fact]
    public async Task FindConnectingRoutesAsync_BoardsNearOriginOnSameRoute()
    {
        var route = new StaticJeepneyRoute
        {
            RouteId = "L",
            RouteName = "L route",
            Coordinates =
            [
                [120.5000, 15.0000],
                [120.5000, 15.0200],
                [120.5200, 15.0200]
            ]
        };
        var service = CreateService(
            route,
            new FakeValhallaService((source, target, _) => DistanceMeters(source, target)),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 600,
                MaxRouteSamples = 6,
                MaxWalkAccessDistanceMeters = 500,
                MaxCandidatesToConfirm = 20,
                MaxTripOptions = 5
            });

        var options = await service.FindConnectingRoutesAsync(
            15.0001, 120.5001,
            15.0200, 120.5195);

        var option = Assert.Single(options);
        Assert.True(option.BoardAccess.WalkDistanceMeters < 100,
            $"Expected a nearby boarding point, got {option.BoardAccess.WalkDistanceMeters:0}m.");
        Assert.True(DistanceMeters(
            new ValhallaLocation { Lat = 15.0001, Lon = 120.5001 },
            new ValhallaLocation { Lat = option.BoardLatitude, Lon = option.BoardLongitude }) < 100);
    }

    [Fact]
    public async Task FindConnectingRoutesAsync_UsesNetworkAccessBeforeBoardingVariantCap()
    {
        var route = new StaticJeepneyRoute
        {
            RouteId = "L",
            RouteName = "L route",
            Coordinates =
            [
                [120.5000, 15.0000],
                [120.5040, 15.0000],
                [120.5100, 15.0000]
            ]
        };
        var origin = new ValhallaLocation { Lat = 15.0009, Lon = 120.5000 };
        var valhalla = new FakeValhallaService((source, target, costing) =>
        {
            if (!string.Equals(costing, "pedestrian", StringComparison.Ordinal) ||
                DistanceMeters(source, origin) > 1)
            {
                return DistanceMeters(source, target);
            }

            // A is geometrically closest but requires a 500m network walk.
            // A middle distractor also looks closer than B, so a two-candidate
            // geometric quota keeps A + distractor and loses B. The physically
            // distinct B corridor has the direct 220m pedestrian route.
            if (target.Lon < 120.5010)
                return 500;
            if (target.Lon < 120.5030)
                return 700;
            return target.Lon < 120.5050 ? 220 : 1_000;
        });
        var service = CreateService(
            route,
            valhalla,
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 100,
                MaxRouteSamples = 20,
                MaxBoardingVariantsPerRoute = 2,
                MaxWalkAccessDistanceMeters = 600,
                MaxCandidatesToConfirm = 30,
                MaxTripOptions = 5
            });

        var options = await service.FindConnectingRoutesAsync(
            origin.Lat,
            origin.Lon,
            15.0000,
            120.5095);

        var option = Assert.Single(options);
        Assert.True(
            option.BoardLongitude is >= 120.5030 and < 120.5050,
            $"Expected the network-accessible B region, got " +
            $"{option.BoardLatitude:F6},{option.BoardLongitude:F6}.");
        Assert.Equal(15.0000, option.BoardLatitude, 6);
        Assert.True(valhalla.MaxPedestrianTargetCount > 1);
    }

    [Fact]
    public async Task FindConnectingRoutesAsync_RejectsConfirmedWalkBeyondTransitAccessLimit()
    {
        var route = new StaticJeepneyRoute
        {
            RouteId = "L",
            RouteName = "L route",
            Coordinates =
            [
                [120.5000, 15.0000],
                [120.5000, 15.0100],
                [120.5100, 15.0100]
            ]
        };
        var service = CreateService(
            route,
            new FakeValhallaService((source, target, costing) =>
                costing == "pedestrian" ? 2_200 : DistanceMeters(source, target)),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 250,
                MaxRouteSamples = 12,
                MaxWalkAccessDistanceMeters = 1_800,
                MaxCandidatesToConfirm = 30
            });

        var options = await service.FindConnectingRoutesAsync(
            15.0001, 120.5001,
            15.0100, 120.5095);

        Assert.Empty(options);
    }

    [Fact]
    public void RoutingPlanSafety_RejectsOverlongWalkOnlyWhenItAccessesJeepney()
    {
        var overlongAccess = new JeepneyAccessSegment
        {
            Mode = AccessMode.Walk,
            WalkDistanceMeters = 2_200,
            WalkTimeSeconds = 1_800,
            TotalTimeSeconds = 1_800,
            GeneralizedCostPesos = 90
        };
        var emptyAccess = new JeepneyAccessSegment
        {
            Mode = AccessMode.Walk
        };

        var transit = new JeepneyTripPlan
        {
            OriginAccess = overlongAccess,
            DestinationAccess = emptyAccess,
            Legs =
            [
                new JeepneyTripLeg
                {
                    Mode = AccessMode.Jeepney,
                    RouteId = "L",
                    DistanceMeters = 1_000
                }
            ]
        };
        var directWalk = new JeepneyTripPlan
        {
            OriginAccess = overlongAccess,
            DestinationAccess = emptyAccess,
            Legs =
            [
                new JeepneyTripLeg
                {
                    Mode = AccessMode.Walk,
                    DistanceMeters = 2_200
                }
            ]
        };

        Assert.False(RoutingPlanSafety.HasValidTransitAccess(transit, 1_800));
        Assert.True(RoutingPlanSafety.HasValidTransitAccess(directWalk, 1_800));
    }

    private static RoutingService CreateService(
        StaticJeepneyRoute route,
        IValhallaService valhalla,
        RoutingOptions options)
    {
        var databaseRoute = new TransportRoute
        {
            RouteId = 1,
            RouteCode = route.RouteId,
            RouteName = route.RouteName,
            OriginName = "Origin",
            DestinationName = "Destination",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = route.Coordinates.Select((coordinate, index) => new RoutePoint
            {
                RouteId = 1,
                PointOrder = index,
                Longitude = coordinate[0],
                Latitude = coordinate[1]
            }).ToList()
        };

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([databaseRoute]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new RoutingService(
            valhalla,
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

    private sealed class FakeValhallaService(
        Func<ValhallaLocation, ValhallaLocation, string, double> distance)
        : IValhallaService
    {
        public int MaxPedestrianTargetCount { get; private set; }

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
            if (string.Equals(costing, "pedestrian", StringComparison.Ordinal))
            {
                MaxPedestrianTargetCount = Math.Max(
                    MaxPedestrianTargetCount,
                    targets.Count);
            }

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) => new ValhallaMatrixResult
                {
                    FromIndex = 0,
                    ToIndex = index,
                    Distance = distance(source, target, costing) / 1_000,
                    Time = distance(source, target, costing) / 1.2
                })
                .ToList();
            return Task.FromResult(results);
        }
    }
}
