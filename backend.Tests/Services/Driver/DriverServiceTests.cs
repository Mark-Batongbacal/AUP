using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Driver;

public sealed class DriverServiceTests
{
    [Fact]
    public async Task GetDriverDetailsAsync_WhenDriverExists_ReturnsCombinedDriverDetails()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homeTerminal = CreateStop("HOME", "Home Terminal");
        var driver = new backend.Models.Database.Driver
        {
            DriverId = driverId,
            UserId = userId,
            User = new UserProfile
            {
                UserId = userId,
                FirstName = "Ana",
                LastName = "Santos",
                Role = "DRIVER",
                IsActive = true,
            },
            LicenseNumber = "D-123",
            VerificationStatus = "VERIFIED",
            HomeTerminalId = homeTerminal.StopId,
            AverageRating = 4.8m,
            RatingCount = 12,
            IsAvailable = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        };
        var driverWithTerminal = new backend.Models.Database.Driver
        {
            DriverId = driverId,
            UserId = userId,
            VerificationStatus = "VERIFIED",
            HomeTerminal = homeTerminal,
        };
        var vehicle = CreateVehicle(driverId, capacity: 4);
        var location = new DriverLocation
        {
            DriverId = driverId,
            Latitude = 14.6,
            Longitude = 121.0,
            UpdatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
        };
        var session = new DriverAvailabilitySession
        {
            SessionId = Guid.NewGuid(),
            DriverId = driverId,
            VehicleId = vehicle.VehicleId,
            Vehicle = vehicle,
            AvailableSeats = 3,
            MaximumDetourMeters = 1500,
            Status = "AVAILABLE",
            StartedAt = new DateTime(2026, 1, 3, 1, 0, 0, DateTimeKind.Utc),
        };

        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        context.DriverRepository
            .Setup(repository => repository.GetWithHomeTerminalAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driverWithTerminal);
        context.DriverVehicleRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vehicle]);
        context.DriverLocationRepository
            .Setup(repository => repository.GetByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await context.Service.GetDriverDetailsAsync(driverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(driverId, result.DriverId);
        Assert.Equal("Ana", result.User?.FirstName);
        Assert.Equal("Home Terminal", result.HomeTerminal?.Name);
        Assert.Single(result.ActiveVehicles);
        Assert.Equal(vehicle.VehicleId, result.ActiveVehicles[0].VehicleId);
        Assert.Equal(14.6, result.CurrentLocation?.Latitude);
        Assert.Equal(session.SessionId, result.CurrentAvailabilitySession?.SessionId);

        context.DriverRepository.Verify(
            repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverRepository.Verify(
            repository => repository.GetWithHomeTerminalAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverVehicleRepository.Verify(
            repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverLocationRepository.Verify(
            repository => repository.GetByDriverAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.AvailabilitySessionRepository.Verify(
            repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetDriverAvailabilityAsync_WhenDriverIdIsEmpty_ReturnsFalseWithoutCallingRepository()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.SetDriverAvailabilityAsync(Guid.Empty, true);

        // Assert
        Assert.False(result);
        context.DriverRepository.Verify(
            repository => repository.UpdateAvailabilityAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateDriverLocationAsync_WhenDriverExists_AddsOrUpdatesLocation()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        DriverLocation? capturedLocation = null;

        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new backend.Models.Database.Driver { DriverId = driverId, UserId = Guid.NewGuid(), VerificationStatus = "VERIFIED" });
        context.DriverLocationRepository
            .Setup(repository => repository.AddOrUpdateAsync(It.IsAny<DriverLocation>(), It.IsAny<CancellationToken>()))
            .Callback<DriverLocation, CancellationToken>((location, _) => capturedLocation = location)
            .ReturnsAsync((DriverLocation location, CancellationToken _) => location);

        // Act
        var result = await context.Service.UpdateDriverLocationAsync(
            driverId,
            14.5995,
            120.9842,
            headingDegrees: 90,
            speedKph: 24,
            accuracyMeters: 5,
            updatedAt: updatedAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedLocation, result);
        Assert.Equal(driverId, capturedLocation?.DriverId);
        Assert.Equal(14.5995, capturedLocation?.Latitude);
        Assert.Equal(120.9842, capturedLocation?.Longitude);
        Assert.Equal(90, capturedLocation?.HeadingDegrees);
        Assert.Equal(updatedAt, capturedLocation?.UpdatedAt);
        Assert.Equal(4326, capturedLocation?.Location?.SRID);

        context.DriverRepository.Verify(
            repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverLocationRepository.Verify(
            repository => repository.AddOrUpdateAsync(It.IsAny<DriverLocation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAvailabilitySessionAsync_WhenVehicleValidAndNoActiveSession_CreatesSessionAndMarksDriverAvailable()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var vehicle = CreateVehicle(driverId, capacity: 3);
        var startedAt = new DateTime(2026, 3, 1, 8, 30, 0, DateTimeKind.Utc);
        DriverAvailabilitySession? capturedSession = null;

        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new backend.Models.Database.Driver { DriverId = driverId, UserId = Guid.NewGuid(), VerificationStatus = "VERIFIED" });
        context.DriverVehicleRepository
            .Setup(repository => repository.GetByIdAsync(vehicle.VehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriverAvailabilitySession?)null);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.AddAsync(It.IsAny<DriverAvailabilitySession>(), It.IsAny<CancellationToken>()))
            .Callback<DriverAvailabilitySession, CancellationToken>((session, _) => capturedSession = session)
            .ReturnsAsync((DriverAvailabilitySession session, CancellationToken _) =>
            {
                session.SessionId = Guid.NewGuid();
                return session;
            });
        context.DriverRepository
            .Setup(repository => repository.UpdateAvailabilityAsync(driverId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.StartAvailabilitySessionAsync(
            driverId,
            vehicle.VehicleId,
            destinationName: "  Mall terminal  ",
            availableSeats: 2,
            maximumDetourMeters: 500,
            startedAt: startedAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedSession, result);
        Assert.Equal(driverId, capturedSession?.DriverId);
        Assert.Equal(vehicle.VehicleId, capturedSession?.VehicleId);
        Assert.Equal("Mall terminal", capturedSession?.DestinationName);
        Assert.Equal(2, capturedSession?.AvailableSeats);
        Assert.Equal(500, capturedSession?.MaximumDetourMeters);
        Assert.Equal("AVAILABLE", capturedSession?.Status);
        Assert.Equal(startedAt, capturedSession?.StartedAt);

        context.AvailabilitySessionRepository.Verify(
            repository => repository.AddAsync(It.IsAny<DriverAvailabilitySession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverRepository.Verify(
            repository => repository.UpdateAvailabilityAsync(driverId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAvailabilitySessionAsync_WhenVehicleDoesNotBelongToDriver_ReturnsNullWithoutCreatingSession()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var vehicle = CreateVehicle(Guid.NewGuid(), capacity: 3);

        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new backend.Models.Database.Driver { DriverId = driverId, UserId = Guid.NewGuid(), VerificationStatus = "VERIFIED" });
        context.DriverVehicleRepository
            .Setup(repository => repository.GetByIdAsync(vehicle.VehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        // Act
        var result = await context.Service.StartAvailabilitySessionAsync(driverId, vehicle.VehicleId);

        // Assert
        Assert.Null(result);
        context.AvailabilitySessionRepository.Verify(
            repository => repository.AddAsync(It.IsAny<DriverAvailabilitySession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.DriverRepository.Verify(
            repository => repository.UpdateAvailabilityAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAvailabilitySessionAsync_WhenDriverAlreadyHasActiveSession_ReturnsNullWithoutCreatingSession()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();

        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new backend.Models.Database.Driver { DriverId = driverId, UserId = Guid.NewGuid(), VerificationStatus = "VERIFIED" });
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverAvailabilitySession
            {
                SessionId = Guid.NewGuid(),
                DriverId = driverId,
                AvailableSeats = 1,
                MaximumDetourMeters = 1000,
                Status = "AVAILABLE",
                StartedAt = DateTime.UtcNow,
            });

        // Act
        var result = await context.Service.StartAvailabilitySessionAsync(driverId);

        // Assert
        Assert.Null(result);
        context.AvailabilitySessionRepository.Verify(
            repository => repository.AddAsync(It.IsAny<DriverAvailabilitySession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EndAvailabilitySessionAsync_WhenActiveSessionEnds_MarksDriverUnavailable()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var endedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverAvailabilitySession
            {
                SessionId = sessionId,
                DriverId = driverId,
                Status = "AVAILABLE",
                AvailableSeats = 1,
                MaximumDetourMeters = 1000,
                StartedAt = DateTime.UtcNow,
            });
        context.AvailabilitySessionRepository
            .Setup(repository => repository.EndSessionAsync(sessionId, endedAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        context.DriverRepository
            .Setup(repository => repository.UpdateAvailabilityAsync(driverId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.EndAvailabilitySessionAsync(driverId, endedAt);

        // Assert
        Assert.True(result);
        context.AvailabilitySessionRepository.Verify(
            repository => repository.EndSessionAsync(sessionId, endedAt, It.IsAny<CancellationToken>()),
            Times.Once);
        context.DriverRepository.Verify(
            repository => repository.UpdateAvailabilityAsync(driverId, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EndAvailabilitySessionAsync_WhenNoActiveSession_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();

        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriverAvailabilitySession?)null);

        // Act
        var result = await context.Service.EndAvailabilitySessionAsync(driverId);

        // Assert
        Assert.False(result);
        context.AvailabilitySessionRepository.Verify(
            repository => repository.EndSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        context.DriverRepository.Verify(
            repository => repository.UpdateAvailabilityAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestContext CreateContext()
    {
        var driverRepository = new Mock<IDriverRepository>(MockBehavior.Strict);
        var driverVehicleRepository = new Mock<IDriverVehicleRepository>(MockBehavior.Strict);
        var driverLocationRepository = new Mock<IDriverLocationRepository>(MockBehavior.Strict);
        var availabilitySessionRepository = new Mock<IDriverAvailabilitySessionRepository>(MockBehavior.Strict);

        return new TestContext(
            new DriverService(
                driverRepository.Object,
                driverVehicleRepository.Object,
                driverLocationRepository.Object,
                availabilitySessionRepository.Object),
            driverRepository,
            driverVehicleRepository,
            driverLocationRepository,
            availabilitySessionRepository);
    }

    private static DriverVehicle CreateVehicle(Guid driverId, int capacity) =>
        new()
        {
            VehicleId = Guid.NewGuid(),
            DriverId = driverId,
            TransportModeId = 1,
            TransportMode = new TransportMode
            {
                TransportModeId = 1,
                Code = "CAR",
                Name = "Car",
                IsMotorized = true,
                AllowsLiveDriver = true,
                IsActive = true,
            },
            PlateNumber = "ABC-123",
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static TransportStop CreateStop(string code, string name) =>
        new()
        {
            StopId = Guid.NewGuid(),
            StopCode = code,
            Name = name,
            StopType = "TERMINAL",
            Latitude = 14.6,
            Longitude = 121.0,
            IsActive = true,
        };

    private sealed record TestContext(
        DriverService Service,
        Mock<IDriverRepository> DriverRepository,
        Mock<IDriverVehicleRepository> DriverVehicleRepository,
        Mock<IDriverLocationRepository> DriverLocationRepository,
        Mock<IDriverAvailabilitySessionRepository> AvailabilitySessionRepository);
}
