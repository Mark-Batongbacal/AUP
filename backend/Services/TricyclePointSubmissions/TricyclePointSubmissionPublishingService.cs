using backend.Models.TricyclePointSubmissions;
using backend.Repositories;

namespace backend.Services;

public sealed class TricyclePointSubmissionPublishingService(
    ITricyclePointSubmissionRepository submissionRepository,
    ITricyclePointSubmissionPublishingRepository publishingRepository)
    : ITricyclePointSubmissionPublishingService
{
    private const int PublishedPointRadiusMeters = 500;

    public async Task<TricyclePointSubmissionPublishResult> PublishAsync(
        Guid adminUserId,
        long submissionId,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == Guid.Empty)
        {
            return TricyclePointSubmissionPublishResult.Invalid("A valid administrator is required.");
        }

        if (submissionId <= 0)
        {
            return TricyclePointSubmissionPublishResult.Missing();
        }

        var submission = await submissionRepository.GetByIdAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            return TricyclePointSubmissionPublishResult.Missing();
        }

        if (submission.PublishedTricyclePointId is not null ||
            submission.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            return TricyclePointSubmissionPublishResult.StateConflict(
                "This submission has already been published.");
        }

        if (!submission.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
            !submission.Status.Equals("NeedsChanges", StringComparison.OrdinalIgnoreCase))
        {
            return TricyclePointSubmissionPublishResult.StateConflict(
                $"Submission status '{submission.Status}' cannot be approved.");
        }

        var pointName = NormalizeOptional(submission.AdminPointName)
            ?? NormalizeOptional(submission.SuggestedTodaName);
        if (pointName is null)
        {
            return TricyclePointSubmissionPublishResult.Invalid(
                "A verified point name is required before approval.");
        }

        var latitude = (double)submission.Latitude;
        var longitude = (double)submission.Longitude;
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            return TricyclePointSubmissionPublishResult.Invalid(
                "The reviewed coordinates are invalid and must be corrected before approval.");
        }

        var draft = new TricyclePointPublicationDraft(
            PointCode: $"TODA-SUB-{submission.TricyclePointSubmissionId}",
            PointName: pointName,
            Latitude: latitude,
            Longitude: longitude,
            RadiusMeters: PublishedPointRadiusMeters,
            Description: NormalizeOptional(submission.AdminDescription),
            Address: NormalizeOptional(submission.AdminAddress),
            OperatorName: NormalizeOptional(submission.AdminOperatorName));

        return await publishingRepository.PublishAsync(
            submissionId,
            adminUserId,
            draft,
            cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
