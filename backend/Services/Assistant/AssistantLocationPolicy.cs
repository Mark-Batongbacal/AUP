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
            locationAt.Value.Kind == DateTimeKind.Unspecified ||
            accuracyMeters is null ||
            !double.IsFinite(accuracyMeters.Value) ||
            accuracyMeters.Value < 0)
        {
            return new(Unknown, null, false);
        }

        var timestampUtc = locationAt.Value.ToUniversalTime();
        var nowUtc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : utcNow.ToUniversalTime();
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
