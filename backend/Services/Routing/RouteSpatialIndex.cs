using backend.Models.Routing;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace backend.Services.Routing;

internal interface IRouteSpatialIndex
{
    IReadOnlyList<string> FindNearbyRoutes(
        double latitude,
        double longitude,
        double radiusMeters);
}

/// <summary>
/// Immutable route-segment index owned by one routing-network snapshot.
/// Query results are route IDs in original network order; repeated segment
/// hits never expose duplicate routes or alter route-occurrence semantics.
/// </summary>
internal sealed class RouteSpatialIndex : IRouteSpatialIndex
{
    private const double EarthRadiusMeters = 6_371_000;
    private readonly STRtree<RouteEntry> _tree;
    private readonly string[] _routeIdsByOrdinal;

    private RouteSpatialIndex(
        STRtree<RouteEntry> tree,
        string[] routeIdsByOrdinal)
    {
        _tree = tree;
        _routeIdsByOrdinal = routeIdsByOrdinal;
    }

    public static IRouteSpatialIndex Build(
        IReadOnlyList<StaticJeepneyRoute> routes)
    {
        var tree = new STRtree<RouteEntry>();
        var routeIds = new string[routes.Count];

        for (var routeOrdinal = 0; routeOrdinal < routes.Count; routeOrdinal++)
        {
            var route = routes[routeOrdinal];
            routeIds[routeOrdinal] = route.RouteId;
            var entry = new RouteEntry(routeOrdinal);

            for (var pointIndex = 1;
                 pointIndex < route.Coordinates.Count;
                 pointIndex++)
            {
                var from = route.Coordinates[pointIndex - 1];
                var to = route.Coordinates[pointIndex];
                tree.Insert(
                    new Envelope(from[0], to[0], from[1], to[1]),
                    entry);
            }
        }

        // STRtree is query-only and safe for concurrent readers after Build.
        tree.Build();
        return new RouteSpatialIndex(tree, routeIds);
    }

    public IReadOnlyList<string> FindNearbyRoutes(
        double latitude,
        double longitude,
        double radiusMeters)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            !double.IsFinite(radiusMeters) || radiusMeters < 0 ||
            _routeIdsByOrdinal.Length == 0)
        {
            return [];
        }

        var hits = _tree.Query(SearchEnvelope(
            latitude,
            longitude,
            radiusMeters));
        if (hits.Count == 0)
            return [];

        // A bool array is cheaper than a string HashSet on this request hot
        // path and lets us restore deterministic snapshot route order.
        var seen = new bool[_routeIdsByOrdinal.Length];
        var uniqueCount = 0;
        foreach (var hit in hits)
        {
            if (seen[hit.RouteOrdinal])
                continue;

            seen[hit.RouteOrdinal] = true;
            uniqueCount++;
        }

        var result = new string[uniqueCount];
        var resultIndex = 0;
        for (var routeOrdinal = 0;
             routeOrdinal < _routeIdsByOrdinal.Length;
             routeOrdinal++)
        {
            if (seen[routeOrdinal])
                result[resultIndex++] = _routeIdsByOrdinal[routeOrdinal];
        }

        return result;
    }

    private static Envelope SearchEnvelope(
        double latitude,
        double longitude,
        double radiusMeters)
    {
        var angularRadius = radiusMeters / EarthRadiusMeters;
        var latitudeRadians = latitude * Math.PI / 180;
        var latitudeDelta = angularRadius * 180 / Math.PI;

        // This is a conservative spherical bounding box, not a distance
        // authority. Its only job is to avoid false negatives before the
        // existing projection and Valhalla checks run.
        var longitudeDelta = Math.Abs(Math.Cos(latitudeRadians)) < 1e-12
            ? 180
            : Math.Asin(Math.Min(
                    1,
                    Math.Sin(angularRadius) /
                    Math.Abs(Math.Cos(latitudeRadians)))) *
                180 / Math.PI;

        return new Envelope(
            longitude - longitudeDelta,
            longitude + longitudeDelta,
            latitude - latitudeDelta,
            latitude + latitudeDelta);
    }

    private readonly record struct RouteEntry(int RouteOrdinal);
}
