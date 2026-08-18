using System.Security.Claims;
using backend.Controllers;
using backend.Models.Database;
using backend.Models.RideMatching;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class RideMatchingControllerTests
{
    [Fact]
    public async Task CreateRideRequest_WhenRequestIsValid_ReturnsCreatedRequest()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var passengerUserId = Guid.NewGuid();
        var requestedAt = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc);
        var expiresAt = requestedAt.AddMinutes(10);
        var requestId = Guid.NewGuid();
        service
            .Setup(item => item.CreateRideRequestAsync(
                passengerUserId,
                "Pickup",
                14.55,
                121.02,
                "Dropoff",
                14.6,
                121.05,
                2,
                1,
                250,
                requestedAt,
                expiresAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRideRequest(
                requestId,
                passengerUserId,
                "SEARCHING",
                requestedAt,
                expiresAt));

        var controller = CreateController(service, passengerUserId);
        var response = await controller.CreateRideRequest(
            new CreateRideRequestRequest(
                "Pickup",
                14.55,
                121.02,
                "Dropoff",
                14.6,
                121.05,
                2,
                1,
                250,
                requestedAt,
                expiresAt),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(RideMatchingController.GetRideRequest), created.ActionName);
        Assert.Equal(requestId, created.RouteValues?["requestId"]);
        var body = Assert.IsType<RideRequestDetailsDto>(created.Value);
        Assert.Equal(requestId, body.RequestId);
        Assert.Equal(passengerUserId, body.PassengerUserId);
        Assert.Equal("SEARCHING", body.Status);
    }

    [Fact]
    public async Task GetRideRequest_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var requestId = Guid.NewGuid();
        service
            .Setup(item => item.GetRideRequestByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PassengerRideRequest?)null);

        var controller = new RideMatchingController(service.Object);
        var response = await controller.GetRideRequest(requestId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task CreateRideMatch_WhenRequestIsSearching_ReturnsCreatedMatch()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var requestId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        service
            .Setup(item => item.GetRideRequestByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRideRequest(requestId, Guid.NewGuid(), "SEARCHING"));
        service
            .Setup(item => item.CreateRideMatchAsync(
                requestId,
                driverId,
                vehicleId,
                100,
                50,
                3,
                12,
                150,
                0.92m,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RideMatch
            {
                MatchId = matchId,
                RequestId = requestId,
                DriverId = driverId,
                VehicleId = vehicleId,
                PickupDistanceMeters = 100,
                DetourDistanceMeters = 50,
                EstimatedPickupMinutes = 3,
                EstimatedTripMinutes = 12,
                EstimatedFare = 150,
                MatchScore = 0.92m,
                Status = "OFFERED",
                OfferedAt = DateTime.UtcNow,
            });

        var controller = new RideMatchingController(service.Object);
        var response = await controller.CreateRideMatch(
            requestId,
            new CreateRideMatchRequest(
                driverId,
                vehicleId,
                100,
                50,
                3,
                12,
                150,
                0.92m),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(RideMatchingController.GetMatch), created.ActionName);
        Assert.Equal(matchId, created.RouteValues?["matchId"]);
        var body = Assert.IsType<RideMatchDetailsDto>(created.Value);
        Assert.Equal("OFFERED", body.Status);
    }

    [Fact]
    public async Task CreateRideMatch_WhenRequestIsNotSearching_ReturnsConflictWithoutCreatingMatch()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var requestId = Guid.NewGuid();
        service
            .Setup(item => item.GetRideRequestByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRideRequest(requestId, Guid.NewGuid(), "MATCHED"));

        var controller = new RideMatchingController(service.Object);
        var response = await controller.CreateRideMatch(
            requestId,
            new CreateRideMatchRequest(Guid.NewGuid()),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response.Result);
        service.Verify(item => item.CreateRideMatchAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptMatch_WhenMatchIsNotOffered_ReturnsConflictWithoutAccepting()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var matchId = Guid.NewGuid();
        service
            .Setup(item => item.GetMatchDetailsAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMatchDetails(matchId, "ACCEPTED"));

        var controller = new RideMatchingController(service.Object);
        var response = await controller.AcceptMatch(matchId, null, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response);
        service.Verify(item => item.AcceptMatchAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMatch_WhenMatchDoesNotExist_ReturnsNotFound()
    {
        var service = new Mock<IRideMatchingService>(MockBehavior.Strict);
        var matchId = Guid.NewGuid();
        service
            .Setup(item => item.GetMatchDetailsAsync(matchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RideMatchDetailsDto?)null);

        var controller = new RideMatchingController(service.Object);
        var response = await controller.GetMatch(matchId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    private static RideMatchingController CreateController(
        Mock<IRideMatchingService> service,
        Guid userId)
    {
        var controller = new RideMatchingController(service.Object);
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

    private static PassengerRideRequest CreateRideRequest(
        Guid requestId,
        Guid passengerUserId,
        string status,
        DateTime? requestedAt = null,
        DateTime? expiresAt = null) =>
        new()
        {
            RequestId = requestId,
            PassengerUserId = passengerUserId,
            PickupName = "Pickup",
            PickupLatitude = 14.55,
            PickupLongitude = 121.02,
            DropoffName = "Dropoff",
            DropoffLatitude = 14.6,
            DropoffLongitude = 121.05,
            PassengerCount = 2,
            Status = status,
            RequestedAt = requestedAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10),
            UpdatedAt = DateTime.UtcNow,
        };

    private static RideMatchDetailsDto CreateMatchDetails(Guid matchId, string status) =>
        new(
            matchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            status,
            DateTime.UtcNow,
            status == "ACCEPTED" ? DateTime.UtcNow : null,
            null,
            null,
            null,
            null,
            null);
}
