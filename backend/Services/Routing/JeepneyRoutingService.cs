using System.Text.Json;
using backend.Models.Routing;
using backend.Models.Valhalla;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

public class JeepneyRoutingService : IJeepneyRoutingService
{
    private const int MaxNearbyRoutes = 20;
    private const int SamplePointsPerRoute = 30;

    // Should match (or stay comfortably under) Valhalla's configured
    // max_matrix_locations so a single chunk never gets rejected outright.
    private const int MatrixChunkSize = 100;

    private const double EarthRadiusMeters = 6_371_000;

    private readonly IValhallaService _valhallaService;
    private readonly ILogger<JeepneyRoutingService> _logger;
    private readonly List<StaticJeepneyRoute> _routes;

    public JeepneyRoutingService(
        IValhallaService valhallaService,
        IWebHostEnvironment environment,
        ILogger<JeepneyRoutingService> logger)
    {
        _valhallaService = valhallaService;
        _logger = logger;

        var path = Path.Combine(
            environment.ContentRootPath,
            "TestData",
            "jeepney-routes.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Static jeepney route file was not found.",
                path);
        }

        var json = File.ReadAllText(path);

        _routes = JsonSerializer.Deserialize<List<StaticJeepneyRoute>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? [];
    }

    public async Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SampledRoutePoint>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (route.Coordinates.Count < 2)
                continue;

            foreach (var point in SampleRoutePoints(route.Coordinates))
            {
                var distanceMeters = ApproximateDistanceMeters(
                    latitude,
                    longitude,
                    point.Latitude,
                    point.Longitude);

                candidates.Add(new SampledRoutePoint(
                    route.RouteId,
                    new NearbyJeepneyResponse
                    {
                        RouteId = route.RouteId,
                        RouteName = route.RouteName,
                        RouteDistanceMeters = distanceMeters,
                        NearestPointLatitude = point.Latitude,
                        NearestPointLongitude = point.Longitude
                    }));
            }
        }

        if (candidates.Count == 0)
            return [];

        try
        {
            return await RankByWalkingDistanceAsync(
                candidates,
                latitude,
                longitude,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch Valhalla walking matrix; returning straight-line ranked routes.");

            return candidates
                .GroupBy(candidate => candidate.RouteId)
                .Select(group => group
                    .OrderBy(candidate => candidate.Response.RouteDistanceMeters)
                    .First()
                    .Response)
                .OrderBy(candidate => candidate.RouteDistanceMeters)
                .Take(MaxNearbyRoutes)
                .ToList();
        }
    }

    /// <summary>
    /// Confirms real walking distances via Valhalla, closest-by-straight-line
    /// first, and stops early once the top <see cref="MaxNearbyRoutes"/> routes
    /// can no longer be beaten by anything left unconfirmed. This works because
    /// walking distance can never be shorter than straight-line distance
    /// (triangle inequality) — so once a candidate's straight-line distance
    /// alone exceeds the current worst confirmed top-N distance, its real
    /// walking distance is guaranteed to exceed it too, and it can be skipped
    /// without ever spending a matrix call on it.
    /// </summary>
    private async Task<List<NearbyJeepneyResponse>> RankByWalkingDistanceAsync(
        List<SampledRoutePoint> candidates,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        candidates.Sort((a, b) =>
            a.Response.RouteDistanceMeters.CompareTo(b.Response.RouteDistanceMeters));

        var routeBestWalking = new Dictionary<string, NearbyJeepneyResponse>();
        var index = 0;

        while (index < candidates.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (routeBestWalking.Count >= MaxNearbyRoutes)
            {
                var bound = routeBestWalking.Values
                    .Select(response => response.WalkingDistanceMeters)
                    .OrderBy(distance => distance)
                    .ElementAt(MaxNearbyRoutes - 1);

                if (candidates[index].Response.RouteDistanceMeters > bound)
                    break;
            }

            var chunkEnd = Math.Min(index + MatrixChunkSize, candidates.Count);
            var chunk = candidates.GetRange(index, chunkEnd - index);
            index = chunkEnd;

            var matrixResults = await _valhallaService.GetMatrixAsync(
                new ValhallaLocation { Lat = latitude, Lon = longitude },
                chunk.Select(candidate => new ValhallaLocation
                {
                    Lat = candidate.Response.NearestPointLatitude,
                    Lon = candidate.Response.NearestPointLongitude
                }).ToList(),
                "pedestrian",
                cancellationToken);

            foreach (var result in matrixResults)
            {
                if (result.FromIndex != 0 ||
                    result.ToIndex < 0 ||
                    result.ToIndex >= chunk.Count ||
                    result.Distance is null ||
                    result.Time is null)
                {
                    continue;
                }

                var sample = chunk[result.ToIndex];
                var walkingDistanceMeters = result.Distance.Value * 1_000;

                if (routeBestWalking.TryGetValue(sample.RouteId, out var existing) &&
                    existing.WalkingDistanceMeters <= walkingDistanceMeters)
                {
                    continue;
                }

                sample.Response.WalkingDistanceMeters = walkingDistanceMeters;
                sample.Response.WalkingTimeSeconds = result.Time.Value;
                routeBestWalking[sample.RouteId] = sample.Response;
            }
        }

        return routeBestWalking.Values
            .OrderBy(response => response.WalkingDistanceMeters)
            .ThenBy(response => response.WalkingTimeSeconds)
            .Take(MaxNearbyRoutes)
            .ToList();
    }

    private static IEnumerable<(double Latitude, double Longitude)> SampleRoutePoints(
        IReadOnlyList<double[]> routeCoordinates)
    {
        var numberOfSamples = Math.Min(SamplePointsPerRoute, routeCoordinates.Count);

        for (var sample = 0; sample < numberOfSamples; sample++)
        {
            var index = numberOfSamples == 1
                ? 0
                : (int)Math.Round(
                    sample * (routeCoordinates.Count - 1d) / (numberOfSamples - 1),
                    MidpointRounding.AwayFromZero);
            var coordinate = routeCoordinates[index];

            // Static jeepney route coordinates are [longitude, latitude].
            yield return (coordinate[1], coordinate[0]);
        }
    }

    private sealed record SampledRoutePoint(
        string RouteId,
        NearbyJeepneyResponse Response);

    private static double ApproximateDistanceMeters(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;

        var deltaLat = (lat2 - lat1) * Math.PI / 180;
        var deltaLon = (lon2 - lon1) * Math.PI / 180;

        var a =
            Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1Rad) *
            Math.Cos(lat2Rad) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }
}
