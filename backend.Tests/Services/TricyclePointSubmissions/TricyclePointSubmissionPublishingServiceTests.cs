using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.TricyclePointSubmissions;

public sealed class TricyclePointSubmissionPublishingServiceTests
{
    [Fact]
    public async Task PublishAsync_PendingReviewedSubmission_BuildsOfficialPointDraftFromOriginalWhenUnchanged()
    {
        var submission = Submission("Pending");
        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var publishing = PublishingRepository();
        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.True(result.Succeeded);
        publishing.Verify(item => item.PublishAsync(
            17,
            It.IsAny<Guid>(),
            It.Is<TricyclePointPublicationDraft>(draft =>
                draft.PointCode == "TODA-SUB-17" &&
                draft.PointName == "Verified TODA" &&
                draft.RadiusMeters == 500 &&
                draft.Latitude == 15.123456 &&
                draft.Longitude == 120.654321),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_AdminCorrectedCoordinates_PublishesReviewedPairAndPreservesOriginal()
    {
        var submission = Submission("Pending");
        submission.AdminLatitude = 15.222222m;
        submission.AdminLongitude = 120.555555m;
        var originalLatitude = submission.Latitude;
        var originalLongitude = submission.Longitude;

        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var publishing = PublishingRepository();
        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.True(result.Succeeded);
        Assert.Equal(originalLatitude, submission.Latitude);
        Assert.Equal(originalLongitude, submission.Longitude);
        publishing.Verify(item => item.PublishAsync(
            17,
            It.IsAny<Guid>(),
            It.Is<TricyclePointPublicationDraft>(draft =>
                draft.Latitude == 15.222222 &&
                draft.Longitude == 120.555555),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_IncompleteAdminCoordinatePair_ReturnsValidationError()
    {
        var submission = Submission("Pending");
        submission.AdminLatitude = 15.2m;
        submission.AdminLongitude = null;

        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        var publishing = new Mock<ITricyclePointSubmissionPublishingRepository>();

        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.False(result.Succeeded);
        Assert.Contains("Reviewed coordinates are incomplete and must be corrected before approval.", result.Errors);
        publishing.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublishAsync_NoVerifiedOrSuggestedName_ReturnsValidationError()
    {
        var submission = Submission("Pending");
        submission.AdminPointName = null;
        submission.SuggestedTodaName = null;

        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        var publishing = new Mock<ITricyclePointSubmissionPublishingRepository>();

        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.False(result.Succeeded);
        Assert.Contains("A verified point name is required before approval.", result.Errors);
        publishing.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublishAsync_RejectedSubmission_ReturnsConflict()
    {
        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Submission("Rejected"));
        var publishing = new Mock<ITricyclePointSubmissionPublishingRepository>();

        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.True(result.Conflict);
        publishing.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublishedSubmission_ReturnsConflict()
    {
        var submission = Submission("Approved");
        submission.PublishedTricyclePointId = 99;
        var submissions = new Mock<ITricyclePointSubmissionRepository>();
        submissions.Setup(item => item.GetByIdAsync(17, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        var publishing = new Mock<ITricyclePointSubmissionPublishingRepository>();

        var service = new TricyclePointSubmissionPublishingService(submissions.Object, publishing.Object);
        var result = await service.PublishAsync(Guid.NewGuid(), 17);

        Assert.True(result.Conflict);
        publishing.VerifyNoOtherCalls();
    }

    private static Mock<ITricyclePointSubmissionPublishingRepository> PublishingRepository()
    {
        var publishing = new Mock<ITricyclePointSubmissionPublishingRepository>();
        publishing.Setup(item => item.PublishAsync(
                17,
                It.IsAny<Guid>(),
                It.IsAny<TricyclePointPublicationDraft>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, Guid adminId, TricyclePointPublicationDraft draft, CancellationToken _) =>
                TricyclePointSubmissionPublishResult.Success(new(
                    id, 88, draft.PointCode, draft.PointName, "Approved", adminId, DateTimeOffset.UtcNow)));
        return publishing;
    }

    private static TricyclePointSubmission Submission(string status) => new()
    {
        TricyclePointSubmissionId = 17,
        SubmittedByUserId = Guid.NewGuid(),
        ProofImageUrl = "/api/tricycle-point-submissions/proof/proof.jpg",
        Latitude = 15.123456m,
        Longitude = 120.654321m,
        LocationCapturedAt = DateTimeOffset.UtcNow,
        SuggestedTodaName = "Suggested TODA",
        Status = status,
        AdminPointName = "Verified TODA",
        AdminOperatorName = "Verified Operator",
        AdminAddress = "Verified Address",
        AdminDescription = "Verified description",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
