namespace backend.Services.Destinations;

public sealed class PeliasOptions
{
    public const string SectionName = "Pelias";
    public string BaseUrl { get; init; } = string.Empty;
    public int DestinationResultLimit { get; init; } = 20;
    public int LandmarkCandidateCount { get; init; } = 30;
    public double LandmarkSearchRadiusKilometers { get; init; } = 1.0;
    public int TimeoutSeconds { get; init; } = 4;

    public bool IsValid() => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && DestinationResultLimit is > 0 and <= 100 &&
        LandmarkCandidateCount is > 0 and <= 100 && LandmarkSearchRadiusKilometers > 0 &&
        TimeoutSeconds is > 0 and <= 30;
}
