using System.ComponentModel.DataAnnotations;

namespace backend.Models.TricyclePointSubmissions;

public sealed class CreateTricyclePointSubmissionRequest
{
    [Required, StringLength(1000)]
    public string ProofImageUrl { get; init; } = string.Empty;

    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public decimal? AccuracyMeters { get; init; }
    public DateTimeOffset LocationCapturedAt { get; init; }

    [StringLength(200)]
    public string? SuggestedTodaName { get; init; }

    [StringLength(300)]
    public string? SuggestedLandmark { get; init; }
}

public sealed record TricyclePointSubmissionResponse(
    long TricyclePointSubmissionId,
    string ProofImageUrl,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    DateTimeOffset LocationCapturedAt,
    string? SuggestedTodaName,
    string? SuggestedLandmark,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt,
    long? PublishedTricyclePointId);

public sealed record TricyclePointSubmissionErrorResponse(IReadOnlyList<string> Errors);
