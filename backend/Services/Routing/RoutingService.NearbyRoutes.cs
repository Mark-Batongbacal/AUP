using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    public async Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var candidates = new List<SampledRoutePoint>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeGeometries.ContainsKey(route.RouteId))
                continue;

            // "Nearest" must be the nearest point on the full route geometry,
            // not merely the nearest sampled point. Sampling is for search
            // efficiency elsewhere; using it here can make long/sparse routes
            // appear nearest only at one of their endpoints.
            var anchor = ProjectOntoFullRoute(
                route.RouteId,
                (latitude, longitude),
                0);
            var distanceMeters = ApproximateDistanceMeters(
                latitude,
                longitude,
                anchor.Latitude,
                anchor.Longitude);

            candidates.Add(new SampledRoutePoint(
                route.RouteId,
                new NearbyJeepneyResponse
                {
                    RouteId = route.RouteId,
                    RouteName = route.RouteName,
                    RouteDistanceMeters = distanceMeters,
                    NearestPointLatitude = anchor.Latitude,
                    NearestPointLongitude = anchor.Longitude
                }));
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
                .OrderBy(candidate => candidate.Response.RouteDistanceMeters)
                .Select(candidate => candidate.Response)
                .Take(MaxNearbyRoutes)
                .ToList();
        }
    }

    private async Task<List<NearbyJeepneyResponse>> RankByWalkingDistanceAsync(
        List<SampledRoutePoint> candidates,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        candidates.Sort((a, b) =>
            a.Response.RouteDistanceMeters.CompareTo(
                b.Response.RouteDistanceMeters));

        var routeBestWalking = new Dictionary<string, NearbyJeepneyResponse>();
        var index = 0;

        while (index < candidates.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Straight-line distance is a valid lower bound for walking
            // distance. Therefore this pruning cannot discard a candidate
            // that could beat the current confirmed top-N.
            if (routeBestWalking.Count >= MaxNearbyRoutes)
            {
                var bound = routeBestWalking.Values
                    .Select(response => response.WalkingDistanceMeters)
                    .OrderBy(distance => distance)
                    .ElementAt(MaxNearbyRoutes - 1);

                if (candidates[index].Response.RouteDistanceMeters > bound)
                    break;
            }

            var chunkEnd = Math.Min(
                index + MatrixChunkSize,
                candidates.Count);

            var chunk = candidates.GetRange(
                index,
                chunkEnd - index);

            index = chunkEnd;

            var matrixResults = await GetMatrixAsync(
                new ValhallaLocation
                {
                    Lat = latitude,
                    Lon = longitude
                },
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

                if (routeBestWalking.TryGetValue(
                        sample.RouteId,
                        out var existing) &&
                    existing.WalkingDistanceMeters <= walkingDistanceMeters)
                {
                    continue;
                }

                sample.Response.WalkingDistanceMeters =
                    walkingDistanceMeters;

                sample.Response.WalkingTimeSeconds =
                    result.Time.Value;

                routeBestWalking[sample.RouteId] =
                    sample.Response;
            }
        }

        return routeBestWalking.Values
            .OrderBy(response => response.WalkingDistanceMeters)
            .ThenBy(response => response.WalkingTimeSeconds)
            .Take(MaxNearbyRoutes)
            .ToList();
    }

    // -------------------------------------------------------------------
    // Single-route trip
    // -------------------------------------------------------------------

}