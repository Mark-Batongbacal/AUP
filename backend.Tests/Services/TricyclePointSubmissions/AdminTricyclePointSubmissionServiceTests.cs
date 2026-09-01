using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.TricyclePointSubmissions;

public sealed class AdminTricyclePointSubmissionServiceTests
{
    [Fact]
    public async Task GetPageAsync_NormalizesPagingAndMapsAdminFields()
    {
        var repository = new Mock<ITricyclePointSubmissionRepository>();
        repository
            .Setup(item => item.GetForAdminAsync("Pending", 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Submission(status: "Pending")], 1));

        var service = new AdminTricyclePointSubmissionService(repository.Object);
        var result = await service.GetPageAsync("pending", 0, 500);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("Pending", result.Items[0].Status);
        Assert.Equal("Verified TODA", result.Items[0].AdminPointName);
        Assert.Equal(15.1m, result.Items[0].Latitude);
        Assert.Equal(120.5m, result.Items[0].Longitude);
    }

    [Fact]
    public async Task UpdateReviewAsync_PendingSubmission_PreservesOriginalAndStoresAdminCoordinates()
    {
        var submission = Submission(status: "Pending");
        var originalLatitude = submission.Latitude;
        var originalLongitude = submission.Longitude;
        var repository = TrackedRepository(submission);
        var service = new AdminTricyclePointSubmissionService(repository.Object);

        var result = await service.UpdateReviewAsync(
            Guid.NewGuid(),
            submission.TricyclePointSubmissionId,
            new UpdateAdminTricyclePointSubmissionReviewRequest
            {
                Latitude = 15.222222m,
                Longitude = 120.555555m,
                PointName = " Corrected TODA ",
                OperatorName = "Operator",
                Address = "Address",
                Landmark = "Landmark",
                Description = "Description",
                AdminNotes = "Checked against map"
            });

        Assert.True(result.Succeeded);
        Assert.Equal(originalLatitude, submission.Latitude);
        Assert.Equal(originalLongitude, submission.Longitude);
        Assert.Equal(15.222222m, submission.AdminLatitude);
        Assert.Equal(120.555555m, submission.AdminLongitude);
        Assert.Equal("Corrected TODA", submission.AdminPointName);
        Assert.Equal("Checked against map", submission.AdminNotes);
        Assert.Equal("Pending", submission.Status);
        Assert.Null(submission.ReviewedAt);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReviewAsync_WhenReviewedCoordinatesMatchOriginal_ClearsAdminCorrection()
    {
        var submission = Submission(status: "Pending");
        submission.AdminLatitude = 15.2m;
        submission.AdminLongitude = 120.6m;
        var repository = TrackedRepository(submission);
        var service = new AdminTricyclePointSubmissionService(repository.Object);

        var result = await service.UpdateReviewAsync(
            Guid.NewGuid(),
            submission.TricyclePointSubmissionId,
            new UpdateAdminTricyclePointSubmissionReviewRequest
            {
                Latitude = submission.Latitude,
                Longitude = submission.Longitude,
                PointName = "Verified TODA"
            });

        Assert.True(result.Succeeded);
        Assert.Null(submission.AdminLatitude);
        Assert.Null(submission.AdminLongitude);
    }

    [Fact]
    public async Task RejectAsync_PendingSubmission_RecordsAdminAndReason()
    {
        var submission = Submission(status: "Pending");
        var repository = TrackedRepository(submission);
        var service = new AdminTricyclePointSubmissionService(repository.Object);
        var adminId = Guid.NewGuid();

        var result = await service.RejectAsync(adminId, submission.TricyclePointSubmissionId, " Duplicate point ");

        Assert.True(result.Succeeded);
        Assert.Equal("Rejected", submission.Status);
        Assert.Equal("Duplicate point", submission.AdminNotes);
        Assert.Equal(adminId, submission.ReviewedByUserId);
        Assert.NotNull(submission.ReviewedAt);
    }

    [Fact]
    public async Task MarkNeedsChangesAsync_ApprovedSubmission_ReturnsConflictWithoutSaving()
    {
        var submission = Submission(status: "Approved");
        var repository = TrackedRepository(submission);
        var service = new AdminTricyclePointSubmissionService(repository.Object);

        var result = await service.MarkNeedsChangesAsync(
            Guid.NewGuid(),
            submission.TricyclePointSubmissionId,
            "Need clearer proof");

        Assert.False(result.Succeeded);
        Assert.True(result.Conflict);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateReviewAsync_MissingCoordinates_ReturnsValidationError()
    {
        var submission = Submission(status: "Pending");
        var repository = TrackedRepository(submission);
        var service = new AdminTricyclePointSubmissionService(repository.Object);

        var result = await service.UpdateReviewAsync(
            Guid.NewGuid(),
            submission.TricyclePointSubmissionId,
            new UpdateAdminTricyclePointSubmissionReviewRequest());

        Assert.False(result.Succeeded);
        Assert.Contains("Latitude is required.", result.Errors);
        Assert.Contains("Longitude is required.", result.Errors);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ITricyclePointSubmissionRepository> TrackedRepository(TricyclePointSubmission submission)
    {
        var repository = new Mock<ITricyclePointSubmissionRepository>();
        repository
            .Setup(item => item.GetTrackedByIdAsync(submission.TricyclePointSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        repository
            .Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static TricyclePointSubmission Submission(string status) => new()
    {
        TricyclePointSubmissionId = 17,
        SubmittedByUserId = Guid.NewGuid(),
        ProofImageUrl = "/api/tricycle-point-submissions/proof/proof.jpg",
        Latitude = 15.1m,
        Longitude = 120.5m,
        AccuracyMeters = 8m,
        LocationCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        SuggestedTodaName = "Suggested",
        SuggestedLandmark = "Market",
        Status = status,
        AdminPointName = "Verified TODA",
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1)
    };
}
