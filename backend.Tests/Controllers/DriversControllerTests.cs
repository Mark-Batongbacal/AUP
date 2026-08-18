using backend.Controllers;
using backend.Models.Database;
using backend.Models.Drivers;
using backend.Services;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class DriversControllerTests
{
    [Fact]
    public async Task GetById_WhenDriverExists_ReturnsDriverDetails()
    {
        var service = new Mock<IDriverService>(MockBehavior.Strict);
        var driverId = Guid.NewGuid();
        var details = CreateDriverDetails(driverId);
        service
            .Setup(item => item.GetDriverDetailsAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var controller = new DriversController(service.Object);
        var response = await controller.GetById(driverId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(details, ok.Value);
    }

    [Fact]
    public async Task GetById_WhenDriverDoesNotExist_ReturnsNotFound()
    {
        var service = new Mock<IDriverService>(MockBehavior.Strict);
        var driverId = Guid.NewGuid();
        service
            .Setup(item => item.GetDriverDetailsAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriverDetailsDto?)null);

        var controller = new DriversController(service.Object);
        var response = await controller.GetById(driverId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task UpdateLocation_WhenRequestIsValid_ReturnsUpdatedLocation()
    {
        var service = new Mock<IDriverService>(MockBehavior.Strict);
        var driverId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
        service
            .Setup(item => item.UpdateDriverLocationAsync(
                driverId,
                14.5995,
                120.9842,
                90,
                24,
                5,
                updatedAt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverLocation
            {
                DriverId = driverId,
                Latitude = 14.5995,
                Longitude = 120.9842,
                HeadingDegrees = 90,
                SpeedKph = 24,
                AccuracyMeters = 5,
                UpdatedAt = updatedAt,
            });

        var controller = new DriversController(service.Object);
        var response = await controller.UpdateLocation(
            driverId,
            new UpdateDriverLocationRequest(14.5995, 120.9842, 90, 24, 5, updatedAt),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<DriverLocationDto>(ok.Value);
        Assert.Equal(driverId, body.DriverId);
        Assert.Equal(14.5995, body.Latitude);
        Assert.Equal(120.9842, body.Longitude);
        Assert.Equal(updatedAt, body.UpdatedAt);
    }

    [Fact]
    public async Task UpdateLocation_WhenCoordinatesAreInvalid_ReturnsBadRequestWithoutCallingService()
    {
        var service = new Mock<IDriverService>(MockBehavior.Strict);
        var controller = new DriversController(service.Object);

        var response = await controller.UpdateLocation(
            Guid.NewGuid(),
            new UpdateDriverLocationRequest(91, 120.9842),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAvailability_WhenDriverAlreadyHasActiveSession_ReturnsConflict()
    {
        var service = new Mock<IDriverService>(MockBehavior.Strict);
        var driverId = Guid.NewGuid();
        var details = CreateDriverDetails(
            driverId,
            new DriverAvailabilitySessionDto(
                11,
                driverId,
                null,
                null,
                null,
                null,
                "Main terminal",
                null,
                null,
                1,
                1000,
                "AVAILABLE",
                DateTime.UtcNow,
                null));
        service
            .Setup(item => item.GetDriverDetailsAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var controller = new DriversController(service.Object);
        var response = await controller.StartAvailability(
            driverId,
            new StartDriverAvailabilityRequest(),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(response.Result);
        service.Verify(item => item.StartAvailabilitySessionAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<long?>(),
            It.IsAny<string?>(),
            It.IsAny<double?>(),
            It.IsAny<double?>(),
            It.IsAny<int>(),
            It.IsAny<decimal>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DriverDetailsDto CreateDriverDetails(
        Guid driverId,
        DriverAvailabilitySessionDto? session = null) =>
        new(
            driverId,
            Guid.NewGuid(),
            null,
            "D-123",
            "VERIFIED",
            null,
            null,
            4.8m,
            12,
            session is not null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            [],
            null,
            session);
}
