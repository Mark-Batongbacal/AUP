using System.ComponentModel.DataAnnotations;

namespace backend.Models.TricyclePointSubmissions;

public sealed class UpdateAdminTricyclePointSubmissionReviewRequest
{
    [Required, Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; init; }

    [Required, Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; init; }

    [StringLength(200)]
    public string? PointName { get; init; }

    [StringLength(200)]
    public string? OperatorName { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(300)]
    public string? Landmark { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [StringLength(1000)]
    public string? AdminNotes { get; init; }
}

public sealed class AdminTricyclePointSubmissionDecisionRequest
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record AdminTricyclePointSubmissionResponse(
    long TricyclePointSubmissionId,
    Guid SubmittedByUserId,
    string ProofImageUrl,
    decimal Latitude,
    decimal Longitude,
    decimal? AdminLatitude,
    decimal? AdminLongitude,
    decimal? AccuracyMeters,
    DateTimeOffset LocationCapturedAt,
    string? SuggestedTodaName,
    string? SuggestedLandmark,
    string Status,
    string? AdminPointName,
    string? AdminOperatorName,
    string? AdminAddress,
    string? AdminLandmark,
    string? AdminDescription,
    string? AdminNotes,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    long? PublishedTricyclePointId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminTricyclePointSubmissionPageResponse(
    IReadOnlyList<AdminTricyclePointSubmissionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminTricyclePointSubmissionMutationResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    IReadOnlyList<string> Errors,
    AdminTricyclePointSubmissionResponse? Submission)
{
    public static AdminTricyclePointSubmissionMutationResult Success(AdminTricyclePointSubmissionResponse submission) =>
        new(true, false, false, [], submission);

    public static AdminTricyclePointSubmissionMutationResult Missing() =>
        new(false, true, false, [], null);

    public static AdminTricyclePointSubmissionMutationResult Invalid(params string[] errors) =>
        new(false, false, false, errors, null);

    public static AdminTricyclePointSubmissionMutationResult StateConflict(string error) =>
        new(false, false, true, [error], null);
}
