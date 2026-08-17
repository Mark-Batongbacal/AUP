using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Trip;

public sealed class TripServiceTests
{
    [Fact]
    public async Task CreateTripSearchAsync_WhenInputIsValid_AddsNormalizedTripSearch()
    {
        // Arrange
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var requestedAt = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
        TripSearch? capturedSearch = null;

        context.TripSearchRepository
            .Setup(repository => repository.AddAsync(It.IsAny<TripSearch>(), It.IsAny<CancellationToken>()))
            .Callback<TripSearch, CancellationToken>((search, _) => capturedSearch = search)
            .ReturnsAsync((TripSearch search, CancellationToken _) =>
            {
                search.TripSearchId = Guid.NewGuid();
                return search;
            });

        // Act
        var result = await context.Service.CreateTripSearchAsync(
            userId,
            "  Ayala  ",
            14.556,
            121.023,
            "  BGC  ",
            14.55,
            121.05,
            passengerCount: 2,
            budget: 250,
            preference: "  cheapest  ",
            requestedAt: requestedAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedSearch, result);
        Assert.Equal(userId, capturedSearch?.UserId);
        Assert.Equal("Ayala", capturedSearch?.OriginName);
        Assert.Equal("BGC", capturedSearch?.DestinationName);
        Assert.Equal(2, capturedSearch?.PassengerCount);
        Assert.Equal(250, capturedSearch?.Budget);
        Assert.Equal("cheapest", capturedSearch?.Preference);
        Assert.Equal(requestedAt, capturedSearch?.RequestedAt);

        context.TripSearchRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TripSearch>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTripSearchAsync_WhenOriginIsMissing_ReturnsNullWithoutAddingSearch()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Service.CreateTripSearchAsync(
            Guid.NewGuid(),
            " ",
            14.556,
            121.023,
            "BGC",
            14.55,
            121.05);

        // Assert
        Assert.Null(result);
        context.TripSearchRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TripSearch>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRecommendationDetailsAsync_WhenRecommendationExists_ReturnsLegsInRepositoryOrder()
    {
        // Arrange
        var context = CreateContext();
        var recommendationId = Guid.NewGuid();
        var recommendation = new RouteRecommendation
        {
            RecommendationId = recommendationId,
            TripSearchId = Guid.NewGuid(),
            RecommendationType = "COMMUTE",
            RankNumber = 1,
            TotalFare = 42,
            TotalMinutes = 35,
            WalkingDistanceMeters = 300,
            TransferCount = 1,
            GeneratedAt = new DateTime(2026, 4, 1, 9, 5, 0, DateTimeKind.Utc),
        };
        var legs = new List<RecommendationLeg>
        {
            CreateLeg(recommendationId, legOrder: 1, fromName: "Origin", toName: "Stop A"),
            CreateLeg(recommendationId, legOrder: 2, fromName: "Stop A", toName: "Destination"),
        };

        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendation);
        context.RecommendationLegRepository
            .Setup(repository => repository.GetOrderedByRecommendationAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legs);

        // Act
        var result = await context.Service.GetRecommendationDetailsAsync(recommendationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recommendationId, result.RecommendationId);
        Assert.Equal([1, 2], result.Legs.Select(leg => leg.LegOrder));
        Assert.Equal("Origin", result.Legs[0].FromName);
        Assert.Equal("Destination", result.Legs[1].ToName);

        context.RouteRecommendationRepository.Verify(
            repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.RecommendationLegRepository.Verify(
            repository => repository.GetOrderedByRecommendationAsync(recommendationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecommendationDetailsAsync_WhenRecommendationDoesNotExist_ReturnsNullWithoutLoadingLegs()
    {
        // Arrange
        var context = CreateContext();
        var recommendationId = Guid.NewGuid();

        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RouteRecommendation?)null);

        // Act
        var result = await context.Service.GetRecommendationDetailsAsync(recommendationId);

        // Assert
        Assert.Null(result);
        context.RecommendationLegRepository.Verify(
            repository => repository.GetOrderedByRecommendationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartPassengerTripAsync_WhenRecommendationExists_CreatesInProgressTrip()
    {
        // Arrange
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        PassengerTrip? capturedTrip = null;

        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteRecommendation
            {
                RecommendationId = recommendationId,
                TripSearchId = Guid.NewGuid(),
                RecommendationType = "COMMUTE",
                RankNumber = 1,
                TotalFare = 20,
                TotalMinutes = 30,
                WalkingDistanceMeters = 100,
                TransferCount = 0,
                GeneratedAt = DateTime.UtcNow,
            });
        context.PassengerTripRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PassengerTrip>(), It.IsAny<CancellationToken>()))
            .Callback<PassengerTrip, CancellationToken>((trip, _) => capturedTrip = trip)
            .ReturnsAsync((PassengerTrip trip, CancellationToken _) =>
            {
                trip.PassengerTripId = Guid.NewGuid();
                return trip;
            });

        // Act
        var result = await context.Service.StartPassengerTripAsync(userId, recommendationId, startedAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedTrip, result);
        Assert.Equal(userId, capturedTrip?.UserId);
        Assert.Equal(recommendationId, capturedTrip?.RecommendationId);
        Assert.Equal(1, capturedTrip?.CurrentLegOrder);
        Assert.Equal("IN_PROGRESS", capturedTrip?.Status);
        Assert.Equal(startedAt, capturedTrip?.StartedAt);
    }

    [Fact]
    public async Task StartPassengerTripAsync_WhenRecommendationDoesNotExist_ReturnsNullWithoutCreatingTrip()
    {
        // Arrange
        var context = CreateContext();
        var recommendationId = Guid.NewGuid();

        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RouteRecommendation?)null);

        // Act
        var result = await context.Service.StartPassengerTripAsync(Guid.NewGuid(), recommendationId);

        // Assert
        Assert.Null(result);
        context.PassengerTripRepository.Verify(
            repository => repository.AddAsync(It.IsAny<PassengerTrip>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCurrentLegAsync_WhenTripExists_UsesExistingStatusAndNewLegOrder()
    {
        // Arrange
        var context = CreateContext();
        var passengerTripId = Guid.NewGuid();
        var trip = new PassengerTrip
        {
            PassengerTripId = passengerTripId,
            UserId = Guid.NewGuid(),
            RecommendationId = Guid.NewGuid(),
            CurrentLegOrder = 1,
            Status = "BOARDING",
        };

        context.PassengerTripRepository
            .Setup(repository => repository.GetByIdAsync(passengerTripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trip);
        context.PassengerTripRepository
            .Setup(repository => repository.UpdateStatusAndCurrentLegAsync(
                passengerTripId,
                "BOARDING",
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.UpdateCurrentLegAsync(passengerTripId, 2);

        // Assert
        Assert.True(result);
        context.PassengerTripRepository.Verify(
            repository => repository.UpdateStatusAndCurrentLegAsync(
                passengerTripId,
                "BOARDING",
                2,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPendingTripAlertsAsync_WhenAlertsExist_ReturnsOnlyUntriggeredAlerts()
    {
        // Arrange
        var context = CreateContext();
        var passengerTripId = Guid.NewGuid();
        var pendingAlert = new TripAlert
        {
            AlertId = Guid.NewGuid(),
            PassengerTripId = passengerTripId,
            AlertType = "ARRIVAL",
            Message = "Prepare to alight",
            IsTriggered = false,
        };
        var triggeredAlert = new TripAlert
        {
            AlertId = Guid.NewGuid(),
            PassengerTripId = passengerTripId,
            AlertType = "BOARD",
            Message = "Board now",
            IsTriggered = true,
        };

        context.TripAlertRepository
            .Setup(repository => repository.GetByPassengerTripAsync(passengerTripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendingAlert, triggeredAlert]);

        // Act
        var result = await context.Service.GetPendingTripAlertsAsync(passengerTripId);

        // Assert
        var alert = Assert.Single(result);
        Assert.Same(pendingAlert, alert);
        context.TripAlertRepository.Verify(
            repository => repository.GetByPassengerTripAsync(passengerTripId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTripAlertAsync_WhenLegBelongsToTripRecommendation_AddsAlert()
    {
        // Arrange
        var context = CreateContext();
        var passengerTripId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        TripAlert? capturedAlert = null;

        context.PassengerTripRepository
            .Setup(repository => repository.GetByIdAsync(passengerTripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PassengerTrip
            {
                PassengerTripId = passengerTripId,
                UserId = Guid.NewGuid(),
                RecommendationId = recommendationId,
                Status = "IN_PROGRESS",
            });
        context.RecommendationLegRepository
            .Setup(repository => repository.GetByIdAsync(legId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLeg(recommendationId, 1, "A", "B", legId));
        context.TripAlertRepository
            .Setup(repository => repository.AddAsync(It.IsAny<TripAlert>(), It.IsAny<CancellationToken>()))
            .Callback<TripAlert, CancellationToken>((alert, _) => capturedAlert = alert)
            .ReturnsAsync((TripAlert alert, CancellationToken _) =>
            {
                alert.AlertId = Guid.NewGuid();
                return alert;
            });

        // Act
        var result = await context.Service.CreateTripAlertAsync(
            passengerTripId,
            "  ARRIVAL  ",
            "  Get ready  ",
            legId,
            title: "  Stop alert  ",
            triggerDistanceMeters: 100);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedAlert, result);
        Assert.Equal(passengerTripId, capturedAlert?.PassengerTripId);
        Assert.Equal(legId, capturedAlert?.LegId);
        Assert.Equal("ARRIVAL", capturedAlert?.AlertType);
        Assert.Equal("Get ready", capturedAlert?.Message);
        Assert.Equal("Stop alert", capturedAlert?.Title);
        Assert.False(capturedAlert?.IsTriggered);
    }

    [Fact]
    public async Task CreateTripAlertAsync_WhenLegDoesNotBelongToTripRecommendation_ReturnsNullWithoutAddingAlert()
    {
        // Arrange
        var context = CreateContext();
        var passengerTripId = Guid.NewGuid();
        var legId = Guid.NewGuid();

        context.PassengerTripRepository
            .Setup(repository => repository.GetByIdAsync(passengerTripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PassengerTrip
            {
                PassengerTripId = passengerTripId,
                UserId = Guid.NewGuid(),
                RecommendationId = Guid.NewGuid(),
                Status = "IN_PROGRESS",
            });
        context.RecommendationLegRepository
            .Setup(repository => repository.GetByIdAsync(legId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLeg(Guid.NewGuid(), 1, "A", "B", legId));

        // Act
        var result = await context.Service.CreateTripAlertAsync(
            passengerTripId,
            "ARRIVAL",
            "Get ready",
            legId);

        // Assert
        Assert.Null(result);
        context.TripAlertRepository.Verify(
            repository => repository.AddAsync(It.IsAny<TripAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MarkTripAlertTriggeredAsync_WhenAlertIdIsValid_DelegatesToRepository()
    {
        // Arrange
        var context = CreateContext();
        var alertId = Guid.NewGuid();
        var triggeredAt = new DateTime(2026, 4, 1, 11, 0, 0, DateTimeKind.Utc);

        context.TripAlertRepository
            .Setup(repository => repository.UpdateTriggerStateAsync(alertId, true, triggeredAt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.MarkTripAlertTriggeredAsync(alertId, triggeredAt);

        // Assert
        Assert.True(result);
        context.TripAlertRepository.Verify(
            repository => repository.UpdateTriggerStateAsync(alertId, true, triggeredAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TestContext CreateContext()
    {
        var tripSearchRepository = new Mock<ITripSearchRepository>(MockBehavior.Strict);
        var routeRecommendationRepository = new Mock<IRouteRecommendationRepository>(MockBehavior.Strict);
        var recommendationLegRepository = new Mock<IRecommendationLegRepository>(MockBehavior.Strict);
        var passengerTripRepository = new Mock<IPassengerTripRepository>(MockBehavior.Strict);
        var tripAlertRepository = new Mock<ITripAlertRepository>(MockBehavior.Strict);

        return new TestContext(
            new TripService(
                tripSearchRepository.Object,
                routeRecommendationRepository.Object,
                recommendationLegRepository.Object,
                passengerTripRepository.Object,
                tripAlertRepository.Object),
            tripSearchRepository,
            routeRecommendationRepository,
            recommendationLegRepository,
            passengerTripRepository,
            tripAlertRepository);
    }

    private static RecommendationLeg CreateLeg(
        Guid recommendationId,
        int legOrder,
        string fromName,
        string toName,
        Guid? legId = null) =>
        new()
        {
            LegId = legId ?? Guid.NewGuid(),
            RecommendationId = recommendationId,
            LegOrder = legOrder,
            TransportModeId = 1,
            TransportMode = new TransportMode
            {
                TransportModeId = 1,
                Code = "BUS",
                Name = "Bus",
                IsMotorized = true,
                AllowsLiveDriver = true,
                IsActive = true,
            },
            FromName = fromName,
            ToName = toName,
            EstimatedMinutes = 10,
            EstimatedFare = 15,
            CreatedAt = DateTime.UtcNow,
        };

    private sealed record TestContext(
        TripService Service,
        Mock<ITripSearchRepository> TripSearchRepository,
        Mock<IRouteRecommendationRepository> RouteRecommendationRepository,
        Mock<IRecommendationLegRepository> RecommendationLegRepository,
        Mock<IPassengerTripRepository> PassengerTripRepository,
        Mock<ITripAlertRepository> TripAlertRepository);
}
