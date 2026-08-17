using backend.Models.Database;
using backend.Repositories;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class TransferConnectionServiceTests
{
    [Fact]
    public async Task GetActiveConnectionsForStopAsync_WhenStopIdIsValid_DelegatesToRepository()
    {
        // Arrange
        var context = CreateContext();
        var stopId = NextId();
        var connections = new List<TransferConnection>
        {
            new() { TransferConnectionId = NextId(), FromStopId = stopId, ToStopId = NextId(), IsActive = true },
        };

        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveForStopAsync(stopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connections);

        // Act
        var result = await context.Service.GetActiveConnectionsForStopAsync(stopId);

        // Assert
        Assert.Same(connections, result);
        context.TransferConnectionRepository.Verify(
            repository => repository.GetActiveForStopAsync(stopId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveConnectionsForStopAsync_WhenStopIdIsInvalid_ReturnsEmptyWithoutRepositoryCall()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.GetActiveConnectionsForStopAsync(0);

        // Assert
        Assert.Empty(result);
        context.TransferConnectionRepository.Verify(
            repository => repository.GetActiveForStopAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddVerifiedTransferConnectionAsync_WhenInputIsValid_AddsConnection()
    {
        // Arrange
        var context = CreateContext();
        var fromStopId = NextId();
        var toStopId = NextId();
        TransferConnection? capturedConnection = null;

        SetupExistingStops(context, fromStopId, toStopId);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(fromStopId, toStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferConnection?)null);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(toStopId, fromStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferConnection?)null);
        context.TransferConnectionRepository
            .Setup(repository => repository.AddAsync(It.IsAny<TransferConnection>(), It.IsAny<CancellationToken>()))
            .Callback<TransferConnection, CancellationToken>((connection, _) => capturedConnection = connection)
            .ReturnsAsync((TransferConnection connection, CancellationToken _) =>
            {
                connection.TransferConnectionId = NextId();
                return connection;
            });

        // Act
        var result = await context.Service.AddVerifiedTransferConnectionAsync(
            fromStopId,
            toStopId,
            maximumWalkingDistanceMeters: 250,
            estimatedWalkingTimeSeconds: 180,
            instructions: "  Use footbridge  ",
            isBidirectional: true);

        // Assert
        Assert.Equal(TransferConnectionMutationStatus.Success, result.Status);
        Assert.Same(capturedConnection, result.TransferConnection);
        Assert.Equal(fromStopId, capturedConnection?.FromStopId);
        Assert.Equal(toStopId, capturedConnection?.ToStopId);
        Assert.Equal(250, capturedConnection?.MaximumWalkingDistanceMeters);
        Assert.Equal(180, capturedConnection?.EstimatedWalkingTimeSeconds);
        Assert.Equal("Use footbridge", capturedConnection?.Instructions);
        Assert.True(capturedConnection?.IsBidirectional);
        Assert.True(capturedConnection?.IsActive);
    }

    [Fact]
    public async Task AddVerifiedTransferConnectionAsync_WhenInputIsInvalid_ReturnsValidationErrorsWithoutLookup()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.AddVerifiedTransferConnectionAsync(
            fromStopId: 5,
            toStopId: 5,
            maximumWalkingDistanceMeters: -1,
            estimatedWalkingTimeSeconds: -1);

        // Assert
        Assert.Equal(TransferConnectionMutationStatus.ValidationFailed, result.Status);
        Assert.Contains("Origin and destination transport stops must be different.", result.Errors);
        Assert.Contains("Maximum walking distance cannot be negative.", result.Errors);
        Assert.Contains("Estimated walking time cannot be negative.", result.Errors);
        context.TransportStopRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.TransferConnectionRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TransferConnection>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddVerifiedTransferConnectionAsync_WhenReverseConnectionIsBidirectional_ReturnsDuplicate()
    {
        // Arrange
        var context = CreateContext();
        var fromStopId = NextId();
        var toStopId = NextId();

        SetupExistingStops(context, fromStopId, toStopId);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(fromStopId, toStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferConnection?)null);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(toStopId, fromStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferConnection
            {
                TransferConnectionId = NextId(),
                FromStopId = toStopId,
                ToStopId = fromStopId,
                IsBidirectional = true,
                IsActive = true,
            });

        // Act
        var result = await context.Service.AddVerifiedTransferConnectionAsync(
            fromStopId,
            toStopId,
            isBidirectional: false);

        // Assert
        Assert.Equal(TransferConnectionMutationStatus.Duplicate, result.Status);
        context.TransferConnectionRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TransferConnection>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateVerifiedTransferConnectionAsync_WhenInputIsValid_UpdatesConnection()
    {
        // Arrange
        var context = CreateContext();
        var connectionId = NextId();
        var fromStopId = NextId();
        var toStopId = NextId();
        var existingConnection = new TransferConnection
        {
            TransferConnectionId = connectionId,
            FromStopId = fromStopId,
            ToStopId = toStopId,
            IsActive = true,
        };
        TransferConnection? updatedConnection = null;

        context.TransferConnectionRepository
            .Setup(repository => repository.GetByIdAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConnection);
        SetupExistingStops(context, fromStopId, toStopId);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(fromStopId, toStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConnection);
        context.TransferConnectionRepository
            .Setup(repository => repository.GetActiveByStopsAsync(toStopId, fromStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferConnection?)null);
        context.TransferConnectionRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<TransferConnection>(), It.IsAny<CancellationToken>()))
            .Callback<TransferConnection, CancellationToken>((connection, _) => updatedConnection = connection)
            .ReturnsAsync((TransferConnection connection, CancellationToken _) => connection);

        // Act
        var result = await context.Service.UpdateVerifiedTransferConnectionAsync(
            connectionId,
            fromStopId,
            toStopId,
            maximumWalkingDistanceMeters: 300,
            estimatedWalkingTimeSeconds: 210,
            instructions: "  Cross at terminal gate  ",
            isBidirectional: false,
            isActive: true);

        // Assert
        Assert.Equal(TransferConnectionMutationStatus.Success, result.Status);
        Assert.Same(updatedConnection, result.TransferConnection);
        Assert.Equal(300, updatedConnection?.MaximumWalkingDistanceMeters);
        Assert.Equal(210, updatedConnection?.EstimatedWalkingTimeSeconds);
        Assert.Equal("Cross at terminal gate", updatedConnection?.Instructions);
        Assert.False(updatedConnection?.IsBidirectional);
        context.TransferConnectionRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<TransferConnection>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void SetupExistingStops(TestContext context, long fromStopId, long toStopId)
    {
        context.TransportStopRepository
            .Setup(repository => repository.GetByIdAsync(fromStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStop(fromStopId));
        context.TransportStopRepository
            .Setup(repository => repository.GetByIdAsync(toStopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStop(toStopId));
    }

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
        var transferConnectionRepository = new Mock<ITransferConnectionRepository>(MockBehavior.Strict);
        var transportStopRepository = new Mock<ITransportStopRepository>(MockBehavior.Strict);

        return new TestContext(
            new TransferConnectionService(
                transferConnectionRepository.Object,
                transportStopRepository.Object),
            transferConnectionRepository,
            transportStopRepository);
    }

    private static long NextId() => Interlocked.Increment(ref _nextId);

    private static long _nextId;

    private sealed record TestContext(
        TransferConnectionService Service,
        Mock<ITransferConnectionRepository> TransferConnectionRepository,
        Mock<ITransportStopRepository> TransportStopRepository);
}
