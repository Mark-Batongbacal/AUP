using backend.Models.Database;
using backend.Repositories;
using backend.Services.Destinations;
using Microsoft.Extensions.Options;
using backend.Services.Telemetry;

namespace backend.Services.Navigation;

public interface ILandmarkCorridorPrefetchService
{
    Task PrefetchAsync(TripSession session, CancellationToken cancellationToken = default);
}

public sealed class LandmarkCorridorPrefetchService(
    IRouteRecommendationRepository recommendations,
    IRoutePointRepository routePoints,
    IPlaceLandmarkDiscoveryService places,
    IMapMatchingService matcher,
    ITripLandmarkCandidateRepository cache,
    IOptions<NavigationOptions> navigationOptions,
    ILogger<LandmarkCorridorPrefetchService> logger,
    ITukiTelemetry? telemetry = null) : ILandmarkCorridorPrefetchService
{
    private readonly NavigationOptions _navigation = navigationOptions.Value;
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;

    public async Task PrefetchAsync(TripSession session, CancellationToken cancellationToken = default)
    {
        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var cached = new List<TripLandmarkCandidate>();
        foreach (var leg in legs.Where(item => item.RouteId.HasValue))
        {
            var geometry = (await routePoints.GetOrderedByRouteAsync(leg.RouteId!.Value, cancellationToken))
                .Select(point => (point.Latitude, point.Longitude)).ToList();
            if (geometry.Count < 2) continue;
            var legStartProgress = matcher.ProjectProgress(
                geometry, leg.StartLatitude ?? geometry[0].Latitude,
                leg.StartLongitude ?? geometry[0].Longitude);
            var legEndProgress = matcher.ProjectProgress(
                geometry, leg.EndLatitude ?? geometry[^1].Latitude,
                leg.EndLongitude ?? geometry[^1].Longitude);
            if (legEndProgress < legStartProgress)
                (legStartProgress, legEndProgress) = (legEndProgress, legStartProgress);
            IReadOnlyList<backend.Models.Destinations.DestinationSearchResult> candidates;
            try
            {
                candidates = await places.FindNearbyVenuesAsync(
                    leg.EndLatitude ?? geometry[^1].Latitude,
                    leg.EndLongitude ?? geometry[^1].Longitude, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Landmark corridor prefetch failed for session {TripSessionId}, leg {LegIndex}", session.TripSessionId, leg.LegOrder);
                _telemetry.Event("PeliasLandmarkLookupFailed", session.TripSessionId);
                continue;
            }
            _telemetry.Event("LandmarkCandidatesFetched", session.TripSessionId,
                candidates.Count.ToString());

            var projected = candidates.Select(place => (Place: place,
                    Match: matcher.ProjectClosest(geometry, place.Latitude, place.Longitude)))
                .Where(item => item.Match is not null &&
                    item.Match.DistanceFromGeometryMeters <= _navigation.MaximumLandmarkProjectionMeters &&
                    item.Match.DistanceFromRouteStartMeters >= Math.Max(
                        legStartProgress, legEndProgress - _navigation.LandmarkLookbackFromAlightMeters) &&
                    item.Match.DistanceFromRouteStartMeters <= legEndProgress)
                .Where(item => IsRecognizable(item.Place))
                .OrderBy(item => Priority(item.Place.Category))
                .ThenBy(item => NameQuality(item.Place.Name))
                .ThenBy(item => item.Match!.DistanceFromRouteStartMeters)
                .ToList();
            var selected = new List<(backend.Models.Destinations.DestinationSearchResult Place, RouteMatch? Match)>();
            foreach (var item in projected)
            {
                if (selected.Any(existing => Math.Abs(existing.Match!.DistanceFromRouteStartMeters -
                    item.Match!.DistanceFromRouteStartMeters) < _navigation.MinimumLandmarkSeparationMeters)) continue;
                selected.Add(item);
                if (selected.Count >= _navigation.MaximumLandmarksPerLeg) break;
            }
            cached.AddRange(selected.Select(item => new TripLandmarkCandidate
            {
                TripSessionId = session.TripSessionId, LegIndex = leg.LegOrder,
                ExternalPlaceId = item.Place.Id, Name = item.Place.Name,
                Category = item.Place.Category, Latitude = item.Place.Latitude,
                Longitude = item.Place.Longitude,
                DistanceFromRouteStartMeters = item.Match!.DistanceFromRouteStartMeters,
                TriggerBeforeMeters = 35, TriggerAfterMeters = 25, CachedAt = DateTime.UtcNow
            }));
            _telemetry.Event(selected.Count == 0 ? "LandmarkSelectionEmpty" : "LandmarkSelected",
                session.TripSessionId, selected.Count.ToString());
        }
        await cache.ReplaceAsync(session.TripSessionId, cached, cancellationToken);
    }

    private static int Priority(string category) => category.ToLowerInvariant() switch
    {
        "terminal" or "mall" or "hospital" => 0,
        "school" or "church" or "intersection" => 1,
        _ => 2
    };

    private static bool IsRecognizable(backend.Models.Destinations.DestinationSearchResult place)
    {
        var normalized = place.Name.Trim().ToLowerInvariant();
        return normalized.Length >= 4 && normalized is not
            ("unknown" or "unnamed" or "bench" or "toilets" or "parking" or "building");
    }

    private static int NameQuality(string name) =>
        name.Any(char.IsLetter) && name.Contains(' ') ? 0 : 1;
}
