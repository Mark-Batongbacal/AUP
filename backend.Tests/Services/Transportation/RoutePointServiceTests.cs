using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class RoutePointServiceTests
{
    [Fact]
    public async Task GetRoutePointsAsync_WhenRouteIdIsValid_ReturnsRepositoryPoints()
    {
        // Arrange
        var context = CreateContext();
        var routeId = NextId();
        var routePoints = new List<RoutePoint>
        {
            new() { RoutePointId = NextId(), RouteId = routeId, PointOrder = 1, Latitude = 15.1451, Longitude = 120.5880 },
            new() { RoutePointId = NextId(), RouteId = routeId, PointOrder = 2, Latitude = 15.1458, Longitude = 120.5895 },
        };

        context.RoutePointRepository
            .Setup(repository => repository.GetOrderedByRouteAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(routePoints);

        // Act
        var result = await context.Service.GetRoutePointsAsync(routeId);

        // Assert
        Assert.Equal([1, 2], result.Select(point => point.PointOrder));
        Assert.Equal(15.1451, result[0].Latitude);
        Assert.Equal(120.5895, result[1].Longitude);
        context.RoutePointRepository.Verify(
            repository => repository.GetOrderedByRouteAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRoutePointsAsync_WhenRouteIdIsInvalid_ReturnsEmptyWithoutCallingRepository()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetRoutePointsAsync(0);

        // Assert
        Assert.Empty(result);
        context.RoutePointRepository.Verify(
            repository => repository.GetOrderedByRouteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceRoutePointsAsync_WhenInputIsValid_ReplacesOrderedRoutePoints()
    {
        // Arrange
        var context = CreateContext();
        var routeId = NextId();
        List<RoutePoint>? capturedRoutePoints = null;

        context.TransportRouteRepository
            .Setup(repository => repository.GetByIdAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportRoute
            {
                RouteId = routeId,
                RouteCode = "JEEP-01",
                RouteName = "Jeepney Route",
                TransportModeId = 1,
            });
        context.RoutePointRepository
            .Setup(repository => repository.ReplaceForRouteAsync(
                routeId,
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<CancellationToken>()))
            .Callback<long, IReadOnlyList<RoutePoint>, CancellationToken>((_, routePoints, _) =>
                capturedRoutePoints = routePoints.ToList())
            .ReturnsAsync((long _, IReadOnlyList<RoutePoint> routePoints, CancellationToken _) =>
                routePoints
                    .Select((routePoint, index) =>
                    {
                        routePoint.RoutePointId = index + 100;
                        return routePoint;
                    })
                    .ToList());

        // Act
        var result = await context.Service.ReplaceRoutePointsAsync(
            routeId,
            [
                [15.1451, 120.5880],
                [15.1458, 120.5895],
                [15.1469, 120.5912],
            ]);

        // Assert
        Assert.Equal(RoutePointReplacementStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal([1, 2, 3], capturedRoutePoints?.Select(point => point.PointOrder));
        Assert.All(capturedRoutePoints!, point => Assert.Equal(routeId, point.RouteId));
        Assert.Equal(15.1451, capturedRoutePoints?[0].Latitude);
        Assert.Equal(120.5912, capturedRoutePoints?[2].Longitude);
        Assert.Equal([1, 2, 3], result.RoutePoints.Select(point => point.PointOrder));

        context.TransportRouteRepository.Verify(
            repository => repository.GetByIdAsync(routeId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.RoutePointRepository.Verify(
            repository => repository.ReplaceForRouteAsync(
                routeId,
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplaceRoutePointsAsync_WhenCoordinateIsInvalid_ReturnsValidationErrorsWithoutPersistence()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.ReplaceRoutePointsAsync(
            routeId: 10,
            [
                [120.5880, 15.1451],
                [15.1458, 120.5895],
            ]);

        // Assert
        Assert.Equal(RoutePointReplacementStatus.ValidationFailed, result.Status);
        Assert.Contains("Point 1 latitude must be between -90 and 90.", result.Errors);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.RoutePointRepository.Verify(
            repository => repository.ReplaceForRouteAsync(
                It.IsAny<long>(),
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceRoutePointsAsync_WhenTooFewPoints_ReturnsValidationErrorsWithoutPersistence()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.ReplaceRoutePointsAsync(
            routeId: 10,
            [
                [15.1451, 120.5880],
            ]);

        // Assert
        Assert.Equal(RoutePointReplacementStatus.ValidationFailed, result.Status);
        Assert.Contains("At least 2 route points are required.", result.Errors);
        context.TransportRouteRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.RoutePointRepository.Verify(
            repository => repository.ReplaceForRouteAsync(
                It.IsAny<long>(),
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceRoutePointsAsync_WhenRouteDoesNotExist_ReturnsRouteNotFoundWithoutReplacing()
    {
        // Arrange
        var context = CreateContext();
        var routeId = NextId();

        context.TransportRouteRepository
            .Setup(repository => repository.GetByIdAsync(routeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransportRoute?)null);

        // Act
        var result = await context.Service.ReplaceRoutePointsAsync(
            routeId,
            [
                [15.1451, 120.5880],
                [15.1458, 120.5895],
            ]);

        // Assert
        Assert.Equal(RoutePointReplacementStatus.RouteNotFound, result.Status);
        Assert.Contains($"Transport route {routeId} was not found.", result.Errors);
        context.RoutePointRepository.Verify(
            repository => repository.ReplaceForRouteAsync(
                It.IsAny<long>(),
                It.IsAny<IReadOnlyList<RoutePoint>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestContext CreateContext()
    {
        var routePointRepository = new Mock<IRoutePointRepository>(MockBehavior.Strict);
        var transportRouteRepository = new Mock<ITransportRouteRepository>(MockBehavior.Strict);

        return new TestContext(
            new RoutePointService(routePointRepository.Object, transportRouteRepository.Object),
            routePointRepository,
            transportRouteRepository);
    }

    private static long NextId() => Interlocked.Increment(ref _nextId);

    private static long _nextId;

    private sealed record TestContext(
        RoutePointService Service,
        Mock<IRoutePointRepository> RoutePointRepository,
        Mock<ITransportRouteRepository> TransportRouteRepository);
}
