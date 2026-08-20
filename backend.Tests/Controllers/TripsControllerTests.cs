using System.Security.Claims;
using backend.Controllers;
using backend.Models.Database;
using backend.Models.Trips;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class TripsControllerTests
{
    [Fact]
    public async Task GetById_WhenTripExistsForCurrentUser_ReturnsTripDetails()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var details = CreateTripDetails(tripId, userId);
        service
            .Setup(item => item.GetPassengerTripDetailsAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var controller = CreateController(service, userId);
        var response = await controller.GetById(tripId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(details, ok.Value);
    }

    [Fact]
    public async Task GetById_WhenTripDoesNotExist_ReturnsNotFound()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        service
            .Setup(item => item.GetPassengerTripDetailsAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PassengerTripDetailsDto?)null);

        var controller = CreateController(service, userId);
        var response = await controller.GetById(tripId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task StartTrip_WhenRecommendationBelongsToCurrentUser_ReturnsCreatedTrip()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var searchId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 8, 18, 11, 0, 0, DateTimeKind.Utc);
        var details = CreateTripDetails(tripId, userId, recommendationId);

        service
            .Setup(item => item.GetRecommendationByIdAsync(recommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteRecommendation
            {
                RecommendationId = recommendationId,
                TripSearchId = searchId,
                RecommendationType = "COMMUTE",
                RankNumber = 1,
                TotalFare = 30,
                TotalMinutes = 20,
                WalkingDistanceMeters = 100,
                TransferCount = 0,
                GeneratedAt = DateTime.UtcNow,
            });
        service
            .Setup(item => item.GetTripSearchByIdAsync(searchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearch
            {
                TripSearchId = searchId,
                UserId = userId,
                OriginName = "Origin",
                OriginLatitude = 14.55,
                OriginLongitude = 121.02,
                DestinationName = "Destination",
                DestinationLatitude = 14.6,
                DestinationLongitude = 121.05,
                PassengerCount = 1,
            });
        service
            .Setup(item => item.StartPassengerTripAsync(
                userId,
                recommendationId,
                startedAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PassengerTrip
            {
                PassengerTripId = tripId,
                UserId = userId,
                RecommendationId = recommendationId,
                CurrentLegOrder = 1,
                Status = "IN_PROGRESS",
                StartedAt = startedAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        service
            .Setup(item => item.GetPassengerTripDetailsAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var controller = CreateController(service, userId);
        var response = await controller.StartTrip(
            new StartTripRequest(recommendationId, startedAt),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(TripsController.GetById), created.ActionName);
        Assert.Equal(tripId, created.RouteValues?["tripId"]);
        Assert.Same(details, created.Value);
    }

    [Fact]
    public async Task GetAlerts_WhenTripExistsForCurrentUser_ReturnsTripAlerts()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var alert = new TripAlertDto(
            Guid.NewGuid(),
            tripId,
            null,
            null,
            null,
            "ARRIVAL",
            "Approaching stop",
            "Prepare to alight",
            100,
            false,
            null,
            DateTime.UtcNow);
        service
            .Setup(item => item.GetPassengerTripDetailsAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTripDetails(tripId, userId, alerts: [alert]));

        var controller = CreateController(service, userId);
        var response = await controller.GetAlerts(tripId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<TripAlertDto>>(ok.Value);
        Assert.Same(alert, Assert.Single(body));
    }

    [Fact]
    public async Task GetRecent_WhenAuthenticated_ReturnsCurrentUsersRecentJourneys()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var journey = new PassengerTripHistoryItemDto(
            Guid.NewGuid(),
            "COMPLETED",
            "Origin",
            "Destination",
            15,
            120,
            15.1,
            120.1,
            DateTime.UtcNow.AddMinutes(-30),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(-35),
            null,
            true,
            1,
            "OFF_ROUTE",
            DateTime.UtcNow.AddMinutes(-10));
        service
            .Setup(item => item.GetPassengerTripHistoryAsync(
                userId,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([journey]);

        var controller = CreateController(service, userId);
        var response = await controller.GetRecent(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<PassengerTripHistoryItemDto>>(ok.Value);
        Assert.Same(journey, Assert.Single(body));
    }

    [Fact]
    public async Task GetRecent_WhenGuest_ReturnsUnauthorizedWithoutServiceLookup()
    {
        var service = new Mock<ITripService>(MockBehavior.Strict);
        var controller = CreateController(service, Guid.Empty);

        var response = await controller.GetRecent(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
        service.Verify(
            item => item.GetPassengerTripHistoryAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TripsController CreateController(Mock<ITripService> service, Guid userId)
    {
        var controller = new TripsController(service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "ApiKey")),
            },
        };

        return controller;
    }

    private static PassengerTripDetailsDto CreateTripDetails(
        Guid tripId,
        Guid userId,
        Guid? recommendationId = null,
        IReadOnlyList<TripAlertDto>? alerts = null) =>
        new(
            tripId,
            userId,
            recommendationId ?? Guid.NewGuid(),
            1,
            "IN_PROGRESS",
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            alerts ?? []);
}
