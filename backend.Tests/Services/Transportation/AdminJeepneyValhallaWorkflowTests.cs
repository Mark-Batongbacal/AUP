using backend.Models.Database;
using backend.Models.JeepneyRouteManagement;
using backend.Repositories;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class AdminJeepneyValhallaWorkflowTests
{
    [Fact]
    public async Task PreviewValhallaAsync_ValidDraft_GeneratesPreviewWithoutSaving()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        var generator = new Mock<IRouteGeneratorService>();
        routeRepository
            .Setup(repository => repository.GetByIdWithPointsForAdminAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftRoute(44));
        generator
            .Setup(service => service.GenerateAsync(It.IsAny<IReadOnlyList<List<double>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                [15.1000, 120.5000],
                [15.1005, 120.5007],
                [15.1010, 120.5010]
            ]);

        var service = new AdminJeepneyRouteManagementService(
            routeRepository.Object,
            modeRepository.Object,
            generator.Object);

        var result = await service.PreviewValhallaAsync(44, Request());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Preview);
        Assert.Equal(2, result.Preview!.Waypoints.Count);
        Assert.Equal(3, result.Preview.GeneratedPoints.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.Preview.EncodedPolyline));
        routeRepository.Verify(repository => repository.ReplaceDraftGeometryAsync(
            It.IsAny<long>(),
            It.IsAny<IReadOnlyList<RoutePoint>>(),
            It.IsAny<IReadOnlyList<RouteWaypoint>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveValhallaGeometryAsync_ValidDraft_SavesGeneratedPointsAndOriginalWaypointsSeparately()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        var generator = new Mock<IRouteGeneratorService>();
        routeRepository
            .Setup(repository => repository.GetByIdWithPointsForAdminAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftRoute(44));
        generator
            .Setup(service => service.GenerateAsync(It.IsAny<IReadOnlyList<List<double>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                [15.1000, 120.5000],
                [15.1005, 120.5007],
                [15.1010, 120.5010]
            ]);
        routeRepository
            .Setup(repository => repository.ReplaceDraftGeometryAsync(
                44,
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<IReadOnlyList<RouteWaypoint>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                long routeId,
                IReadOnlyList<RoutePoint> routePoints,
                IReadOnlyList<RouteWaypoint> routeWaypoints,
                string polyline,
                CancellationToken _) =>
            {
                var route = DraftRoute(routeId);
                route.RoutePoints = routePoints.ToList();
                route.RouteWaypoints = routeWaypoints.ToList();
                route.EncodedPolyline = polyline;
                return route;
            });

        var service = new AdminJeepneyRouteManagementService(
            routeRepository.Object,
            modeRepository.Object,
            generator.Object);

        var result = await service.SaveValhallaGeometryAsync(44, Request());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Geometry);
        Assert.Equal(3, result.Geometry!.Points.Count);
        routeRepository.Verify(repository => repository.ReplaceDraftGeometryAsync(
            44,
            It.Is<IReadOnlyList<RoutePoint>>(points =>
                points.Count == 3 &&
                points[0].PointOrder == 1 &&
                points[2].PointOrder == 3),
            It.Is<IReadOnlyList<RouteWaypoint>>(waypoints =>
                waypoints.Count == 2 &&
                waypoints[0].WaypointOrder == 1 &&
                waypoints[0].Latitude == 15.1000 &&
                waypoints[1].WaypointOrder == 2 &&
                waypoints[1].Latitude == 15.1010),
            It.Is<string>(polyline => !string.IsNullOrWhiteSpace(polyline)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AdminJeepneyValhallaRequest Request() => new()
    {
        Waypoints =
        [
            new() { Latitude = 15.1000, Longitude = 120.5000 },
            new() { Latitude = 15.1010, Longitude = 120.5010 }
        ]
    };

    private static TransportRoute DraftRoute(long routeId) => new()
    {
        RouteId = routeId,
        RouteCode = "TEST-JEEPNEY",
        RouteName = "Test Jeepney",
        OriginName = "Origin",
        DestinationName = "Destination",
        TransportMode = new TransportMode
        {
            TransportModeId = 2,
            Code = "JEEPNEY",
            Name = "Jeepney",
            IsActive = true
        },
        TransportModeId = 2,
        IsActive = false,
        RoutePoints = [],
        RouteWaypoints = [],
        CreatedAt = DateTime.UtcNow
    };
}
