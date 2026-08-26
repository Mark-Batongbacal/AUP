using System.ComponentModel.DataAnnotations;

namespace Tuki.Admin.Models.TricycleSubmissions;

public sealed record AdminTricycleSubmission(
    long TricyclePointSubmissionId,
    Guid SubmittedByUserId,
    string ProofImageUrl,
    decimal Latitude,
    decimal Longitude,
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

public sealed record AdminTricycleSubmissionPage(
    IReadOnlyList<AdminTricycleSubmission> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed class AdminTricycleReviewRequest
{
    [Required]
    public decimal? Latitude { get; set; }

    [Required]
    public decimal? Longitude { get; set; }

    [StringLength(200)]
    public string? PointName { get; set; }

    [StringLength(200)]
    public string? OperatorName { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(300)]
    public string? Landmark { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? AdminNotes { get; set; }
}

public sealed record AdminDecisionRequest(string Reason);
public sealed record BackendErrorResponse(IReadOnlyList<string> Errors);
public sealed record ProofImageContent(byte[] Bytes, string ContentType);

public sealed record AdminRepositoryResult<T>(
    bool Succeeded,
    int StatusCode,
    T? Value,
    string? ErrorMessage)
{
    public static AdminRepositoryResult<T> Success(T value, int statusCode = 200) =>
        new(true, statusCode, value, null);

    public static AdminRepositoryResult<T> Failure(int statusCode, string message) =>
        new(false, statusCode, default, message);
}
