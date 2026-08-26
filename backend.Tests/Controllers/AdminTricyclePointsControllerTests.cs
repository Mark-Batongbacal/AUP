using backend.Controllers;
using backend.Models.TricyclePointManagement;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class AdminTricyclePointsControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var attribute = Assert.Single(
            typeof(AdminTricyclePointsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal("Admin", attribute.Roles);
    }

    [Fact]
    public async Task GetDuplicates_WhenCoordinatesMissing_ReturnsBadRequest()
    {
        var service = new Mock<IAdminTricyclePointManagementService>(MockBehavior.Strict);
        var controller = new AdminTricyclePointsController(service.Object);

        var result = await controller.GetDuplicates(null, null, null, 75, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Archive_WhenPointExists_ReturnsUpdatedPoint()
    {
        var service = new Mock<IAdminTricyclePointManagementService>(MockBehavior.Strict);
        var point = new AdminTricyclePointResponse(
            5, null, "TODA-5", "TODA 5", null, null, null,
            15.145, 120.588, 500, null, null, null, null, null,
            false, DateTime.UtcNow, DateTime.UtcNow);
        service.Setup(item => item.SetActiveAsync(5, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminTricyclePointMutationResult.Success(
                new AdminTricyclePointMutationResponse(point, [])));
        var controller = new AdminTricyclePointsController(service.Object);

        var result = await controller.Archive(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AdminTricyclePointMutationResponse>(ok.Value);
        Assert.False(response.Point.IsActive);
    }
}
