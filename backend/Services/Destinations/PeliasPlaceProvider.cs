using System.Net.Http.Json;
using System.Text.Json.Serialization;
using backend.Models.Destinations;
using Microsoft.Extensions.Options;

namespace backend.Services.Destinations;

public interface IPlaceLandmarkDiscoveryService
{
    Task<IReadOnlyList<DestinationSearchResult>> FindNearbyVenuesAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);
}

public interface IReverseGeocodingService
{
    Task<DestinationSearchResult?> ReverseAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);
}

public sealed class PeliasPlaceProvider(
    HttpClient client, IOptions<PeliasOptions> options,
    ILogger<PeliasPlaceProvider> logger)
    : IDestinationSearchProvider, IPlaceLandmarkDiscoveryService, IReverseGeocodingService
{
    private readonly PeliasOptions _options = options.Value;

    public Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query, DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<(string Key, string Value)>
        {
            ("text", query), ("size", _options.DestinationResultLimit.ToString())
        };
        if (context?.FocusLatitude is { } lat && context.FocusLongitude is { } lon &&
            lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
        {
            parameters.Add(("focus.point.lat", FormattableString.Invariant($"{lat}")));
            parameters.Add(("focus.point.lon", FormattableString.Invariant($"{lon}")));
        }
        return SendAsync("v1/autocomplete", parameters, cancellationToken);
    }

    public Task<IReadOnlyList<DestinationSearchResult>> FindNearbyVenuesAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default) =>
        SendAsync("v1/reverse",
        [
            ("point.lat", FormattableString.Invariant($"{latitude}")),
            ("point.lon", FormattableString.Invariant($"{longitude}")),
            ("layers", "venue"), ("sources", "openstreetmap"),
            ("boundary.circle.radius", FormattableString.Invariant($"{_options.LandmarkSearchRadiusKilometers}")),
            ("size", _options.LandmarkCandidateCount.ToString())
        ], cancellationToken);

    public async Task<DestinationSearchResult?> ReverseAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var results = await SendAsync("v1/reverse",
        [
            ("point.lat", FormattableString.Invariant($"{latitude}")),
            ("point.lon", FormattableString.Invariant($"{longitude}")),
            ("sources", "openstreetmap"),
            ("size", "1")
        ], cancellationToken);
        return results.FirstOrDefault();
    }

    private async Task<IReadOnlyList<DestinationSearchResult>> SendAsync(
        string path, IEnumerable<(string Key, string Value)> parameters,
        CancellationToken cancellationToken)
    {
        var query = string.Join('&', parameters.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        try
        {
            var response = await client.GetFromJsonAsync<PeliasResponse>(
                $"{path}?{query}", cancellationToken);
            return response?.Features.Select(Map).Where(item => item is not null)
                .Cast<DestinationSearchResult>().ToList() ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Pelias request failed for {Path}", path);
            throw new DestinationProviderUnavailableException("Pelias is unavailable.", exception);
        }
    }

    private static DestinationSearchResult? Map(PeliasFeature feature)
    {
        if (feature.Geometry?.Coordinates is not { Count: >= 2 } coordinates ||
            string.IsNullOrWhiteSpace(feature.Properties?.Gid)) return null;
        var properties = feature.Properties;
        var name = properties.Name ?? properties.Label;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var locality = FirstNonBlank(
            properties.Locality,
            properties.LocalAdmin,
            properties.Borough,
            properties.County,
            properties.Region);
        return new(properties.Gid, name, coordinates[1], coordinates[0],
            properties.Categories.FirstOrDefault() ?? properties.Layer ?? "place",
            "pelias", properties.Label, locality);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private sealed class PeliasResponse
    {
        [JsonPropertyName("features")] public List<PeliasFeature> Features { get; set; } = [];
    }
    private sealed class PeliasFeature
    {
        [JsonPropertyName("geometry")] public PeliasGeometry? Geometry { get; set; }
        [JsonPropertyName("properties")] public PeliasProperties? Properties { get; set; }
    }
    private sealed class PeliasGeometry
    {
        [JsonPropertyName("coordinates")] public List<double> Coordinates { get; set; } = [];
    }
    private sealed class PeliasProperties
    {
        [JsonPropertyName("gid")] public string Gid { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("layer")] public string? Layer { get; set; }
        [JsonPropertyName("category")] public List<string> Categories { get; set; } = [];
        [JsonPropertyName("locality")] public string? Locality { get; set; }
        [JsonPropertyName("localadmin")] public string? LocalAdmin { get; set; }
        [JsonPropertyName("borough")] public string? Borough { get; set; }
        [JsonPropertyName("county")] public string? County { get; set; }
        [JsonPropertyName("region")] public string? Region { get; set; }
    }
}
