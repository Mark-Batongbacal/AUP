using System.Security.Claims;
using backend.Controllers;
using backend.Models.Users;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class UsersControllerTests
{
    [Fact]
    public async Task GetCurrent_WhenProfileExists_ReturnsCurrentProfile()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var profile = CreateProfileResponse(userId);
        service
            .Setup(item => item.GetCurrentUserProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var controller = CreateController(service, userId);
        var response = await controller.GetCurrent(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(profile, ok.Value);
    }

    [Fact]
    public async Task GetCurrent_WhenProfileMissing_ReturnsNotFound()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        service
            .Setup(item => item.GetCurrentUserProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfileResponse?)null);

        var controller = CreateController(service, userId);
        var response = await controller.GetCurrent(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    [Fact]
    public async Task GetCurrent_WhenUserClaimIsMissing_ReturnsUnauthorized()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var controller = CreateController(service);

        var response = await controller.GetCurrent(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response.Result);
        service.Verify(
            item => item.GetCurrentUserProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCurrent_WhenUpdateSucceeds_ReturnsUpdatedProfile()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest(
            "Ana",
            "Santos",
            "+63 900 000 0000",
            "https://example.test/avatar.png");
        var profile = CreateProfileResponse(userId);
        service
            .Setup(item => item.UpdateCurrentUserProfileAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.ProfileImageUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserProfileMutationResult.Success(profile));

        var controller = CreateController(service, userId);
        var response = await controller.UpdateCurrent(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(profile, ok.Value);
    }

    [Fact]
    public async Task UpdateCurrent_WhenRequestBodyIsMissing_ReturnsBadRequest()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var controller = CreateController(service, Guid.NewGuid());

        var response = await controller.UpdateCurrent(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        service.Verify(
            item => item.UpdateCurrentUserProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCurrent_WhenServiceRejectsRequest_ReturnsBadRequest()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest(FirstName: new string('A', 101));
        service
            .Setup(item => item.UpdateCurrentUserProfileAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.ProfileImageUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserProfileMutationResult.ValidationFailed(
                ["First name must be 100 characters or fewer."]));

        var controller = CreateController(service, userId);
        var response = await controller.UpdateCurrent(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task UpdateCurrent_WhenProfileMissing_ReturnsNotFound()
    {
        var service = new Mock<IUserProfileService>(MockBehavior.Strict);
        var userId = Guid.NewGuid();
        var request = new UpdateUserProfileRequest(FirstName: "Ana");
        service
            .Setup(item => item.UpdateCurrentUserProfileAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.ProfileImageUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserProfileMutationResult.NotFound(userId));

        var controller = CreateController(service, userId);
        var response = await controller.UpdateCurrent(request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(response.Result);
    }

    private static UsersController CreateController(
        Mock<IUserProfileService> service,
        Guid? userId = null)
    {
        var claims = userId.HasValue
            ? [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())]
            : Array.Empty<Claim>();
        var controller = new UsersController(service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey")),
            },
        };

        return controller;
    }

    private static UserProfileResponse CreateProfileResponse(Guid userId) =>
        new(
            userId,
            "Ana",
            "Santos",
            "+63 900 000 0000",
            "Passenger",
            "https://example.test/avatar.png",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
}
