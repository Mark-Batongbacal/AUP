namespace backend.Services.Assistant;

public sealed record AssistantLocationAssessment(
    string Reliability,
    double? AgeSeconds,
    bool CanUseForReroute);

public static class AssistantLocationPolicy
{
    public const double MaxRerouteAccuracyMeters = 75d;
    public const double CurrentMaxAgeSeconds = 30d;
    public const double LastKnownMaxAgeSeconds = 60d;

    public const string Current = "CURRENT";
    public const string LastKnown = "LAST_KNOWN";
    public const string Stale = "STALE";
    public const string Inaccurate = "INACCURATE";
    public const string Unavailable = "UNAVAILABLE";
    public const string Unknown = "UNKNOWN";

    public static AssistantLocationAssessment Assess(
        double? latitude,
        double? longitude,
        double? accuracyMeters,
        DateTime? locationAt,
        DateTime utcNow)
    {
        if (!IsCoordinateValid(latitude, longitude))
            return new(Unavailable, null, false);

        if (locationAt is null ||
            accuracyMeters is null ||
            !double.IsFinite(accuracyMeters.Value) ||
            accuracyMeters.Value < 0)
        {
            return new(Unknown, null, false);
        }

        // SQL Server datetime2 values are commonly materialized with DateTimeKind.Unspecified.
        // LastLocationAt is written from a validated UTC navigation timestamp, so preserve that
        // contract when the persisted session is loaded back from the database.
        var timestampUtc = locationAt.Value.Kind switch
        {
            DateTimeKind.Utc => locationAt.Value,
            DateTimeKind.Local => locationAt.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(locationAt.Value, DateTimeKind.Utc)
        };
        var nowUtc = utcNow.Kind switch
        {
            DateTimeKind.Utc => utcNow,
            DateTimeKind.Local => utcNow.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
        };
        var ageSeconds = (nowUtc - timestampUtc).TotalSeconds;

        // Navigation's GPS validator permits a small amount of clock skew into the future,
        // but a future timestamp is not a defensible origin for an assistant-triggered replan.
        if (!double.IsFinite(ageSeconds) || ageSeconds < 0)
            return new(Unknown, null, false);

        if (accuracyMeters.Value > MaxRerouteAccuracyMeters)
            return new(Inaccurate, ageSeconds, false);

        if (ageSeconds <= CurrentMaxAgeSeconds)
            return new(Current, ageSeconds, true);

        if (ageSeconds <= LastKnownMaxAgeSeconds)
            return new(LastKnown, ageSeconds, false);

        return new(Stale, ageSeconds, false);
    }

    private static bool IsCoordinateValid(double? latitude, double? longitude) =>
        latitude is { } lat &&
        longitude is { } lon &&
        double.IsFinite(lat) &&
        double.IsFinite(lon) &&
        lat is >= -90 and <= 90 &&
        lon is >= -180 and <= 180;
}
