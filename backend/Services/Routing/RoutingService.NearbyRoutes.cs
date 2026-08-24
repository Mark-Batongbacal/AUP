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

            if (!_routeSamples.TryGetValue(route.RouteId, out var samples) ||
                !_routeGeometries.ContainsKey(route.RouteId))
            {
                continue;
            }

            // Always include the exact nearest point on the full route. The
            // sampled points remain as alternatives because a slightly farther
            // point can have much better real pedestrian access (bridge,
            // divided road, wall, etc.). This prevents sparse sampling from
            // making a route endpoint look like the only "nearest" point while
            // preserving Valhalla as the authority for walkability.
            var exact = ProjectOntoFullRoute(
                route.RouteId,
                (latitude, longitude),
                0);
            AddCandidate(exact.Latitude, exact.Longitude);

            foreach (var point in samples)
                AddCandidate(point.Latitude, point.Longitude);

            void AddCandidate(double pointLatitude, double pointLongitude)
            {
                if (candidates.Any(candidate =>
                        candidate.RouteId == route.RouteId &&
                        ApproximateDistanceMeters(
                            candidate.Response.NearestPointLatitude,
                            candidate.Response.NearestPointLongitude,
                            pointLatitude,
                            pointLongitude) <= 1.0))
                {
                    return;
                }

                var distanceMeters = ApproximateDistanceMeters(
                    latitude,
                    longitude,
                    pointLatitude,
                    pointLongitude);

                candidates.Add(new SampledRoutePoint(
                    route.RouteId,
                    new NearbyJeepneyResponse
                    {
                        RouteId = route.RouteId,
                        RouteName = route.RouteName,
                        RouteDistanceMeters = distanceMeters,
                        NearestPointLatitude = pointLatitude,
                        NearestPointLongitude = pointLongitude
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