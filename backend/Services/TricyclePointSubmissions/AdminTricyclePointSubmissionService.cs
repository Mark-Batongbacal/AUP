using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using backend.Repositories;

namespace backend.Services;

public sealed class AdminTricyclePointSubmissionService(ITricyclePointSubmissionRepository repository)
    : IAdminTricyclePointSubmissionService
{
    private const int MaxPageSize = 100;
    private static readonly HashSet<string> AllowedStatuses =
        ["Pending", "Approved", "Rejected", "NeedsChanges"];

    public async Task<AdminTricyclePointSubmissionPageResponse> GetPageAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeStatus(status);
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, totalCount) = await repository.GetForAdminAsync(
            normalizedStatus,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        return new AdminTricyclePointSubmissionPageResponse(
            items.Select(Map).ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<AdminTricyclePointSubmissionResponse?> GetByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default)
    {
        if (submissionId <= 0)
        {
            return null;
        }

        var submission = await repository.GetByIdAsync(submissionId, cancellationToken);
        return submission is null ? null : Map(submission);
    }

    public async Task<AdminTricyclePointSubmissionMutationResult> UpdateReviewAsync(
        Guid adminUserId,
        long submissionId,
        UpdateAdminTricyclePointSubmissionReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == Guid.Empty)
        {
            return AdminTricyclePointSubmissionMutationResult.Invalid("A valid administrator is required.");
        }

        var submission = await repository.GetTrackedByIdAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            return AdminTricyclePointSubmissionMutationResult.Missing();
        }

        if (!CanReview(submission))
        {
            return AdminTricyclePointSubmissionMutationResult.StateConflict(
                $"Submission status '{submission.Status}' can no longer be edited in review.");
        }

        var errors = ValidateReview(request);
        if (errors.Count > 0)
        {
            return AdminTricyclePointSubmissionMutationResult.Invalid(errors.ToArray());
        }

        submission.Latitude = request.Latitude!.Value;
        submission.Longitude = request.Longitude!.Value;
        submission.AdminPointName = NormalizeOptional(request.PointName);
        submission.AdminOperatorName = NormalizeOptional(request.OperatorName);
        submission.AdminAddress = NormalizeOptional(request.Address);
        submission.AdminLandmark = NormalizeOptional(request.Landmark);
        submission.AdminDescription = NormalizeOptional(request.Description);
        submission.AdminNotes = NormalizeOptional(request.AdminNotes);
        submission.UpdatedAt = DateTimeOffset.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return AdminTricyclePointSubmissionMutationResult.Success(Map(submission));
    }

    public Task<AdminTricyclePointSubmissionMutationResult> RejectAsync(
        Guid adminUserId,
        long submissionId,
        string reason,
        CancellationToken cancellationToken = default) =>
        ApplyDecisionAsync(adminUserId, submissionId, "Rejected", reason, cancellationToken);

    public Task<AdminTricyclePointSubmissionMutationResult> MarkNeedsChangesAsync(
        Guid adminUserId,
        long submissionId,
        string reason,
        CancellationToken cancellationToken = default) =>
        ApplyDecisionAsync(adminUserId, submissionId, "NeedsChanges", reason, cancellationToken);

    private async Task<AdminTricyclePointSubmissionMutationResult> ApplyDecisionAsync(
        Guid adminUserId,
        long submissionId,
        string status,
        string reason,
        CancellationToken cancellationToken)
    {
        if (adminUserId == Guid.Empty)
        {
            return AdminTricyclePointSubmissionMutationResult.Invalid("A valid administrator is required.");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length < 3)
        {
            return AdminTricyclePointSubmissionMutationResult.Invalid(
                "A review reason of at least 3 characters is required.");
        }

        if (normalizedReason.Length > 1000)
        {
            return AdminTricyclePointSubmissionMutationResult.Invalid(
                "Review reason must be 1000 characters or fewer.");
        }

        var submission = await repository.GetTrackedByIdAsync(submissionId, cancellationToken);
        if (submission is null)
        {
            return AdminTricyclePointSubmissionMutationResult.Missing();
        }

        if (!CanReview(submission))
        {
            return AdminTricyclePointSubmissionMutationResult.StateConflict(
                $"Submission status '{submission.Status}' can no longer be changed by this review action.");
        }

        var now = DateTimeOffset.UtcNow;
        submission.Status = status;
        submission.AdminNotes = normalizedReason;
        submission.ReviewedByUserId = adminUserId;
        submission.ReviewedAt = now;
        submission.UpdatedAt = now;

        await repository.SaveChangesAsync(cancellationToken);
        return AdminTricyclePointSubmissionMutationResult.Success(Map(submission));
    }

    private static bool CanReview(TricyclePointSubmission submission) =>
        submission.PublishedTricyclePointId is null &&
        (submission.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
         submission.Status.Equals("NeedsChanges", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeStatus(string? status)
    {
        var normalized = status?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return AllowedStatuses.FirstOrDefault(
            item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ValidateReview(UpdateAdminTricyclePointSubmissionReviewRequest request)
    {
        var errors = new List<string>();
        if (request.Latitude is null)
        {
            errors.Add("Latitude is required.");
        }
        else if (request.Latitude is < -90m or > 90m)
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (request.Longitude is null)
        {
            errors.Add("Longitude is required.");
        }
        else if (request.Longitude is < -180m or > 180m)
        {
            errors.Add("Longitude must be between -180 and 180.");
        }

        AddLengthError(errors, request.PointName, 200, "Point name");
        AddLengthError(errors, request.OperatorName, 200, "Operator name");
        AddLengthError(errors, request.Address, 500, "Address");
        AddLengthError(errors, request.Landmark, 300, "Landmark");
        AddLengthError(errors, request.Description, 500, "Description");
        AddLengthError(errors, request.AdminNotes, 1000, "Admin notes");
        return errors;
    }

    private static void AddLengthError(ICollection<string> errors, string? value, int maxLength, string label)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && normalized.Length > maxLength)
        {
            errors.Add($"{label} must be {maxLength} characters or fewer.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static AdminTricyclePointSubmissionResponse Map(TricyclePointSubmission submission) =>
        new(
            submission.TricyclePointSubmissionId,
            submission.SubmittedByUserId,
            submission.ProofImageUrl,
            submission.Latitude,
            submission.Longitude,
            submission.AccuracyMeters,
            submission.LocationCapturedAt,
            submission.SuggestedTodaName,
            submission.SuggestedLandmark,
            submission.Status,
            submission.AdminPointName,
            submission.AdminOperatorName,
            submission.AdminAddress,
            submission.AdminLandmark,
            submission.AdminDescription,
            submission.AdminNotes,
            submission.ReviewedByUserId,
            submission.ReviewedAt,
            submission.PublishedTricyclePointId,
            submission.CreatedAt,
            submission.UpdatedAt);
}
