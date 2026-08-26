using System.Security.Claims;
using backend.Controllers;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class TricyclePointSubmissionsControllerTests
{
    [Fact]
    public async Task Create_RegisteredUser_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionService>();
        service
            .Setup(item => item.CreateAsync(userId, It.IsAny<CreateTricyclePointSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointSubmissionMutationResult.Success(
                new TricyclePointSubmissionResponse(
                    5,
                    "https://example.test/proof.jpg",
                    15.1m,
                    120.5m,
                    5m,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    "Pending",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    null,
                    null)));

        var controller = new TricyclePointSubmissionsController(service.Object)
        {
            ControllerContext = BuildControllerContext(userId, "Passenger")
        };

        var result = await controller.Create(
            new CreateTricyclePointSubmissionRequest
            {
                ProofImageUrl = "https://example.test/proof.jpg",
                Latitude = 15.1m,
                Longitude = 120.5m,
                LocationCapturedAt = DateTimeOffset.UtcNow
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TricyclePointSubmissionsController.GetById), created.ActionName);
    }

    [Fact]
    public async Task Create_GuestUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionService>();
        var controller = new TricyclePointSubmissionsController(service.Object)
        {
            ControllerContext = BuildControllerContext(userId, "Guest")
        };

        var result = await controller.Create(
            new CreateTricyclePointSubmissionRequest
            {
                ProofImageUrl = "https://example.test/proof.jpg",
                Latitude = 15.1m,
                Longitude = 120.5m,
                LocationCapturedAt = DateTimeOffset.UtcNow
            },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        service.Verify(item => item.CreateAsync(
            It.IsAny<Guid>(),
            It.IsAny<CreateTricyclePointSubmissionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ControllerContext BuildControllerContext(Guid userId, string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
