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

    [Fact]
    public async Task GetPassengerTripHistoryAsync_WhenRecentOnly_MapsCompletedCancelledAndRerouteMetadata()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var searchId = Guid.NewGuid();
        var completed = new TripSession
        {
            TripSessionId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = recommendationId,
            CurrentNavigationState = TripNavigationState.Arrived,
            OriginLatitude = 15,
            OriginLongitude = 120,
            DestinationLatitude = 15.1,
            DestinationLongitude = 120.1,
            DestinationName = "Market",
            StartedAt = new DateTime(2026, 8, 20, 1, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 8, 20, 1, 30, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 20, 0, 55, 0, DateTimeKind.Utc),
            RerouteCount = 2,
            LastRerouteReason = "OFF_ROUTE",
            LastRerouteAt = new DateTime(2026, 8, 20, 1, 10, 0, DateTimeKind.Utc),
        };
        var cancelled = new TripSession
        {
            TripSessionId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = recommendationId,
            CurrentNavigationState = TripNavigationState.Cancelled,
            OriginLatitude = 15,
            OriginLongitude = 120,
            DestinationLatitude = 15.2,
            DestinationLongitude = 120.2,
            DestinationName = "Terminal",
            CancelledAt = new DateTime(2026, 8, 20, 2, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 20, 1, 50, 0, DateTimeKind.Utc),
        };

        context.TripSessionRepository
            .Setup(repository => repository.GetOwnedRecentHistoryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([completed, cancelled]);
        context.PassengerTripRepository
            .Setup(repository => repository.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteRecommendation
            {
                RecommendationId = recommendationId,
                TripSearchId = searchId,
                RecommendationType = "efficient",
                RankNumber = 1,
                TotalFare = 35,
                TotalMinutes = 25,
                WalkingDistanceMeters = 200,
                TransferCount = 1,
                GeneratedAt = DateTime.UtcNow,
            });
        context.TripSearchRepository
            .Setup(repository => repository.GetByIdAsync(searchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearch
            {
                TripSearchId = searchId,
                UserId = userId,
                OriginName = "Origin",
                DestinationName = "Original destination",
                PassengerCount = 1,
            });
        context.RecommendationLegRepository
            .Setup(repository => repository.GetOrderedByRecommendationAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateLeg(recommendationId, 0, "Origin", "Market")]);

        var result = await context.Service.GetPassengerTripHistoryAsync(userId, recentOnly: true);

        Assert.Equal(2, result.Count);
        var completedItem = Assert.Single(result, item => item.PassengerTripId == completed.TripSessionId);
        Assert.Equal("COMPLETED", completedItem.Status);
        Assert.True(completedItem.Rerouted);
        Assert.Equal(2, completedItem.RerouteCount);
        Assert.Equal("OFF_ROUTE", completedItem.LastRerouteReason);
        Assert.Equal(completed.LastRerouteAt, completedItem.LastRerouteAt);
        var cancelledItem = Assert.Single(result, item => item.PassengerTripId == cancelled.TripSessionId);
        Assert.Equal("CANCELLED", cancelledItem.Status);
        Assert.False(cancelledItem.Rerouted);
        Assert.Equal(cancelled.CancelledAt, cancelledItem.CompletedAt);
    }

    [Fact]
    public async Task GetPassengerTripHistoryAsync_WhenRecentOnly_IncludesLegacyCompletedAndCancelledTrips()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var completedRecommendationId = Guid.NewGuid();
        var completedSearchId = Guid.NewGuid();
        var cancelledRecommendationId = Guid.NewGuid();
        var cancelledSearchId = Guid.NewGuid();
        var inProgressRecommendationId = Guid.NewGuid();
        var completed = new PassengerTrip
        {
            PassengerTripId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = completedRecommendationId,
            Recommendation = CreateRecommendation(completedRecommendationId, completedSearchId),
            Status = "COMPLETED",
            StartedAt = new DateTime(2026, 8, 19, 8, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 19, 7, 55, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 8, 19, 8, 35, 0, DateTimeKind.Utc),
        };
        var cancelled = new PassengerTrip
        {
            PassengerTripId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = cancelledRecommendationId,
            Recommendation = CreateRecommendation(cancelledRecommendationId, cancelledSearchId),
            Status = "CANCELLED",
            StartedAt = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 20, 8, 55, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 8, 20, 9, 5, 0, DateTimeKind.Utc),
        };
        var inProgress = new PassengerTrip
        {
            PassengerTripId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = inProgressRecommendationId,
            Status = "IN_PROGRESS",
            StartedAt = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 20, 9, 55, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 8, 20, 10, 5, 0, DateTimeKind.Utc),
        };

        context.TripSessionRepository
            .Setup(repository => repository.GetOwnedRecentHistoryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        context.PassengerTripRepository
            .Setup(repository => repository.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([completed, cancelled, inProgress]);
        SetupRecommendationDetails(
            context,
            completedRecommendationId,
            completedSearchId,
            userId,
            "Home",
            "Office",
            15.1,
            120.1,
            15.2,
            120.2);
        SetupRecommendationDetails(
            context,
            cancelledRecommendationId,
            cancelledSearchId,
            userId,
            "Campus",
            "Clinic",
            15.3,
            120.3,
            15.4,
            120.4);

        var result = await context.Service.GetPassengerTripHistoryAsync(userId, recentOnly: true);

        Assert.Equal(2, result.Count);
        Assert.Equal(cancelled.PassengerTripId, result[0].PassengerTripId);
        Assert.Equal("CANCELLED", result[0].Status);
        Assert.Equal(cancelled.UpdatedAt, result[0].CompletedAt);
        Assert.Equal("Campus", result[0].OriginName);
        Assert.Equal("Clinic", result[0].DestinationName);
        Assert.Equal(cancelledRecommendationId, result[0].Recommendation?.RecommendationId);
        Assert.Equal(2, result[0].Recommendation?.Legs.Count);
        Assert.Equal(completed.PassengerTripId, result[1].PassengerTripId);
        Assert.Equal("COMPLETED", result[1].Status);
        Assert.Equal(completed.UpdatedAt, result[1].CompletedAt);
        Assert.Equal("Home", result[1].OriginName);
        Assert.Equal("Office", result[1].DestinationName);
        Assert.DoesNotContain(result, item => item.PassengerTripId == inProgress.PassengerTripId);
    }

    [Fact]
    public async Task GetPassengerTripHistoryAsync_WhenRecentOnly_DeduplicatesLegacyTripWithMatchingSession()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var searchId = Guid.NewGuid();
        var session = new TripSession
        {
            TripSessionId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = recommendationId,
            CurrentNavigationState = TripNavigationState.Arrived,
            OriginLatitude = 15,
            OriginLongitude = 120,
            DestinationLatitude = 15.1,
            DestinationLongitude = 120.1,
            DestinationName = "Market",
            StartedAt = new DateTime(2026, 8, 20, 1, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 8, 20, 1, 30, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 20, 0, 55, 0, DateTimeKind.Utc),
        };
        var legacyTrip = new PassengerTrip
        {
            PassengerTripId = Guid.NewGuid(),
            UserId = userId,
            RecommendationId = recommendationId,
            Status = "COMPLETED",
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.CompletedAt.Value,
        };

        context.TripSessionRepository
            .Setup(repository => repository.GetOwnedRecentHistoryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        context.PassengerTripRepository
            .Setup(repository => repository.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([legacyTrip]);
        SetupRecommendationDetails(
            context,
            recommendationId,
            searchId,
            userId,
            "Origin",
            "Market",
            15,
            120,
            15.1,
            120.1);

        var result = await context.Service.GetPassengerTripHistoryAsync(userId, recentOnly: true);

        var item = Assert.Single(result);
        Assert.Equal(session.TripSessionId, item.PassengerTripId);
        Assert.Equal("COMPLETED", item.Status);
    }

    [Fact]
    public async Task GetPassengerTripHistoryAsync_WhenUserIsEmpty_ReturnsEmptyWithoutRepositoryLookup()
    {
        var context = CreateContext();

        var result = await context.Service.GetPassengerTripHistoryAsync(Guid.Empty, recentOnly: true);

        Assert.Empty(result);
        context.TripSessionRepository.Verify(
            repository => repository.GetOwnedRecentHistoryAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        context.PassengerTripRepository.Verify(
            repository => repository.GetByUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestContext CreateContext()
    {
        var tripSearchRepository = new Mock<ITripSearchRepository>(MockBehavior.Strict);
        var routeRecommendationRepository = new Mock<IRouteRecommendationRepository>(MockBehavior.Strict);
        var recommendationLegRepository = new Mock<IRecommendationLegRepository>(MockBehavior.Strict);
        var passengerTripRepository = new Mock<IPassengerTripRepository>(MockBehavior.Strict);
        var tripAlertRepository = new Mock<ITripAlertRepository>(MockBehavior.Strict);
        var tripSessionRepository = new Mock<ITripSessionRepository>(MockBehavior.Strict);

        return new TestContext(
            new TripService(
                tripSearchRepository.Object,
                routeRecommendationRepository.Object,
                recommendationLegRepository.Object,
                passengerTripRepository.Object,
                tripAlertRepository.Object,
                tripSessionRepository.Object),
            tripSearchRepository,
            routeRecommendationRepository,
            recommendationLegRepository,
            passengerTripRepository,
            tripAlertRepository,
            tripSessionRepository);
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

    private static RouteRecommendation CreateRecommendation(Guid recommendationId, Guid searchId) =>
        new()
        {
            RecommendationId = recommendationId,
            TripSearchId = searchId,
            RecommendationType = "efficient",
            RankNumber = 1,
            TotalFare = 42,
            TotalMinutes = 30,
            WalkingDistanceMeters = 250,
            TransferCount = 1,
            GeneratedAt = DateTime.UtcNow,
        };

    private static void SetupRecommendationDetails(
        TestContext context,
        Guid recommendationId,
        Guid searchId,
        Guid userId,
        string originName,
        string destinationName,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        var recommendation = CreateRecommendation(recommendationId, searchId);

        context.RouteRecommendationRepository
            .Setup(repository => repository.GetByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendation);
        context.TripSearchRepository
            .Setup(repository => repository.GetByIdAsync(searchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearch
            {
                TripSearchId = searchId,
                UserId = userId,
                OriginName = originName,
                OriginLatitude = originLatitude,
                OriginLongitude = originLongitude,
                DestinationName = destinationName,
                DestinationLatitude = destinationLatitude,
                DestinationLongitude = destinationLongitude,
                PassengerCount = 1,
            });
        context.RecommendationLegRepository
            .Setup(repository => repository.GetOrderedByRecommendationAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateLeg(recommendationId, 1, originName, "Transfer"),
                CreateLeg(recommendationId, 2, "Transfer", destinationName),
            ]);
    }

    private sealed record TestContext(
        TripService Service,
        Mock<ITripSearchRepository> TripSearchRepository,
        Mock<IRouteRecommendationRepository> RouteRecommendationRepository,
        Mock<IRecommendationLegRepository> RecommendationLegRepository,
        Mock<IPassengerTripRepository> PassengerTripRepository,
        Mock<ITripAlertRepository> TripAlertRepository,
        Mock<ITripSessionRepository> TripSessionRepository);
}
