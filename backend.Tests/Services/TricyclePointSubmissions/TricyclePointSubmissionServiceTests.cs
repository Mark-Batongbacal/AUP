using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.TricyclePointSubmissions;

public sealed class TricyclePointSubmissionServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesPendingSubmission()
    {
        var repository = new Mock<ITricyclePointSubmissionRepository>();
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<TricyclePointSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePointSubmission submission, CancellationToken _) =>
            {
                submission.TricyclePointSubmissionId = 42;
                return submission;
            });

        var service = new TricyclePointSubmissionService(repository.Object);
        var userId = Guid.NewGuid();
        var result = await service.CreateAsync(
            userId,
            new CreateTricyclePointSubmissionRequest
            {
                ProofImageUrl = " https://example.test/proof.jpg ",
                Latitude = 15.1453m,
                Longitude = 120.5887m,
                AccuracyMeters = 8.4m,
                LocationCapturedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
                SuggestedTodaName = "  Dau TODA  ",
                SuggestedLandmark = "  Near terminal  "
            });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Submission);
        Assert.Equal(42, result.Submission!.TricyclePointSubmissionId);
        Assert.Equal("Pending", result.Submission.Status);
        Assert.Equal("Dau TODA", result.Submission.SuggestedTodaName);
        Assert.Equal("Near terminal", result.Submission.SuggestedLandmark);
        repository.Verify(repo => repo.AddAsync(
            It.Is<TricyclePointSubmission>(submission =>
                submission.SubmittedByUserId == userId &&
                submission.Status == "Pending" &&
                submission.ProofImageUrl == "https://example.test/proof.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvalidCoordinates_DoesNotPersist()
    {
        var repository = new Mock<ITricyclePointSubmissionRepository>();
        var service = new TricyclePointSubmissionService(repository.Object);

        var result = await service.CreateAsync(
            Guid.NewGuid(),
            new CreateTricyclePointSubmissionRequest
            {
                ProofImageUrl = "https://example.test/proof.jpg",
                Latitude = 91m,
                Longitude = 181m,
                LocationCapturedAt = DateTimeOffset.UtcNow
            });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Latitude", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Longitude", StringComparison.Ordinal));
        repository.Verify(repo => repo.AddAsync(It.IsAny<TricyclePointSubmission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdForUserAsync_DifferentSubmitter_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var repository = new Mock<ITricyclePointSubmissionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TricyclePointSubmission
            {
                TricyclePointSubmissionId = 10,
                SubmittedByUserId = ownerId,
                ProofImageUrl = "proof",
                Status = "Pending"
            });

        var service = new TricyclePointSubmissionService(repository.Object);
        var result = await service.GetByIdForUserAsync(Guid.NewGuid(), 10);

        Assert.Null(result);
    }
}
