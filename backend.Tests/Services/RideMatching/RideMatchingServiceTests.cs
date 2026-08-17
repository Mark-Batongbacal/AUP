using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.RideMatching;

public sealed class RideMatchingServiceTests
{
    [Fact]
    public async Task CreateRideRequestAsync_WhenInputIsValidAndTransportModeActive_AddsSearchingRequest()
    {
        // Arrange
        var context = CreateContext();
        var passengerUserId = Guid.NewGuid();
        var requestedAt = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var expiresAt = requestedAt.AddMinutes(15);
        PassengerRideRequest? capturedRequest = null;

        context.TransportModeRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportMode
            {
                TransportModeId = 1,
                Code = "CAR",
                Name = "Car",
                IsMotorized = true,
                AllowsLiveDriver = true,
                IsActive = true,
            });
        context.RideRequestRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PassengerRideRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PassengerRideRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync((PassengerRideRequest request, CancellationToken _) =>
            {
                request.RequestId = Guid.NewGuid();
                return request;
            });

        // Act
        var result = await context.Service.CreateRideRequestAsync(
            passengerUserId,
            "  Pickup  ",
            14.55,
            121.02,
            "  Dropoff  ",
            14.6,
            121.05,
            passengerCount: 2,
            transportModeId: 1,
            maxBudget: 300,
            requestedAt: requestedAt,
            expiresAt: expiresAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedRequest, result);
        Assert.Equal(passengerUserId, capturedRequest?.PassengerUserId);
        Assert.Equal("Pickup", capturedRequest?.PickupName);
        Assert.Equal("Dropoff", capturedRequest?.DropoffName);
        Assert.Equal(2, capturedRequest?.PassengerCount);
        Assert.Equal(300, capturedRequest?.MaxBudget);
        Assert.Equal("SEARCHING", capturedRequest?.Status);
        Assert.Equal(requestedAt, capturedRequest?.RequestedAt);
        Assert.Equal(expiresAt, capturedRequest?.ExpiresAt);

        context.TransportModeRepository.Verify(
            repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()),
            Times.Once);
        context.RideRequestRepository.Verify(
            repository => repository.AddAsync(It.IsAny<PassengerRideRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRideRequestAsync_WhenTransportModeIsInactive_ReturnsNullWithoutAddingRequest()
    {
        // Arrange
        var context = CreateContext();

        context.TransportModeRepository
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransportMode { TransportModeId = 1, Code = "CAR", Name = "Car", IsActive = false });

        // Act
        var result = await context.Service.CreateRideRequestAsync(
            Guid.NewGuid(),
            "Pickup",
            14.55,
            121.02,
            "Dropoff",
            14.6,
            121.05,
            transportModeId: 1);

        // Assert
        Assert.Null(result);
        context.RideRequestRepository.Verify(
            repository => repository.AddAsync(It.IsAny<PassengerRideRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAvailableDriversAsync_WhenDriverHasSessionLocationAndEligibleVehicle_ReturnsCandidate()
    {
        // Arrange
        var context = CreateContext();
        var driverId = Guid.NewGuid();
        var driver = CreateDriver(driverId, isAvailable: true);
        var vehicle = CreateVehicle(driverId, capacity: 4);
        var session = CreateSession(driverId, vehicle.VehicleId, availableSeats: 3);
        var location = CreateLocation(driverId);

        context.DriverRepository
            .Setup(repository => repository.GetAvailableDriversAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([driver]);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        context.DriverLocationRepository
            .Setup(repository => repository.GetByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);
        context.DriverVehicleRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vehicle]);

        // Act
        var result = await context.Service.GetAvailableDriversAsync();

        // Assert
        var candidate = Assert.Single(result);
        Assert.Equal(driverId, candidate.DriverId);
        Assert.Equal(session.SessionId, candidate.ActiveAvailabilitySession.SessionId);
        Assert.Equal(vehicle.VehicleId, candidate.Vehicle.VehicleId);
        Assert.Equal(location.Latitude, candidate.CurrentLocation.Latitude);
    }

    [Fact]
    public async Task GetCandidateDriversAsync_WhenRequestDoesNotExist_ReturnsEmptyWithoutLoadingDrivers()
    {
        // Arrange
        var context = CreateContext();
        var requestId = Guid.NewGuid();

        context.RideRequestRepository
            .Setup(repository => repository.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PassengerRideRequest?)null);

        // Act
        var result = await context.Service.GetCandidateDriversAsync(requestId);

        // Assert
        Assert.Empty(result);
        context.DriverRepository.Verify(
            repository => repository.GetAvailableDriversAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateRideMatchAsync_WhenCandidateIsValid_AddsOfferedMatch()
    {
        // Arrange
        var context = CreateContext();
        var request = CreateActiveRequest(passengerCount: 2);
        var driver = CreateDriver(Guid.NewGuid(), isAvailable: true);
        var vehicle = CreateVehicle(driver.DriverId, capacity: 4);
        var session = CreateSession(driver.DriverId, vehicle.VehicleId, availableSeats: 3);
        RideMatch? capturedMatch = null;

        context.RideRequestRepository
            .Setup(repository => repository.GetByIdAsync(request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        context.DriverLocationRepository
            .Setup(repository => repository.GetByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLocation(driver.DriverId));
        context.DriverVehicleRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vehicle]);
        context.RideMatchRepository
            .Setup(repository => repository.GetByRequestAsync(request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        context.RideMatchRepository
            .Setup(repository => repository.AddAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()))
            .Callback<RideMatch, CancellationToken>((match, _) => capturedMatch = match)
            .ReturnsAsync((RideMatch match, CancellationToken _) =>
            {
                match.MatchId = Guid.NewGuid();
                return match;
            });

        // Act
        var result = await context.Service.CreateRideMatchAsync(
            request.RequestId,
            driver.DriverId,
            vehicle.VehicleId,
            pickupDistanceMeters: 100,
            estimatedFare: 150,
            matchScore: 0.85m);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedMatch, result);
        Assert.Equal(request.RequestId, capturedMatch?.RequestId);
        Assert.Equal(driver.DriverId, capturedMatch?.DriverId);
        Assert.Equal(session.SessionId, capturedMatch?.SessionId);
        Assert.Equal(vehicle.VehicleId, capturedMatch?.VehicleId);
        Assert.Equal("OFFERED", capturedMatch?.Status);

        context.RideMatchRepository.Verify(
            repository => repository.AddAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRideMatchAsync_WhenDuplicateDriverSessionMatchExists_ReturnsNullWithoutAddingMatch()
    {
        // Arrange
        var context = CreateContext();
        var request = CreateActiveRequest(passengerCount: 1);
        var driver = CreateDriver(Guid.NewGuid(), isAvailable: true);
        var vehicle = CreateVehicle(driver.DriverId, capacity: 4);
        var session = CreateSession(driver.DriverId, vehicle.VehicleId, availableSeats: 2);

        context.RideRequestRepository
            .Setup(repository => repository.GetByIdAsync(request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        context.DriverLocationRepository
            .Setup(repository => repository.GetByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLocation(driver.DriverId));
        context.DriverVehicleRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vehicle]);
        context.RideMatchRepository
            .Setup(repository => repository.GetByRequestAsync(request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RideMatch
                {
                    MatchId = Guid.NewGuid(),
                    RequestId = request.RequestId,
                    DriverId = driver.DriverId,
                    SessionId = session.SessionId,
                    VehicleId = vehicle.VehicleId,
                    Status = "OFFERED",
                },
            ]);

        // Act
        var result = await context.Service.CreateRideMatchAsync(request.RequestId, driver.DriverId, vehicle.VehicleId);

        // Assert
        Assert.Null(result);
        context.RideMatchRepository.Verify(
            repository => repository.AddAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateRideMatchAsync_WhenVehicleDoesNotMatchActiveSession_ReturnsNullWithoutAddingMatch()
    {
        // Arrange
        var context = CreateContext();
        var request = CreateActiveRequest(passengerCount: 1);
        var driver = CreateDriver(Guid.NewGuid(), isAvailable: true);
        var sessionVehicleId = Guid.NewGuid();
        var requestedVehicleId = Guid.NewGuid();

        context.RideRequestRepository
            .Setup(repository => repository.GetByIdAsync(request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        context.DriverRepository
            .Setup(repository => repository.GetByIdAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        context.AvailabilitySessionRepository
            .Setup(repository => repository.GetActiveByDriverAsync(driver.DriverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSession(driver.DriverId, sessionVehicleId, availableSeats: 2));

        // Act
        var result = await context.Service.CreateRideMatchAsync(request.RequestId, driver.DriverId, requestedVehicleId);

        // Assert
        Assert.Null(result);
        context.RideMatchRepository.Verify(
            repository => repository.AddAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptMatchAsync_WhenOfferedMatchExists_UpdatesMatchAndRideRequestStatus()
    {
        // Arrange
        var context = CreateContext();
        var matchId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var acceptedAt = new DateTime(2026, 5, 1, 8, 5, 0, DateTimeKind.Utc);
        RideMatch? updatedMatch = null;

        context.RideMatchRepository
            .Setup(repository => repository.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RideMatch
            {
                MatchId = matchId,
                RequestId = requestId,
                DriverId = Guid.NewGuid(),
                Status = "OFFERED",
                OfferedAt = acceptedAt.AddMinutes(-1),
            });
        context.RideMatchRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()))
            .Callback<RideMatch, CancellationToken>((match, _) => updatedMatch = match)
            .ReturnsAsync((RideMatch match, CancellationToken _) => match);
        context.RideRequestRepository
            .Setup(repository => repository.UpdateStatusAsync(requestId, "MATCHED", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.AcceptMatchAsync(matchId, acceptedAt);

        // Assert
        Assert.True(result);
        Assert.Equal("ACCEPTED", updatedMatch?.Status);
        Assert.Equal(acceptedAt, updatedMatch?.AcceptedAt);
        context.RideRequestRepository.Verify(
            repository => repository.UpdateStatusAsync(requestId, "MATCHED", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectMatchAsync_WhenOfferedMatchExists_UpdatesOnlyMatchStatus()
    {
        // Arrange
        var context = CreateContext();
        var matchId = Guid.NewGuid();
        RideMatch? updatedMatch = null;

        context.RideMatchRepository
            .Setup(repository => repository.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RideMatch
            {
                MatchId = matchId,
                RequestId = Guid.NewGuid(),
                DriverId = Guid.NewGuid(),
                Status = "OFFERED",
                OfferedAt = DateTime.UtcNow,
            });
        context.RideMatchRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()))
            .Callback<RideMatch, CancellationToken>((match, _) => updatedMatch = match)
            .ReturnsAsync((RideMatch match, CancellationToken _) => match);

        // Act
        var result = await context.Service.RejectMatchAsync(matchId);

        // Assert
        Assert.True(result);
        Assert.Equal("REJECTED", updatedMatch?.Status);
        context.RideRequestRepository.Verify(
            repository => repository.UpdateStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelMatchAsync_WhenMatchExists_UpdatesMatchStatusToCancelled()
    {
        // Arrange
        var context = CreateContext();
        var matchId = Guid.NewGuid();
        RideMatch? updatedMatch = null;

        context.RideMatchRepository
            .Setup(repository => repository.GetByIdAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RideMatch
            {
                MatchId = matchId,
                RequestId = Guid.NewGuid(),
                DriverId = Guid.NewGuid(),
                Status = "ACCEPTED",
                OfferedAt = DateTime.UtcNow,
            });
        context.RideMatchRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<RideMatch>(), It.IsAny<CancellationToken>()))
            .Callback<RideMatch, CancellationToken>((match, _) => updatedMatch = match)
            .ReturnsAsync((RideMatch match, CancellationToken _) => match);

        // Act
        var result = await context.Service.CancelMatchAsync(matchId);

        // Assert
        Assert.True(result);
        Assert.Equal("CANCELLED", updatedMatch?.Status);
    }

    private static TestContext CreateContext()
    {
        var rideRequestRepository = new Mock<IPassengerRideRequestRepository>(MockBehavior.Strict);
        var rideMatchRepository = new Mock<IRideMatchRepository>(MockBehavior.Strict);
        var driverRepository = new Mock<IDriverRepository>(MockBehavior.Strict);
        var driverLocationRepository = new Mock<IDriverLocationRepository>(MockBehavior.Strict);
        var availabilitySessionRepository = new Mock<IDriverAvailabilitySessionRepository>(MockBehavior.Strict);
        var driverVehicleRepository = new Mock<IDriverVehicleRepository>(MockBehavior.Strict);
        var transportModeRepository = new Mock<ITransportModeRepository>(MockBehavior.Strict);

        return new TestContext(
            new RideMatchingService(
                rideRequestRepository.Object,
                rideMatchRepository.Object,
                driverRepository.Object,
                driverLocationRepository.Object,
                availabilitySessionRepository.Object,
                driverVehicleRepository.Object,
                transportModeRepository.Object),
            rideRequestRepository,
            rideMatchRepository,
            driverRepository,
            driverLocationRepository,
            availabilitySessionRepository,
            driverVehicleRepository,
            transportModeRepository);
    }

    private static PassengerRideRequest CreateActiveRequest(int passengerCount) =>
        new()
        {
            RequestId = Guid.NewGuid(),
            PassengerUserId = Guid.NewGuid(),
            PickupName = "Pickup",
            PickupLatitude = 14.55,
            PickupLongitude = 121.02,
            DropoffName = "Dropoff",
            DropoffLatitude = 14.6,
            DropoffLongitude = 121.05,
            PassengerCount = passengerCount,
            Status = "SEARCHING",
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };

    private static backend.Models.Database.Driver CreateDriver(Guid driverId, bool isAvailable) =>
        new()
        {
            DriverId = driverId,
            UserId = Guid.NewGuid(),
            VerificationStatus = "VERIFIED",
            IsAvailable = isAvailable,
        };

    private static DriverAvailabilitySession CreateSession(Guid driverId, Guid vehicleId, int availableSeats) =>
        new()
        {
            SessionId = NextSessionId(),
            DriverId = driverId,
            VehicleId = vehicleId,
            AvailableSeats = availableSeats,
            MaximumDetourMeters = 1000,
            Status = "AVAILABLE",
            StartedAt = DateTime.UtcNow,
        };

    private static long NextSessionId() => Interlocked.Increment(ref _nextSessionId);

    private static long _nextSessionId;

    private static DriverLocation CreateLocation(Guid driverId) =>
        new()
        {
            DriverId = driverId,
            Latitude = 14.6,
            Longitude = 121.0,
            UpdatedAt = DateTime.UtcNow,
        };

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
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private sealed record TestContext(
        RideMatchingService Service,
        Mock<IPassengerRideRequestRepository> RideRequestRepository,
        Mock<IRideMatchRepository> RideMatchRepository,
        Mock<IDriverRepository> DriverRepository,
        Mock<IDriverLocationRepository> DriverLocationRepository,
        Mock<IDriverAvailabilitySessionRepository> AvailabilitySessionRepository,
        Mock<IDriverVehicleRepository> DriverVehicleRepository,
        Mock<ITransportModeRepository> TransportModeRepository);
}
