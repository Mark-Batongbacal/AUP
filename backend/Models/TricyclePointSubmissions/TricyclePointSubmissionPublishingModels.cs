namespace backend.Models.TricyclePointSubmissions;

public sealed record TricyclePointPublicationDraft(
    string PointCode,
    string PointName,
    double Latitude,
    double Longitude,
    int RadiusMeters,
    string? Description,
    string? Address,
    string? OperatorName);

public sealed record TricyclePointSubmissionPublishResponse(
    long TricyclePointSubmissionId,
    long TricyclePointId,
    string PointCode,
    string PointName,
    string Status,
    Guid ReviewedByUserId,
    DateTimeOffset ReviewedAt);

public sealed record TricyclePointSubmissionPublishResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    IReadOnlyList<string> Errors,
    TricyclePointSubmissionPublishResponse? Publication)
{
    public static TricyclePointSubmissionPublishResult Success(
        TricyclePointSubmissionPublishResponse publication) =>
        new(true, false, false, [], publication);

    public static TricyclePointSubmissionPublishResult Missing() =>
        new(false, true, false, [], null);

    public static TricyclePointSubmissionPublishResult Invalid(params string[] errors) =>
        new(false, false, false, errors, null);

    public static TricyclePointSubmissionPublishResult StateConflict(params string[] errors) =>
        new(false, false, true, errors, null);
}
