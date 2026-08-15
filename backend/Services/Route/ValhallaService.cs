
using System.Net.Http.Json;
using backend.Helpers;
using backend.Models.Valhalla;

namespace backend.Services.Route;

public class ValhallaService : IValhallaService
{
    private readonly HttpClient _httpClient;

    public ValhallaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing = "car",
        CancellationToken cancellationToken = default)
    {
        var request = new ValhallaRouteRequest
        {
            Locations =
            [
                new ValhallaLocation
                {
                    Lat = startLatitude,
                    Lon = startLongitude
                },
                new ValhallaLocation
                {
                    Lat = endLatitude,
                    Lon = endLongitude
                }
            ],
            Costing = costing
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/route",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<ValhallaRouteResponse>(
                cancellationToken);

        var route = result
            ?? throw new InvalidOperationException(
                "Valhalla returned an empty response.");

        if (route.Trip is not null)
        {
            foreach (var leg in route.Trip.Legs)
            {
                leg.Points = PolylineDecoder.DecodePolyline6(leg.Shape)
                    .Select(point => new[]
                    {
                        point.Longitude,
                        point.Latitude
                    })
                    .ToList();
            }
        }

        return route;
    }

    public async Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default)
    {
        if (targets.Count == 0)
            return [];

        var request = new ValhallaMatrixRequest
        {
            Sources = [source],
            Targets = targets.ToList(),
            Costing = costing,
            Units = "kilometers",
            Verbose = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/sources_to_targets",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<ValhallaMatrixResponse>(cancellationToken);

        return result?.SourcesToTargets
            .SelectMany(row => row)
            .ToList()
            ?? throw new InvalidOperationException(
                "Valhalla returned an empty matrix response.");
    }
}
