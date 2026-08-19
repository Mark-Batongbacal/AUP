using backend.Models.Database;
using backend.Models.Destinations;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Routing;
using backend.Services.Telemetry;
using Microsoft.Extensions.Options;

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
    IValhallaService? roadRouting = null,
    ITukiTelemetry? telemetry = null) : ILandmarkCorridorPrefetchService
{
    private readonly NavigationOptions _navigation = navigationOptions.Value;
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;

    public async Task PrefetchAsync(TripSession session, CancellationToken cancellationToken = default)
    {
        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var cached = new List<TripLandmarkCandidate>();
        foreach (var leg in legs.Where(IsTransitLeg))
        {
            var boardLatitude = leg.StartLatitude;
            var boardLongitude = leg.StartLongitude;
            var alightLatitude = leg.EndLatitude;
            var alightLongitude = leg.EndLongitude;
            if (boardLatitude is null || boardLongitude is null ||
                alightLatitude is null || alightLongitude is null) continue;

            var boardPlaces = await DiscoverAsync(session, leg,
                boardLatitude.Value, boardLongitude.Value, "board", cancellationToken);
            var alightPlaces = await DiscoverAsync(session, leg,
                alightLatitude.Value, alightLongitude.Value, "alight", cancellationToken);
            var geometry = await GeometryAsync(leg, cancellationToken);
            if (geometry.Count < 2)
            {
                var directBoard = boardPlaces.Where(IsRecognizable)
                    .Where(item => DistanceMeters(item.Latitude, item.Longitude,
                        boardLatitude.Value, boardLongitude.Value) <=
                            _navigation.BoardReferenceMaximumDistanceMeters)
                    .OrderBy(item => Priority(item.Category))
                    .ThenBy(item => NameQuality(item.Name))
                    .ThenBy(item => DistanceMeters(item.Latitude, item.Longitude,
                        boardLatitude.Value, boardLongitude.Value))
                    .FirstOrDefault();
                if (directBoard is not null)
                    cached.Add(MapDirect(session, leg, directBoard,
                        DistanceMeters(directBoard.Latitude, directBoard.Longitude,
                            boardLatitude.Value, boardLongitude.Value)));
                continue;
            }

            var legStart = matcher.ProjectProgress(geometry,
                leg.StartLatitude ?? geometry[0].Latitude, leg.StartLongitude ?? geometry[0].Longitude);
            var legEnd = matcher.ProjectProgress(geometry,
                leg.EndLatitude ?? geometry[^1].Latitude, leg.EndLongitude ?? geometry[^1].Longitude);
            if (legEnd < legStart) (legStart, legEnd) = (legEnd, legStart);

            var projected = boardPlaces.Concat(alightPlaces)
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .Where(IsRecognizable)
                .Select(place => new ProjectedPlace(place,
                    matcher.ProjectClosest(geometry, place.Latitude, place.Longitude)))
                .Where(item => item.Match is not null &&
                    item.Match.DistanceFromGeometryMeters <= _navigation.MaximumLandmarkProjectionMeters)
                .ToList();
            var boardIds = boardPlaces.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var alightIds = alightPlaces.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

            var board = projected
                .Where(item => boardIds.Contains(item.Place.Id) &&
                    DistanceMeters(item.Place.Latitude, item.Place.Longitude,
                        leg.StartLatitude ?? geometry[0].Latitude,
                        leg.StartLongitude ?? geometry[0].Longitude) <=
                            _navigation.BoardReferenceMaximumDistanceMeters &&
                    Math.Abs(item.Match!.DistanceFromRouteStartMeters - legStart) <=
                        _navigation.BoardReferenceMaximumDistanceMeters)
                .OrderBy(item => Priority(item.Place.Category))
                .ThenBy(item => NameQuality(item.Place.Name))
                .ThenBy(item => Math.Abs(item.Match!.DistanceFromRouteStartMeters - legStart))
                .FirstOrDefault();
            if (board is not null)
                cached.Add(Map(session, leg, board, LandmarkRole.BoardReference,
                    LandmarkRelation.NearBoardPoint,
                    Math.Abs(board.Match!.DistanceFromRouteStartMeters - legStart)));

            // Route progress, not straight-line proximity, defines BEFORE_ALIGHT.
            var alight = projected
                .Where(item => alightIds.Contains(item.Place.Id) && item.Place.Id != board?.Place.Id &&
                    item.Match!.DistanceFromRouteStartMeters >= Math.Max(
                        legStart, legEnd - _navigation.LandmarkLookbackFromAlightMeters) &&
                    item.Match.DistanceFromRouteStartMeters <=
                        legEnd - _navigation.MinimumAlightReferenceLeadMeters)
                .OrderBy(item => Priority(item.Place.Category))
                .ThenBy(item => NameQuality(item.Place.Name))
                .ThenBy(item => legEnd - item.Match!.DistanceFromRouteStartMeters)
                .FirstOrDefault();
            if (alight is not null)
                cached.Add(Map(session, leg, alight, LandmarkRole.AlightReference,
                    LandmarkRelation.BeforeAlight,
                    legEnd - alight.Match!.DistanceFromRouteStartMeters));

            var semanticIds = cached.Where(item => item.LegIndex == leg.LegOrder)
                .Select(item => item.ExternalPlaceId).ToHashSet(StringComparer.Ordinal);
            var semanticCount = semanticIds.Count;
            var progressLimit = Math.Max(0, _navigation.MaximumLandmarksPerLeg - semanticCount);
            var progress = new List<ProjectedPlace>();
            foreach (var item in projected
                .Where(item => !semanticIds.Contains(item.Place.Id) &&
                    item.Match!.DistanceFromRouteStartMeters >= legStart &&
                    item.Match.DistanceFromRouteStartMeters <= legEnd)
                .OrderBy(item => Priority(item.Place.Category))
                .ThenBy(item => NameQuality(item.Place.Name))
                .ThenBy(item => item.Match!.DistanceFromRouteStartMeters))
            {
                if (progressLimit == 0) break;
                if (cached.Any(existing => existing.LegIndex == leg.LegOrder &&
                        Math.Abs(existing.DistanceFromRouteStartMeters -
                            item.Match!.DistanceFromRouteStartMeters) < _navigation.MinimumLandmarkSeparationMeters) ||
                    progress.Any(existing => Math.Abs(existing.Match!.DistanceFromRouteStartMeters -
                        item.Match!.DistanceFromRouteStartMeters) < _navigation.MinimumLandmarkSeparationMeters))
                    continue;
                progress.Add(item);
                if (progress.Count >= progressLimit) break;
            }
            cached.AddRange(progress.Select(item => Map(session, leg, item,
                LandmarkRole.ProgressReference, LandmarkRelation.AlongRoute, 0)));
            var selectedCount = cached.Count(item => item.LegIndex == leg.LegOrder);
            _telemetry.Event(selectedCount == 0 ? "LandmarkSelectionEmpty" : "LandmarkSelected",
                session.TripSessionId, selectedCount.ToString());
        }
        await cache.ReplaceAsync(session.TripSessionId, cached, cancellationToken);
    }

    private async Task<List<(double Latitude, double Longitude)>> GeometryAsync(
        RecommendationLeg leg, CancellationToken cancellationToken)
    {
        if (leg.RouteId is { } routeId)
            return (await routePoints.GetOrderedByRouteAsync(routeId, cancellationToken))
                .Select(point => (point.Latitude, point.Longitude)).ToList();
        if (roadRouting is null || leg.StartLatitude is not { } startLatitude ||
            leg.StartLongitude is not { } startLongitude || leg.EndLatitude is not { } endLatitude ||
            leg.EndLongitude is not { } endLongitude) return [];
        try
        {
            var route = await roadRouting.GetRouteAsync(startLatitude, startLongitude,
                endLatitude, endLongitude, _navigation.TricycleRoadCosting, cancellationToken);
            return route.Trip?.Legs.SelectMany(item => item.Points)
                .Where(point => point.Length >= 2)
                .Select(point => (Latitude: point[1], Longitude: point[0])).ToList() ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Tricycle landmark route geometry failed for leg {LegIndex}", leg.LegOrder);
            return [];
        }
    }

    private async Task<IReadOnlyList<DestinationSearchResult>> DiscoverAsync(
        TripSession session, RecommendationLeg leg, double latitude, double longitude,
        string purpose, CancellationToken cancellationToken)
    {
        try
        {
            var result = await places.FindNearbyVenuesAsync(latitude, longitude, cancellationToken) ?? [];
            _telemetry.Event("LandmarkCandidatesFetched", session.TripSessionId,
                $"{purpose}:{result.Count}");
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Landmark {Purpose} lookup failed for session {TripSessionId}, leg {LegIndex}",
                purpose, session.TripSessionId, leg.LegOrder);
            _telemetry.Event("PeliasLandmarkLookupFailed", session.TripSessionId, purpose);
            return [];
        }
    }

    private static TripLandmarkCandidate Map(TripSession session, RecommendationLeg leg,
        ProjectedPlace item, LandmarkRole role, LandmarkRelation relation, double targetDistance) => new()
    {
        TripSessionId = session.TripSessionId,
        LegIndex = leg.LegOrder,
        ExternalPlaceId = item.Place.Id,
        Name = item.Place.Name,
        Category = item.Place.Category,
        Role = role,
        Relation = relation,
        Latitude = item.Place.Latitude,
        Longitude = item.Place.Longitude,
        DistanceFromRouteStartMeters = item.Match!.DistanceFromRouteStartMeters,
        DistanceFromTargetMeters = targetDistance,
        TriggerBeforeMeters = 35,
        TriggerAfterMeters = 25,
        CachedAt = DateTime.UtcNow
    };

    private static TripLandmarkCandidate MapDirect(TripSession session, RecommendationLeg leg,
        DestinationSearchResult place, double targetDistance) => new()
    {
        TripSessionId = session.TripSessionId,
        LegIndex = leg.LegOrder,
        ExternalPlaceId = place.Id,
        Name = place.Name,
        Category = place.Category,
        Role = LandmarkRole.BoardReference,
        Relation = LandmarkRelation.NearBoardPoint,
        Latitude = place.Latitude,
        Longitude = place.Longitude,
        DistanceFromRouteStartMeters = 0,
        DistanceFromTargetMeters = targetDistance,
        TriggerBeforeMeters = 35,
        TriggerAfterMeters = 25,
        CachedAt = DateTime.UtcNow
    };

    private static int Priority(string category) => category.ToLowerInvariant() switch
    {
        "terminal" or "mall" or "hospital" => 0,
        "school" or "church" or "intersection" or "fast_food" or "restaurant" => 1,
        "commercial" or "public_building" => 2,
        _ => 3
    };

    private static bool IsRecognizable(DestinationSearchResult place)
    {
        var normalized = place.Name.Trim().ToLowerInvariant();
        return normalized.Length >= 4 && normalized is not
            ("unknown" or "unnamed" or "bench" or "toilets" or "toilet" or "parking" or "building");
    }

    private static int NameQuality(string name) =>
        name.Any(char.IsLetter) && name.Contains(' ') ? 0 : 1;

    private static double DistanceMeters(double latitude1, double longitude1,
        double latitude2, double longitude2)
    {
        const double earthRadius = 6_371_000;
        var lat1 = latitude1 * Math.PI / 180;
        var lat2 = latitude2 * Math.PI / 180;
        var deltaLat = (latitude2 - latitude1) * Math.PI / 180;
        var deltaLon = (longitude2 - longitude1) * Math.PI / 180;
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static bool IsTransitLeg(RecommendationLeg leg) =>
        leg.TransportMode?.Code is null ||
        leg.TransportMode.Code.ToUpperInvariant() is "JEEPNEY" or "TRICYCLE" or "TRIKE";

    private sealed record ProjectedPlace(DestinationSearchResult Place, RouteMatch? Match);
}
