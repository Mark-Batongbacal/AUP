using System.Security.Claims;
using backend.Controllers;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class AdminTricyclePointSubmissionsControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var attribute = Assert.Single(
            typeof(AdminTricyclePointSubmissionsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("Admin", attribute.Roles);
    }

    [Fact]
    public async Task GetAll_InvalidStatus_ReturnsBadRequestWithoutCallingService()
    {
        var service = new Mock<IAdminTricyclePointSubmissionService>();
        var controller = Controller(service.Object, Guid.NewGuid());

        var result = await controller.GetAll("Unknown", 1, 25, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.Verify(item => item.GetPageAsync(
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_ExistingSubmission_ReturnsAdminDetails()
    {
        var service = new Mock<IAdminTricyclePointSubmissionService>();
        service
            .Setup(item => item.GetByIdAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(12, "Pending"));
        var controller = Controller(service.Object, Guid.NewGuid());

        var result = await controller.GetById(12, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AdminTricyclePointSubmissionResponse>(ok.Value);
        Assert.Equal(12, response.TricyclePointSubmissionId);
        Assert.Equal("Verified TODA", response.AdminPointName);
    }

    [Fact]
    public async Task UpdateReview_UsesAuthenticatedAdminId()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<IAdminTricyclePointSubmissionService>();
        service
            .Setup(item => item.UpdateReviewAsync(
                adminId,
                12,
                It.IsAny<UpdateAdminTricyclePointSubmissionReviewRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminTricyclePointSubmissionMutationResult.Success(Response(12, "Pending")));
        var controller = Controller(service.Object, adminId);

        var result = await controller.UpdateReview(
            12,
            new UpdateAdminTricyclePointSubmissionReviewRequest
            {
                Latitude = 15.1m,
                Longitude = 120.5m,
                PointName = "Verified TODA"
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.VerifyAll();
    }

    [Fact]
    public async Task Reject_Conflict_Returns409()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<IAdminTricyclePointSubmissionService>();
        service
            .Setup(item => item.RejectAsync(adminId, 12, "Duplicate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminTricyclePointSubmissionMutationResult.StateConflict("Already reviewed."));
        var controller = Controller(service.Object, adminId);

        var result = await controller.Reject(
            12,
            new AdminTricyclePointSubmissionDecisionRequest { Reason = "Duplicate" },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var error = Assert.IsType<TricyclePointSubmissionErrorResponse>(conflict.Value);
        Assert.Contains("Already reviewed.", error.Errors);
    }

    [Fact]
    public async Task NeedsChanges_MissingSubmission_Returns404()
    {
        var adminId = Guid.NewGuid();
        var service = new Mock<IAdminTricyclePointSubmissionService>();
        service
            .Setup(item => item.MarkNeedsChangesAsync(adminId, 99, "Need proof", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminTricyclePointSubmissionMutationResult.Missing());
        var controller = Controller(service.Object, adminId);

        var result = await controller.NeedsChanges(
            99,
            new AdminTricyclePointSubmissionDecisionRequest { Reason = "Need proof" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static AdminTricyclePointSubmissionsController Controller(
        IAdminTricyclePointSubmissionService service,
        Guid adminId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            "Test");

        return new AdminTricyclePointSubmissionsController(service)
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

    private static AdminTricyclePointSubmissionResponse Response(long id, string status) => new(
        id,
        Guid.NewGuid(),
        "/api/tricycle-point-submissions/proof/proof.jpg",
        15.1m,
        120.5m,
        null,
        null,
        5m,
        DateTimeOffset.UtcNow,
        "Suggested TODA",
        "Market",
        status,
        "Verified TODA",
        "Operator",
        "Address",
        "Landmark",
        "Description",
        "Notes",
        null,
        null,
        null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
