using backend.Controllers;
using backend.Models.Database;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class TricyclePointsControllerTests
{
    [Fact]
    public async Task Create_WhenInputIsValid_ReturnsCreatedPoint()
    {
        var service = new Mock<ITricyclePointService>();
        var savedPoint = new TricyclePoint
        {
            TricyclePointId = 17,
            PointCode = "TRIKE-17",
            PointName = "Main TODA",
            CenterLatitude = 15.1097,
            CenterLongitude = 120.5824,
            RadiusMeters = 500,
            BaseFare = 35,
            IsActive = true,
        };
        service
            .Setup(item => item.AddVerifiedTricyclePointAsync(
                "TRIKE-17",
                "Main TODA",
                15.1097,
                120.5824,
                500,
                null,
                null,
                null,
                null,
                35,
                null,
                null,
                null,
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointMutationResult.Success(savedPoint));

        var controller = new TricyclePointsController(service.Object);
        var response = await controller.Create(new(
            "TRIKE-17",
            "Main TODA",
            [15.1097, 120.5824],
            500,
            BaseFare: 35), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(TricyclePointsController.GetById), created.ActionName);
        Assert.Equal(17L, created.RouteValues?["tricyclePointId"]);
        var body = Assert.IsType<TricyclePointResponseDto>(created.Value);
        Assert.Equal("TRIKE-17", body.PointCode);
        Assert.Equal(35, body.BaseFare);
    }

    [Fact]
    public async Task Create_WhenPointIsDuplicate_ReturnsConflict()
    {
        var service = new Mock<ITricyclePointService>();
        service
            .Setup(item => item.AddVerifiedTricyclePointAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<long?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<int?>(),
                It.IsAny<TimeOnly?>(),
                It.IsAny<TimeOnly?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointMutationResult.Duplicate(
                ["Point code TRIKE-17 is already used."]));

        var controller = new TricyclePointsController(service.Object);
        var response = await controller.Create(new(
            "TRIKE-17",
            "Main TODA",
            [15.1097, 120.5824],
            500), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
        var body = Assert.IsType<TricyclePointErrorResponseDto>(conflict.Value);
        Assert.Contains("Point code TRIKE-17 is already used.", body.Errors);
    }

    [Fact]
    public async Task GetById_WhenPointDoesNotExist_ReturnsNotFound()
    {
        var service = new Mock<ITricyclePointService>();
        service
            .Setup(item => item.GetPointByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint?)null);

        var controller = new TricyclePointsController(service.Object);
        var response = await controller.GetById(99, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var body = Assert.IsType<TricyclePointErrorResponseDto>(notFound.Value);
        Assert.Contains("Tricycle point 99 was not found.", body.Errors);
    }

    [Fact]
    public async Task Create_WhenCoordinatesDoNotContainLatitudeAndLongitude_ReturnsBadRequest()
    {
        var service = new Mock<ITricyclePointService>(MockBehavior.Strict);
        var controller = new TricyclePointsController(service.Object);

        var response = await controller.Create(new(
            "TRIKE-17",
            "Main TODA",
            [120.5824],
            500), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var body = Assert.IsType<TricyclePointErrorResponseDto>(badRequest.Value);
        Assert.Contains(
            "Coordinates must contain exactly two values: [latitude, longitude].",
            body.Errors);
        service.VerifyNoOtherCalls();
    }
}
