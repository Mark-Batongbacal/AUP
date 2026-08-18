namespace backend.Models.Destinations;

public sealed record DestinationSearchResult(
    string Id,
    string Name,
    double Latitude,
    double Longitude,
    string Category,
    string Source,
    string? Address = null);

public sealed record DestinationSearchResponse(
    IReadOnlyList<DestinationSearchResult> Results,
    string? Error = null,
    string? Message = null);
