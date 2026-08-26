using backend.Models.Database;
using backend.Models.JeepneyRouteManagement;
using backend.Repositories;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class AdminJeepneyRouteManagementServiceTests
{
    [Fact]
    public async Task CreateDraftAsync_ValidRequest_CreatesInactiveJeepneyRouteWithoutGeometry()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        modeRepository
            .Setup(repository => repository.GetByCodeAsync("JEEPNEY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportMode
            {
                TransportModeId = 2,
                Code = "JEEPNEY",
                Name = "Jeepney",
                IsActive = true
            });
        routeRepository
            .Setup(repository => repository.GetByRouteCodeAsync("XEVERA-ASTRO", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransportRoute?)null);
        routeRepository
            .Setup(repository => repository.AddAsync(It.IsAny<TransportRoute>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransportRoute route, CancellationToken _) =>
            {
                route.RouteId = 44;
                return route;
            });

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.CreateDraftAsync(Request());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Route);
        Assert.False(result.Route!.IsActive);
        Assert.Equal(0, result.Route.PointCount);
        Assert.False(result.Route.HasPolyline);
        routeRepository.Verify(repository => repository.AddAsync(
            It.Is<TransportRoute>(route =>
                !route.IsActive &&
                route.TransportModeId == 2 &&
                route.RoutePoints.Count == 0 &&
                route.EncodedPolyline == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDraftAsync_DuplicateCode_ReturnsConflictWithoutSaving()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        routeRepository
            .Setup(repository => repository.GetByRouteCodeAsync("XEVERA-ASTRO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportRoute { RouteId = 9, RouteCode = "XEVERA-ASTRO", RouteName = "Existing" });

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.CreateDraftAsync(Request());

        Assert.Equal(AdminJeepneyRouteMutationStatus.Conflict, result.Status);
        routeRepository.Verify(repository => repository.AddAsync(
            It.IsAny<TransportRoute>(), It.IsAny<CancellationToken>()), Times.Never);
        modeRepository.Verify(repository => repository.GetByCodeAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDraftAsync_ActiveRoute_ReturnsLockedWithoutUpdating()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        routeRepository
            .Setup(repository => repository.GetTrackedByIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportRoute
            {
                RouteId = 12,
                RouteCode = "XEVERA-ASTRO",
                RouteName = "Published",
                OriginName = "Xevera",
                DestinationName = "Astro",
                IsActive = true,
                TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" }
            });

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.UpdateDraftAsync(12, Request());

        Assert.Equal(AdminJeepneyRouteMutationStatus.ActiveRouteLocked, result.Status);
        routeRepository.Verify(repository => repository.UpdateAsync(
            It.IsAny<TransportRoute>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_BothFiltersOff_DoesNotQueryRepository()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);

        var result = await service.GetAllAsync(includeActive: false, includeDrafts: false);

        Assert.Empty(result);
        routeRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceDraftGeometryAsync_ValidDraft_SavesOrderedGeometryAndPolyline()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        routeRepository
            .Setup(repository => repository.GetByIdWithPointsForAdminAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftRoute(44));
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

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.ReplaceDraftGeometryAsync(44, GeometryRequest());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Geometry);
        Assert.Equal(3, result.Geometry!.Points.Count);
        Assert.Equal([1, 2, 3], result.Geometry.Points.Select(point => point.PointOrder));
        Assert.False(string.IsNullOrWhiteSpace(result.Geometry.EncodedPolyline));
        routeRepository.Verify(repository => repository.ReplaceDraftGeometryAsync(
            44,
            It.Is<IReadOnlyList<RoutePoint>>(points =>
                points.Count == 3 &&
                points[0].PointOrder == 1 &&
                points[1].PointOrder == 2 &&
                points[2].PointOrder == 3),
            It.Is<IReadOnlyList<RouteWaypoint>>(points =>
                points.Count == 3 &&
                points[0].WaypointOrder == 1 &&
                points[2].WaypointOrder == 3),
            It.Is<string>(polyline => !string.IsNullOrWhiteSpace(polyline)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceDraftGeometryAsync_ActiveRoute_ReturnsLockedWithoutSaving()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        var activeRoute = DraftRoute(44);
        activeRoute.IsActive = true;
        routeRepository
            .Setup(repository => repository.GetByIdWithPointsForAdminAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeRoute);

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.ReplaceDraftGeometryAsync(44, GeometryRequest());

        Assert.Equal(AdminJeepneyRouteMutationStatus.ActiveRouteLocked, result.Status);
        routeRepository.Verify(repository => repository.ReplaceDraftGeometryAsync(
            It.IsAny<long>(),
            It.IsAny<IReadOnlyList<RoutePoint>>(),
            It.IsAny<IReadOnlyList<RouteWaypoint>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceDraftGeometryAsync_InvalidCoordinate_ReturnsValidationWithoutQueryingRepository()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        var request = new AdminJeepneyRouteGeometryRequest
        {
            Points =
            [
                new() { Latitude = 100, Longitude = 120.5 },
                new() { Latitude = 15.1, Longitude = 120.6 }
            ]
        };

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.ReplaceDraftGeometryAsync(44, request);

        Assert.Equal(AdminJeepneyRouteMutationStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("latitude", StringComparison.OrdinalIgnoreCase));
        routeRepository.VerifyNoOtherCalls();
        modeRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReplaceDraftGeometryAsync_DraftPublishedDuringSave_ReturnsLocked()
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var modeRepository = new Mock<ITransportModeRepository>();
        routeRepository
            .Setup(repository => repository.GetByIdWithPointsForAdminAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftRoute(44));
        routeRepository
            .Setup(repository => repository.ReplaceDraftGeometryAsync(
                44,
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<IReadOnlyList<RouteWaypoint>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransportRoute?)null);

        var service = new AdminJeepneyRouteManagementService(routeRepository.Object, modeRepository.Object);
        var result = await service.ReplaceDraftGeometryAsync(44, GeometryRequest());

        Assert.Equal(AdminJeepneyRouteMutationStatus.ActiveRouteLocked, result.Status);
    }

    private static AdminJeepneyRouteMutationRequest Request() => new()
    {
        RouteCode = " XEVERA-ASTRO ",
        RouteName = " Xevera to Astro ",
        OriginName = " Xevera ",
        DestinationName = " Astro ",
        DirectionName = "Outbound",
        OperatorName = "Verified Operator",
        Description = "Draft route metadata",
        BaseFare = 13m
    };

    private static AdminJeepneyRouteGeometryRequest GeometryRequest() => new()
    {
        Points =
        [
            new() { Latitude = 15.154, Longitude = 120.591 },
            new() { Latitude = 15.151, Longitude = 120.598 },
            new() { Latitude = 15.147, Longitude = 120.605 }
        ]
    };

    private static TransportRoute DraftRoute(long routeId) => new()
    {
        RouteId = routeId,
        RouteCode = "XEVERA-ASTRO",
        RouteName = "Xevera to Astro",
        OriginName = "Xevera",
        DestinationName = "Astro",
        IsActive = false,
        CreatedAt = DateTime.UtcNow,
        TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" }
    };
}
