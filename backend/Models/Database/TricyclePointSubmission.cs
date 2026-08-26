namespace backend.Models.Database;

public sealed class TricyclePointSubmission
{
    public long TricyclePointSubmissionId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string ProofImageUrl { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public DateTimeOffset LocationCapturedAt { get; set; }
    public string? SuggestedTodaName { get; set; }
    public string? SuggestedLandmark { get; set; }
    public string Status { get; set; } = "Pending";
    public string? AdminPointName { get; set; }
    public string? AdminOperatorName { get; set; }
    public string? AdminAddress { get; set; }
    public string? AdminLandmark { get; set; }
    public string? AdminDescription { get; set; }
    public string? AdminNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public long? PublishedTricyclePointId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
