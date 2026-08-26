using System.Security.Claims;
using backend.Controllers;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class AdminTricyclePointSubmissionPublishingControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var attribute = Assert.Single(
            typeof(AdminTricyclePointSubmissionPublishingController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("Admin", attribute.Roles);
    }

    [Fact]
    public async Task Approve_Success_ReturnsPublishedPoint()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionPublishingService>();
        service.Setup(item => item.PublishAsync(adminId, 17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointSubmissionPublishResult.Success(new(
                17, 88, "TODA-SUB-17", "Verified TODA", "Approved", adminId, DateTimeOffset.UtcNow)));

        var controller = BuildController(service.Object, adminId);
        var result = await controller.Approve(17, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TricyclePointSubmissionPublishResponse>(ok.Value);
        Assert.Equal(88, response.TricyclePointId);
        Assert.Equal("Approved", response.Status);
    }

    [Fact]
    public async Task Approve_AlreadyPublished_ReturnsConflict()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionPublishingService>();
        service.Setup(item => item.PublishAsync(adminId, 17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointSubmissionPublishResult.StateConflict("Already published."));

        var controller = BuildController(service.Object, adminId);
        var result = await controller.Approve(17, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    private static AdminTricyclePointSubmissionPublishingController BuildController(
        ITricyclePointSubmissionPublishingService service,
        Guid adminId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            "Test");

        return new AdminTricyclePointSubmissionPublishingController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
