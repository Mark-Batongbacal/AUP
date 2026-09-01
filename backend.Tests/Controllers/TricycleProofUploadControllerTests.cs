using System.Security.Claims;
using backend.Controllers;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public sealed class TricycleProofUploadControllerTests
{
    [Fact]
    public async Task UploadProof_ValidJpeg_ReturnsStoredProofUrl()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionService>();
        var storage = new Mock<ITricycleProofStorage>();
        storage
            .Setup(item => item.SaveAsync(
                userId,
                It.IsAny<ReadOnlyMemory<byte>>(),
                "jpg",
                "image/jpeg",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredTricycleProof($"{userId:N}-proof.jpg", "image/jpeg"));

        var controller = new TricyclePointSubmissionsController(service.Object, storage.Object)
        {
            ControllerContext = BuildControllerContext(userId, "Passenger")
        };

        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "proof.jpg");

        var result = await controller.UploadProof(formFile, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TricycleProofUploadResponse>(ok.Value);
        Assert.Equal(
            $"/api/tricycle-point-submissions/proof/{userId:N}-proof.jpg",
            response.ProofImageUrl);
    }

    [Fact]
    public async Task UploadProof_InvalidImage_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionService>();
        var storage = new Mock<ITricycleProofStorage>();
        var controller = new TricyclePointSubmissionsController(service.Object, storage.Object)
        {
            ControllerContext = BuildControllerContext(userId, "Passenger")
        };

        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "fake.jpg");

        var result = await controller.UploadProof(formFile, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        storage.Verify(item => item.SaveAsync(
            It.IsAny<Guid>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithExternalProofUrl_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ITricyclePointSubmissionService>();
        var storage = new Mock<ITricycleProofStorage>();
        var controller = new TricyclePointSubmissionsController(service.Object, storage.Object)
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

        Assert.IsType<BadRequestObjectResult>(result.Result);
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
