using backend.Models.Database;
using backend.Repositories;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class TricyclePointServiceTests
{
    [Fact]
    public async Task GetActivePointsCoveringLocationAsync_WhenLocationIsValid_ReturnsOnlyPointsWithinRadius()
    {
        // Arrange
        var context = CreateContext();
        var coveringPoint = CreatePoint(
            pointId: NextId(),
            pointCode: "TRI-1",
            latitude: 15.1451,
            longitude: 120.5880,
            radiusMeters: 200);
        var outsidePoint = CreatePoint(
            pointId: NextId(),
            pointCode: "TRI-2",
            latitude: 15.1700,
            longitude: 120.6200,
            radiusMeters: 100);

        context.TricyclePointRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([outsidePoint, coveringPoint]);

        // Act
        var result = await context.Service.GetActivePointsCoveringLocationAsync(15.1452, 120.5881);

        // Assert
        var point = Assert.Single(result);
        Assert.Same(coveringPoint, point);
        context.TricyclePointRepository.Verify(
            repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActivePointsCoveringLocationAsync_WhenCoordinatesAreInvalid_ReturnsEmptyWithoutRepositoryCall()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetActivePointsCoveringLocationAsync(double.NaN, 120.5881);

        // Assert
        Assert.Empty(result);
        context.TricyclePointRepository.Verify(
            repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void IsLocationInsideTricyclePointRadius_WhenLocationIsInsideRadius_ReturnsTrue()
    {
        // Arrange
        var context = CreateContext();
        var point = CreatePoint(
            pointId: NextId(),
            pointCode: "TRI-1",
            latitude: 15.1451,
            longitude: 120.5880,
            radiusMeters: 100);

        // Act
        var result = context.Service.IsLocationInsideTricyclePointRadius(point, 15.1451, 120.5880);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AddVerifiedTricyclePointAsync_WhenInputIsValid_AddsPoint()
    {
        // Arrange
        var context = CreateContext();
        var stopId = NextId();
        TricyclePoint? capturedPoint = null;

        context.TricyclePointRepository
            .Setup(repository => repository.GetByPointCodeAsync("TRI-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint?)null);
        context.TransportStopRepository
            .Setup(repository => repository.GetByIdAsync(stopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStop(stopId));
        context.TricyclePointRepository
            .Setup(repository => repository.GetByStopIdAsync(stopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint?)null);
        context.TricyclePointRepository
            .Setup(repository => repository.AddAsync(It.IsAny<TricyclePoint>(), It.IsAny<CancellationToken>()))
            .Callback<TricyclePoint, CancellationToken>((point, _) => capturedPoint = point)
            .ReturnsAsync((TricyclePoint point, CancellationToken _) =>
            {
                point.TricyclePointId = NextId();
                return point;
            });

        // Act
        var result = await context.Service.AddVerifiedTricyclePointAsync(
            pointCode: "  TRI-1  ",
            pointName: "  Main terminal  ",
            centerLatitude: 15.1451,
            centerLongitude: 120.5880,
            radiusMeters: 500,
            stopId: stopId,
            description: "  Verified terminal  ",
            baseFare: 20,
            farePerKilometer: 5,
            averageWaitingTimeSeconds: 300);

        // Assert
        Assert.Equal(TricyclePointMutationStatus.Success, result.Status);
        Assert.Same(capturedPoint, result.TricyclePoint);
        Assert.Equal("TRI-1", capturedPoint?.PointCode);
        Assert.Equal("Main terminal", capturedPoint?.PointName);
        Assert.Equal("Verified terminal", capturedPoint?.Description);
        Assert.Equal(stopId, capturedPoint?.StopId);
        Assert.Equal(500, capturedPoint?.RadiusMeters);
        Assert.True(capturedPoint?.IsActive);
    }

    [Fact]
    public async Task AddVerifiedTricyclePointAsync_WhenInputIsInvalid_ReturnsValidationErrorsWithoutLookup()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.AddVerifiedTricyclePointAsync(
            pointCode: " ",
            pointName: " ",
            centerLatitude: 91,
            centerLongitude: 181,
            radiusMeters: 0,
            stopId: 0,
            baseFare: -1,
            farePerKilometer: -1,
            averageWaitingTimeSeconds: -1);

        // Assert
        Assert.Equal(TricyclePointMutationStatus.ValidationFailed, result.Status);
        Assert.Contains("Point code is required.", result.Errors);
        Assert.Contains("Point name is required.", result.Errors);
        Assert.Contains("Latitude must be between -90 and 90.", result.Errors);
        Assert.Contains("Longitude must be between -180 and 180.", result.Errors);
        Assert.Contains("Radius meters must be greater than zero.", result.Errors);
        Assert.Contains("Base fare cannot be negative.", result.Errors);
        Assert.Contains("Fare per kilometer cannot be negative.", result.Errors);
        Assert.Contains("Average waiting time cannot be negative.", result.Errors);
        Assert.Contains("Transport stop id must be greater than zero when supplied.", result.Errors);
        context.TricyclePointRepository.Verify(
            repository => repository.GetByPointCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.TricyclePointRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TricyclePoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddVerifiedTricyclePointAsync_WhenPointCodeAlreadyExists_ReturnsDuplicate()
    {
        // Arrange
        var context = CreateContext();
        var existingPoint = CreatePoint(NextId(), "TRI-1", 15.1451, 120.5880, 500);

        context.TricyclePointRepository
            .Setup(repository => repository.GetByPointCodeAsync("TRI-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPoint);

        // Act
        var result = await context.Service.AddVerifiedTricyclePointAsync(
            pointCode: "TRI-1",
            pointName: "Main terminal",
            centerLatitude: 15.1451,
            centerLongitude: 120.5880,
            radiusMeters: 500);

        // Assert
        Assert.Equal(TricyclePointMutationStatus.Duplicate, result.Status);
        Assert.Contains("Point code TRI-1 is already used.", result.Errors);
        context.TricyclePointRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TricyclePoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateVerifiedTricyclePointAsync_WhenInputIsValid_UpdatesPoint()
    {
        // Arrange
        var context = CreateContext();
        var pointId = NextId();
        var existingPoint = CreatePoint(pointId, "TRI-1", 15.1451, 120.5880, 500);
        TricyclePoint? updatedPoint = null;

        context.TricyclePointRepository
            .Setup(repository => repository.GetByIdAsync(pointId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPoint);
        context.TricyclePointRepository
            .Setup(repository => repository.GetByPointCodeAsync("TRI-1A", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint?)null);
        context.TricyclePointRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<TricyclePoint>(), It.IsAny<CancellationToken>()))
            .Callback<TricyclePoint, CancellationToken>((point, _) => updatedPoint = point)
            .ReturnsAsync((TricyclePoint point, CancellationToken _) => point);

        // Act
        var result = await context.Service.UpdateVerifiedTricyclePointAsync(
            tricyclePointId: pointId,
            pointCode: "TRI-1A",
            pointName: "Updated terminal",
            centerLatitude: 15.1460,
            centerLongitude: 120.5890,
            radiusMeters: 700,
            isActive: false);

        // Assert
        Assert.Equal(TricyclePointMutationStatus.Success, result.Status);
        Assert.Same(updatedPoint, result.TricyclePoint);
        Assert.Equal("TRI-1A", updatedPoint?.PointCode);
        Assert.Equal("Updated terminal", updatedPoint?.PointName);
        Assert.Equal(700, updatedPoint?.RadiusMeters);
        Assert.False(updatedPoint?.IsActive);
        Assert.NotNull(updatedPoint?.UpdatedAt);
    }

    [Fact]
    public async Task UpdateVerifiedTricyclePointAsync_WhenPointDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var context = CreateContext();
        var pointId = NextId();

        context.TricyclePointRepository
            .Setup(repository => repository.GetByIdAsync(pointId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint?)null);

        // Act
        var result = await context.Service.UpdateVerifiedTricyclePointAsync(
            pointId,
            "TRI-1",
            "Main terminal",
            15.1451,
            120.5880,
            500);

        // Assert
        Assert.Equal(TricyclePointMutationStatus.NotFound, result.Status);
        context.TricyclePointRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<TricyclePoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TricyclePoint CreatePoint(
        long pointId,
        string pointCode,
        double latitude,
        double longitude,
        int radiusMeters) =>
        new()
        {
            TricyclePointId = pointId,
            PointCode = pointCode,
            PointName = pointCode,
            CenterLatitude = latitude,
            CenterLongitude = longitude,
            RadiusMeters = radiusMeters,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static TransportStop CreateStop(long stopId) =>
        new()
        {
            StopId = stopId,
            Name = $"Stop {stopId}",
            StopType = "Terminal",
            Latitude = 15.145,
            Longitude = 120.588,
            IsActive = true,
        };

    private static TestContext CreateContext()
    {
        var tricyclePointRepository = new Mock<ITricyclePointRepository>(MockBehavior.Strict);
        var transportStopRepository = new Mock<ITransportStopRepository>(MockBehavior.Strict);

        return new TestContext(
            new TricyclePointService(
                tricyclePointRepository.Object,
                transportStopRepository.Object),
            tricyclePointRepository,
            transportStopRepository);
    }

    private static long NextId() => Interlocked.Increment(ref _nextId);

    private static long _nextId;

    private sealed record TestContext(
        TricyclePointService Service,
        Mock<ITricyclePointRepository> TricyclePointRepository,
        Mock<ITransportStopRepository> TransportStopRepository);
}
