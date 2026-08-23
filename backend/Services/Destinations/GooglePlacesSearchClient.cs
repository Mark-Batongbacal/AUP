using System.Net.Http.Json;
using System.Text.Json.Serialization;
using backend.Models.Destinations;

namespace backend.Services.Destinations;

public static class GooglePlacesSearchClient
{
    private const string ApiKeyEnvironmentVariable = "GOOGLE_PLACES_API_KEY";
    private const string SearchEndpoint = "https://places.googleapis.com/v1/places:searchText";
    private const string FieldMask =
        "places.id,places.displayName,places.formattedAddress,places.location,places.primaryType";
    private const int ResultLimit = 8;
    private const double SearchRadiusMeters = 15_000;

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static async Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query,
        DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new DestinationProviderUnavailableException(
                $"Google Places is not configured. Set {ApiKeyEnvironmentVariable} on the backend.");

        var body = new Dictionary<string, object>
        {
            ["textQuery"] = query,
            ["pageSize"] = ResultLimit,
            ["regionCode"] = "PH"
        };

        if (context?.FocusLatitude is { } latitude && context.FocusLongitude is { } longitude &&
            latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
        {
            body["locationBias"] = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = SearchRadiusMeters
                }
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", FieldMask);

        try
        {
            using var response = await Client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<GooglePlacesResponse>(
                cancellationToken: cancellationToken);
            return payload?.Places
                .Select(Map)
                .Where(item => item is not null)
                .Cast<DestinationSearchResult>()
                .ToList() ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DestinationProviderUnavailableException(
                "Google Places search is temporarily unavailable.", exception);
        }
    }

    private static DestinationSearchResult? Map(GooglePlace place)
    {
        if (string.IsNullOrWhiteSpace(place.Id) ||
            string.IsNullOrWhiteSpace(place.DisplayName?.Text) ||
            place.Location is null)
            return null;

        return new DestinationSearchResult(
            $"google:{place.Id}",
            place.DisplayName.Text,
            place.Location.Latitude,
            place.Location.Longitude,
            string.IsNullOrWhiteSpace(place.PrimaryType) ? "place" : place.PrimaryType,
            "google",
            place.FormattedAddress);
    }

    private sealed class GooglePlacesResponse
    {
        [JsonPropertyName("places")]
        public List<GooglePlace> Places { get; set; } = [];
    }

    private sealed class GooglePlace
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public GoogleDisplayName? DisplayName { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; set; }

        [JsonPropertyName("primaryType")]
        public string? PrimaryType { get; set; }
    }

    private sealed class GoogleDisplayName
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
