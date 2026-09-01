using System.Text.Json;
using backend.Models.Database;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// The real Tuki network, frozen into a file.
///
/// TestData/production-network.json holds every active jeepney route and
/// tricycle terminal exactly as the live service serves them: 18 routes,
/// 4,554 route points and 25 terminals. It was exported from the deployed
/// API's public read endpoints:
///
///     GET /api/transport-routes
///     GET /api/transport-routes/{routeId}/points
///     GET /api/tricycle-points
///
/// Refresh it the same way when the network changes. Nothing here reaches the
/// database or the network at test time -- the file is the whole input.
///
/// This exists because synthetic corridors kept passing while production
/// failed. Real geometry has properties a hand-built fixture does not: routes
/// that double back, corridors that run parallel for kilometres, terminals
/// clustered in a town centre, and interchange counts an order of magnitude
/// larger than anything drawn by hand.
/// </summary>
internal static class ProductionNetworkFixture
{
    /// <summary>The route that production reported the planner was avoiding.</summary>
    public const string LinkCorridor = "SM-CPOINT-HOLY-HIWAY";

    /// <summary>
    /// The Routing section from backend/appsettings.json, as deployed. Copied
    /// rather than loaded so that retuning production cannot silently change
    /// what these regressions mean.
    /// </summary>
    public static RoutingOptions DeployedOptions() => new()
    {
        MaxNearbyRoutes = 20,
        MaxTripOptions = 5,
        MaxInterchangesPerRoutePair = 3,
        MaxTransferWalkMeters = 600,
        MinimumSelfTransferProgressMeters = 1_000,
        MinimumSelfTransferRouteToWalkRatio = 3,
        MaxNearbyTrikeCandidates = 4,
        MaxWalkToTrikePointMeters = 1_200,
        MaxWalkOnlyTripDistanceMeters = 2_500,
        MaxWalkTrikeTripDistanceMeters = 8_000,
        MaxWalkAccessDistanceMeters = 1_800,
        MaxTotalWalkingMetersPerJourney = 3_000,
        MaxSupportedTripStraightLineMeters = 75_000,
        ValueOfTimePesosPerMinute = 3,
        WalkingFatiguePesosPerKilometer = 3,
        WalkingSpeedMetersPerSecond = 1.2,
        TrikeSpeedMetersPerSecond = 5.6,
        JeepneySpeedMetersPerSecond = 6.5,
        JeepneyBoardingWaitTimeSeconds = 180,
        JeepneyBaseFarePesos = 13,
        MaxCandidatesToConfirm = 150,
        MaxTransfers = 2
    };

    public static RoutingOptions DeployedOptionsWith(
        double? maxWalkToTrikePointMeters = null,
        double? maxWalkAccessDistanceMeters = null)
    {
        var baseline = DeployedOptions();
        return new RoutingOptions
        {
            MaxNearbyRoutes = baseline.MaxNearbyRoutes,
            MaxTripOptions = baseline.MaxTripOptions,
            MaxInterchangesPerRoutePair = baseline.MaxInterchangesPerRoutePair,
            MaxTransferWalkMeters = baseline.MaxTransferWalkMeters,
            MinimumSelfTransferProgressMeters = baseline.MinimumSelfTransferProgressMeters,
            MinimumSelfTransferRouteToWalkRatio = baseline.MinimumSelfTransferRouteToWalkRatio,
            MaxNearbyTrikeCandidates = baseline.MaxNearbyTrikeCandidates,
            MaxWalkToTrikePointMeters =
                maxWalkToTrikePointMeters ?? baseline.MaxWalkToTrikePointMeters,
            MaxWalkOnlyTripDistanceMeters = baseline.MaxWalkOnlyTripDistanceMeters,
            MaxWalkTrikeTripDistanceMeters = baseline.MaxWalkTrikeTripDistanceMeters,
            MaxWalkAccessDistanceMeters =
                maxWalkAccessDistanceMeters ?? baseline.MaxWalkAccessDistanceMeters,
            MaxTotalWalkingMetersPerJourney = baseline.MaxTotalWalkingMetersPerJourney,
            MaxSupportedTripStraightLineMeters = baseline.MaxSupportedTripStraightLineMeters,
            ValueOfTimePesosPerMinute = baseline.ValueOfTimePesosPerMinute,
            WalkingFatiguePesosPerKilometer = baseline.WalkingFatiguePesosPerKilometer,
            WalkingSpeedMetersPerSecond = baseline.WalkingSpeedMetersPerSecond,
            TrikeSpeedMetersPerSecond = baseline.TrikeSpeedMetersPerSecond,
            JeepneySpeedMetersPerSecond = baseline.JeepneySpeedMetersPerSecond,
            JeepneyBoardingWaitTimeSeconds = baseline.JeepneyBoardingWaitTimeSeconds,
            JeepneyBaseFarePesos = baseline.JeepneyBaseFarePesos,
            MaxCandidatesToConfirm = baseline.MaxCandidatesToConfirm,
            MaxTransfers = baseline.MaxTransfers
        };
    }

    /// <summary>
    /// Valhalla is not available to unit tests, so confirmed distances come
    /// from a road-network stand-in: straight line plus a detour factor. That
    /// is enough for these regressions, which are about which routes a journey
    /// is built from, not about matching production's metres to the metre.
    /// </summary>
    public static RoutingService CreateService(RoutingOptions? options = null)
    {
        var (routes, trikePoints) = LoadNetwork();

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(trikePoints);

        return new RoutingService(
            new RoadNetworkValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options ?? DeployedOptions()));
    }

    private static (List<TransportRoute> Routes, List<TricyclePoint> TrikePoints) LoadNetwork()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "TestData", "production-network.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var routes = document.RootElement.GetProperty("routes").EnumerateArray()
            .Select(route =>
            {
                var routeId = route.GetProperty("routeId").GetInt32();
                return new TransportRoute
                {
                    RouteId = routeId,
                    RouteCode = route.GetProperty("routeCode").GetString()!,
                    RouteName = route.GetProperty("routeName").GetString()!,
                    OriginName = "start",
                    DestinationName = "end",
                    IsActive = true,
                    TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
                    RoutePoints = route.GetProperty("points").EnumerateArray()
                        .Select((point, order) => new RoutePoint
                        {
                            RouteId = routeId,
                            PointOrder = order,
                            Latitude = point[0].GetDouble(),
                            Longitude = point[1].GetDouble()
                        }).ToList()
                };
            }).ToList();

        var trikePoints = document.RootElement.GetProperty("trikePoints").EnumerateArray()
            .Select(point => new TricyclePoint
            {
                TricyclePointId = point.GetProperty("id").GetInt32(),
                PointCode = point.GetProperty("code").GetString()!,
                PointName = point.GetProperty("name").GetString()!,
                CenterLatitude = point.GetProperty("lat").GetDouble(),
                CenterLongitude = point.GetProperty("lon").GetDouble(),
                IsActive = true
            }).ToList();

        return (routes, trikePoints);
    }
}
