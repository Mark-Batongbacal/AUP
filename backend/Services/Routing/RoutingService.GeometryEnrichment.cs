using backend.Models.Routing;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private async Task EnrichSelectedPlanGeometryAsync(
        IReadOnlyList<JeepneyTripPlan> plans,
        CancellationToken cancellationToken)
    {
        var roadGeometryTasks = new Dictionary<string, Task<List<RouteGeometryPoint>>>(
            StringComparer.Ordinal);
        var pending = new List<(JeepneyTripLeg Leg, Task<List<RouteGeometryPoint>> Task)>();

        foreach (var plan in plans)
        {
            foreach (var leg in plan.Legs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (leg.Mode == AccessMode.Jeepney &&
                    !string.IsNullOrWhiteSpace(leg.RouteId) &&
                    _routeGeometries.ContainsKey(leg.RouteId))
                {
                    leg.Geometry = ExtractJeepneyGeometry(leg);
                    continue;
                }

                var costing = leg.Mode == AccessMode.Walk
                    ? "pedestrian"
                    : TrikeCostingModel;
                var key = GeometryRequestKey(leg, costing);

                if (!roadGeometryTasks.TryGetValue(key, out var task))
                {
                    task = GetRoadGeometryAsync(leg, costing, cancellationToken);
                    roadGeometryTasks[key] = task;
                }

                pending.Add((leg, task));
            }
        }

        if (pending.Count == 0)
            return;

        await Task.WhenAll(pending.Select(item => item.Task));

        foreach (var item in pending)
            item.Leg.Geometry = await item.Task;
    }

    private List<RouteGeometryPoint> ExtractJeepneyGeometry(JeepneyTripLeg leg)
    {
        var routeId = leg.RouteId!;
        var geometry = _routeGeometries[routeId];
        var from = ProjectOntoFullRoute(
            routeId,
            (leg.OriginLatitude, leg.OriginLongitude),
            0);
        var to = ProjectOntoFullRoute(
            routeId,
            (leg.DestinationLatitude, leg.DestinationLongitude),
            from.SegmentIndex);

        if (to.DistanceFromRouteStartMeters < from.DistanceFromRouteStartMeters)
            return EndpointGeometry(leg);

        var points = new List<RouteGeometryPoint>
        {
            new(from.Latitude, from.Longitude)
        };

        for (var pointIndex = from.SegmentIndex + 1;
             pointIndex <= to.SegmentIndex && pointIndex < geometry.Points.Count;
             pointIndex++)
        {
            var point = geometry.Points[pointIndex];
            AddDistinct(points, new RouteGeometryPoint(point.Latitude, point.Longitude));
        }

        AddDistinct(points, new RouteGeometryPoint(to.Latitude, to.Longitude));
        return points.Count >= 2 ? points : EndpointGeometry(leg);
    }

    private async Task<List<RouteGeometryPoint>> GetRoadGeometryAsync(
        JeepneyTripLeg leg,
        string costing,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _valhallaService.GetRouteAsync(
                leg.OriginLatitude,
                leg.OriginLongitude,
                leg.DestinationLatitude,
                leg.DestinationLongitude,
                costing,
                cancellationToken);

            var points = new List<RouteGeometryPoint>();
            foreach (var point in response.Trip?.Legs.SelectMany(item => item.Points) ?? [])
            {
                if (point is not { Length: >= 2 } ||
                    !double.IsFinite(point[0]) ||
                    !double.IsFinite(point[1]))
                {
                    continue;
                }

                // ValhallaService decodes route shapes into [longitude, latitude].
                AddDistinct(points, new RouteGeometryPoint(point[1], point[0]));
            }

            return points.Count >= 2 ? points : EndpointGeometry(leg);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to enrich {Mode} journey leg with route geometry; using endpoints",
                leg.Mode);
            return EndpointGeometry(leg);
        }
    }

    private static string GeometryRequestKey(JeepneyTripLeg leg, string costing) =>
        $"{costing}|{leg.OriginLatitude:F7},{leg.OriginLongitude:F7}|" +
        $"{leg.DestinationLatitude:F7},{leg.DestinationLongitude:F7}";

    private static List<RouteGeometryPoint> EndpointGeometry(JeepneyTripLeg leg) =>
        [
            new(leg.OriginLatitude, leg.OriginLongitude),
            new(leg.DestinationLatitude, leg.DestinationLongitude)
        ];

    private static void AddDistinct(
        List<RouteGeometryPoint> points,
        RouteGeometryPoint point)
    {
        if (points.Count == 0 || points[^1] != point)
            points.Add(point);
    }
}
