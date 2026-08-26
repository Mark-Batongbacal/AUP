using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using backend.Repositories;

namespace backend.Services;

public sealed class TricyclePointSubmissionService(ITricyclePointSubmissionRepository repository)
    : ITricyclePointSubmissionService
{
    private const int MaxProofUrlLength = 1000;
    private const int MaxSuggestedTodaNameLength = 200;
    private const int MaxSuggestedLandmarkLength = 300;
    private const decimal MaxReasonableAccuracyMeters = 100000m;

    public async Task<TricyclePointSubmissionMutationResult> CreateAsync(
        Guid userId,
        CreateTricyclePointSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(userId, request);
        if (errors.Count > 0)
        {
            return new TricyclePointSubmissionMutationResult(false, errors, null);
        }

        var now = DateTimeOffset.UtcNow;
        var submission = new TricyclePointSubmission
        {
            SubmittedByUserId = userId,
            ProofImageUrl = request.ProofImageUrl.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            LocationCapturedAt = request.LocationCapturedAt.ToUniversalTime(),
            SuggestedTodaName = NormalizeOptional(request.SuggestedTodaName),
            SuggestedLandmark = NormalizeOptional(request.SuggestedLandmark),
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await repository.AddAsync(submission, cancellationToken);
        return TricyclePointSubmissionMutationResult.Success(Map(saved));
    }

    public async Task<TricyclePointSubmissionResponse?> GetByIdForUserAsync(
        Guid userId,
        long submissionId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || submissionId <= 0)
        {
            return null;
        }

        var submission = await repository.GetByIdAsync(submissionId, cancellationToken);
        return submission is null || submission.SubmittedByUserId != userId
            ? null
            : Map(submission);
    }

    public async Task<IReadOnlyList<TricyclePointSubmissionResponse>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var submissions = await repository.GetByUserAsync(userId, cancellationToken);
        return submissions.Select(Map).ToArray();
    }

    private static List<string> Validate(Guid userId, CreateTricyclePointSubmissionRequest request)
    {
        var errors = new List<string>();
        var proofUrl = request.ProofImageUrl?.Trim();
        if (userId == Guid.Empty)
        {
            errors.Add("A valid authenticated user is required.");
        }

        if (string.IsNullOrWhiteSpace(proofUrl))
        {
            errors.Add("A proof image is required.");
        }
        else if (proofUrl.Length > MaxProofUrlLength)
        {
            errors.Add($"Proof image URL must be {MaxProofUrlLength} characters or fewer.");
        }

        if (request.Latitude is < -90m or > 90m)
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180m or > 180m)
        {
            errors.Add("Longitude must be between -180 and 180.");
        }

        if (request.AccuracyMeters is < 0m or > MaxReasonableAccuracyMeters)
        {
            errors.Add("Location accuracy must be between 0 and 100000 meters when provided.");
        }

        if (request.LocationCapturedAt == default)
        {
            errors.Add("Location capture time is required.");
        }
        else if (request.LocationCapturedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            errors.Add("Location capture time cannot be in the future.");
        }

        AddOptionalLengthError(errors, request.SuggestedTodaName, MaxSuggestedTodaNameLength, "Suggested TODA name");
        AddOptionalLengthError(errors, request.SuggestedLandmark, MaxSuggestedLandmarkLength, "Suggested landmark");
        return errors;
    }

    private static void AddOptionalLengthError(
        ICollection<string> errors,
        string? value,
        int maxLength,
        string label)
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

    private static TricyclePointSubmissionResponse Map(TricyclePointSubmission submission) =>
        new(
            submission.TricyclePointSubmissionId,
            submission.ProofImageUrl,
            submission.Latitude,
            submission.Longitude,
            submission.AccuracyMeters,
            submission.LocationCapturedAt,
            submission.SuggestedTodaName,
            submission.SuggestedLandmark,
            submission.Status,
            submission.CreatedAt,
            submission.UpdatedAt,
            submission.ReviewedAt,
            submission.PublishedTricyclePointId);
}
