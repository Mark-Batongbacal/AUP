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
}
