using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class TransportRouteServiceTests
{
    [Fact]
    public async Task GetAllActiveRoutesAsync_WhenRepositoryReturnsRoutes_ReturnsRoutesAndCallsRepositoryOnce()
    {
        // Arrange
        var context = CreateContext();
        var routes = new List<TransportRoute>
        {
            new() { RouteId = Guid.NewGuid(), RouteCode = "R1", RouteName = "Route 1", TransportModeId = 1 },
            new() { RouteId = Guid.NewGuid(), RouteCode = "R2", RouteName = "Route 2", TransportModeId = 1 },
        };

        context.TransportRouteRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        // Act
        var result = await context.Service.GetAllActiveRoutesAsync();

        // Assert
        Assert.Same(routes, result);
        context.TransportRouteRepository.Verify(
            repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRouteByIdAsync_WhenRouteExists_ReturnsRoute()
    {
        // Arrange
        var context = CreateContext();
        var routeId = Guid.NewGuid();
        var route = new TransportRoute { RouteId = routeId, RouteCode = "BUS-01", RouteName = "Loop", TransportModeId = 1 };

        context.TransportRouteRepository
            .Setup(repository => repository.GetByIdAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

        // Act
        var result = await context.Service.GetRouteByIdAsync(routeId);

        // Assert
        Assert.Same(route, result);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByIdAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRouteByIdAsync_WhenRouteIdIsEmpty_ReturnsNullWithoutCallingRepository()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetRouteByIdAsync(Guid.Empty);

        // Assert
        Assert.Null(result);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRouteByCodeAsync_WhenCodeHasWhitespace_TrimsCodeBeforeRepositoryCall()
    {
        // Arrange
        var context = CreateContext();
        var route = new TransportRoute { RouteId = Guid.NewGuid(), RouteCode = "A1", RouteName = "Airport", TransportModeId = 1 };

        context.TransportRouteRepository
            .Setup(repository => repository.GetByRouteCodeAsync("A1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

        // Act
        var result = await context.Service.GetRouteByCodeAsync("  A1  ");

        // Assert
        Assert.Same(route, result);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByRouteCodeAsync("A1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRoutesByTransportModeAsync_WhenTransportModeIdIsInvalid_ReturnsEmptyWithoutCallingRepository()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetRoutesByTransportModeAsync(0);

        // Assert
        Assert.Empty(result);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByTransportModeAsync(It.IsAny<short>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRouteDetailsAsync_WhenRouteExists_ReturnsCombinedRouteDetails()
    {
        // Arrange
        var context = CreateContext();
        var routeId = Guid.NewGuid();
        var startStop = CreateStop("START", "Central", 14.6, 121.0);
        var endStop = CreateStop("END", "Terminal", 14.7, 121.1);
        var mode = new TransportMode
        {
            TransportModeId = 1,
            Code = "BUS",
            Name = "Bus",
            IsMotorized = true,
            AllowsLiveDriver = true,
            IconName = "bus",
            IsActive = true,
        };
        var route = new TransportRoute
        {
            RouteId = routeId,
            RouteCode = "BUS-01",
            RouteName = "Central Loop",
            TransportModeId = mode.TransportModeId,
            TransportMode = mode,
            StartStopId = startStop.StopId,
            StartStop = startStop,
            EndStopId = endStop.StopId,
            EndStop = endStop,
            RouteDescription = "Downtown service",
            BaseFare = 15,
            EstimatedTotalMinutes = 45,
            OperatesMonday = true,
            OperatesTuesday = true,
            OperatesWednesday = true,
            OperatesThursday = true,
            OperatesFriday = true,
        };
        var routeStops = new List<RouteStop>
        {
            new() { RouteStopId = Guid.NewGuid(), RouteId = routeId, StopId = startStop.StopId, Stop = startStop, StopOrder = 1, CanBoard = true, CanAlight = false },
            new() { RouteStopId = Guid.NewGuid(), RouteId = routeId, StopId = endStop.StopId, Stop = endStop, StopOrder = 2, CanBoard = false, CanAlight = true },
        };
        var routeSegments = new List<RouteSegment>
        {
            new()
            {
                SegmentId = 10,
                RouteId = routeId,
                FromStopId = startStop.StopId,
                FromStop = startStop,
                ToStopId = endStop.StopId,
                ToStop = endStop,
                SegmentOrder = 1,
                DistanceMeters = 1200,
                EstimatedMinutes = 8,
                EstimatedFare = 15,
                IsBidirectional = false,
            },
        };
        var fareRules = new List<FareRule>
        {
            new()
            {
                FareRuleId = Guid.NewGuid(),
                RouteId = routeId,
                TransportModeId = mode.TransportModeId,
                RuleName = "Base fare",
                BaseFare = 15,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsActive = true,
            },
        };

        context.TransportRouteRepository
            .Setup(repository => repository.GetWithEndpointsAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        context.RouteStopRepository
            .Setup(repository => repository.GetOrderedStopsForRouteAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(routeStops);
        context.RouteSegmentRepository
            .Setup(repository => repository.GetOrderedSegmentsForRouteAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(routeSegments);
        context.FareRuleRepository
            .Setup(repository => repository.GetActiveByRouteAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fareRules);

        // Act
        var result = await context.Service.GetRouteDetailsAsync(routeId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(routeId, result.RouteId);
        Assert.Equal("BUS-01", result.RouteCode);
        Assert.Equal("Bus", result.TransportMode?.Name);
        Assert.Equal("Central", result.StartStop?.Name);
        Assert.Equal("Terminal", result.EndStop?.Name);
        Assert.Equal([1, 2], result.Stops.Select(stop => stop.StopOrder));
        Assert.Equal([1], result.Segments.Select(segment => segment.SegmentOrder));
        Assert.Single(result.FareRules);
        Assert.Equal("Base fare", result.FareRules[0].RuleName);

        context.TransportRouteRepository.Verify(
            repository => repository.GetWithEndpointsAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.RouteStopRepository.Verify(
            repository => repository.GetOrderedStopsForRouteAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.RouteSegmentRepository.Verify(
            repository => repository.GetOrderedSegmentsForRouteAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.FareRuleRepository.Verify(
            repository => repository.GetActiveByRouteAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRouteDetailsAsync_WhenRouteDoesNotExist_ReturnsNullWithoutLoadingChildData()
    {
        // Arrange
        var context = CreateContext();
        var routeId = Guid.NewGuid();

        context.TransportRouteRepository
            .Setup(repository => repository.GetWithEndpointsAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransportRoute?)null);

        // Act
        var result = await context.Service.GetRouteDetailsAsync(routeId);

        // Assert
        Assert.Null(result);
        context.RouteStopRepository.Verify(
            repository => repository.GetOrderedStopsForRouteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.RouteSegmentRepository.Verify(
            repository => repository.GetOrderedSegmentsForRouteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.FareRuleRepository.Verify(
            repository => repository.GetActiveByRouteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRouteStopsAsync_WhenRouteIdIsValid_ReturnsRepositoryStops()
    {
        // Arrange
        var context = CreateContext();
        var routeId = Guid.NewGuid();
        var stops = new List<RouteStop>
        {
            new() { RouteStopId = Guid.NewGuid(), RouteId = routeId, StopId = Guid.NewGuid(), StopOrder = 1 },
        };

        context.RouteStopRepository
            .Setup(repository => repository.GetOrderedStopsForRouteAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stops);

        // Act
        var result = await context.Service.GetRouteStopsAsync(routeId);

        // Assert
        Assert.Same(stops, result);
        context.RouteStopRepository.Verify(
            repository => repository.GetOrderedStopsForRouteAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRouteSegmentsAsync_WhenRouteIdIsEmpty_ReturnsEmptyWithoutCallingRepository()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetRouteSegmentsAsync(Guid.Empty);

        // Assert
        Assert.Empty(result);
        context.RouteSegmentRepository.Verify(
            repository => repository.GetOrderedSegmentsForRouteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestContext CreateContext()
    {
        var transportRouteRepository = new Mock<ITransportRouteRepository>(MockBehavior.Strict);
        var routeStopRepository = new Mock<IRouteStopRepository>(MockBehavior.Strict);
        var routeSegmentRepository = new Mock<IRouteSegmentRepository>(MockBehavior.Strict);
        var fareRuleRepository = new Mock<IFareRuleRepository>(MockBehavior.Strict);

        return new TestContext(
            new TransportRouteService(
                transportRouteRepository.Object,
                routeStopRepository.Object,
                routeSegmentRepository.Object,
                fareRuleRepository.Object),
            transportRouteRepository,
            routeStopRepository,
            routeSegmentRepository,
            fareRuleRepository);
    }

    private static TransportStop CreateStop(string code, string name, double latitude, double longitude) =>
        new()
        {
            StopId = Guid.NewGuid(),
            StopCode = code,
            Name = name,
            StopType = "TERMINAL",
            Latitude = latitude,
            Longitude = longitude,
            IsActive = true,
        };

    private sealed record TestContext(
        TransportRouteService Service,
        Mock<ITransportRouteRepository> TransportRouteRepository,
        Mock<IRouteStopRepository> RouteStopRepository,
        Mock<IRouteSegmentRepository> RouteSegmentRepository,
        Mock<IFareRuleRepository> FareRuleRepository);
}
