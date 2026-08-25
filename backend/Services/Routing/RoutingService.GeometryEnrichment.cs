using backend.Models.Routing;

namespace backend.Services.Routing;

public interface IJourneyGeometryEnricher
{
    Task EnrichSelectedPlanGeometryAsync(
        IReadOnlyList<JeepneyTripPlan> plans,
        CancellationToken cancellationToken = default);
}

public partial class RoutingService : IJourneyGeometryEnricher
{
    public async Task EnrichSelectedPlanGeometryAsync(
        IReadOnlyList<JeepneyTripPlan> plans,
        CancellationToken cancellationToken = default)
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

        if (pending.Count > 0)
        {
            await Task.WhenAll(pending.Select(item => item.Task));

            foreach (var item in pending)
                item.Leg.Geometry = await item.Task;
        }

        // Geometry is presentation/navigation data, but it still has to obey
        // the physical leg chain returned by the planner. Anchor every shape to
        // its leg endpoints, then validate the complete chain once more.
        foreach (var plan in plans)
            NormalizeAndValidatePlanGeometry(plan);
    }

    private List<RouteGeometryPoint> ExtractJeepneyGeometry(JeepneyTripLeg leg)
    {
        var routeId = leg.RouteId!;
        var geometry = _routeGeometries[routeId];
        var (from, to) = ResolveJeepneyGeometryAnchors(leg);

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

    /// <summary>
    /// A selected jeepney leg already carries the authoritative ride distance
    /// computed from its occurrence-aware full-route anchors. Geometry
    /// enrichment must not throw that information away by re-projecting the
    /// board coordinate from segment zero: on loops/retraced routes the same
    /// physical coordinate can occur multiple times. Resolve the physically
    /// nearby board/alight occurrences whose route-progress span best matches
    /// the planned jeepney distance, then slice only that occurrence range.
    /// </summary>
    private (RouteAnchor From, RouteAnchor To) ResolveJeepneyGeometryAnchors(
        JeepneyTripLeg leg)
    {
        var routeId = leg.RouteId!;
        var boardPoint = (leg.OriginLatitude, leg.OriginLongitude);
        var alightPoint = (leg.DestinationLatitude, leg.DestinationLongitude);
        var expectedDistanceMeters = leg.JeepneyDistanceMeters ?? leg.DistanceMeters;

        if (double.IsFinite(expectedDistanceMeters) && expectedDistanceMeters > 0)
        {
            var boardOccurrences = GetNearbyRouteOccurrenceProjections(
                routeId,
                boardPoint);
            var alightOccurrences = GetNearbyRouteOccurrenceProjections(
                routeId,
                alightPoint);

            var bestPair = boardOccurrences
                .SelectMany(board => alightOccurrences
                    .Where(alight =>
                        alight.Anchor.DistanceFromRouteStartMeters >=
                        board.Anchor.DistanceFromRouteStartMeters)
                    .Select(alight => new
                    {
                        From = board.Anchor,
                        To = alight.Anchor,
                        DistanceErrorMeters = Math.Abs(
                            RouteDistanceBetweenAnchors(board.Anchor, alight.Anchor) -
                            expectedDistanceMeters),
                        EndpointGapMeters = board.GapMeters + alight.GapMeters
                    }))
                .OrderBy(pair => pair.DistanceErrorMeters)
                .ThenBy(pair => pair.EndpointGapMeters)
                .ThenBy(pair => pair.From.DistanceFromRouteStartMeters)
                .FirstOrDefault();

            if (bestPair is not null)
                return (bestPair.From, bestPair.To);
        }

        // Backward-compatible fallback for legacy/manually-created legs that
        // do not carry a usable planned jeepney distance.
        var from = ProjectOntoFullRoute(routeId, boardPoint, 0);
        var to = ProjectOntoFullRoute(routeId, alightPoint, from.SegmentIndex);
        return (from, to);
    }

    private List<(RouteAnchor Anchor, double GapMeters)>
        GetNearbyRouteOccurrenceProjections(
            string routeId,
            (double Latitude, double Longitude) point)
    {
        var geometry = _routeGeometries[routeId];
        var projections = new List<(RouteAnchor Anchor, double GapMeters)>();

        for (var segmentIndex = 0;
             segmentIndex < geometry.Points.Count - 1;
             segmentIndex++)
        {
            var anchor = ProjectOntoFullRoute(
                routeId,
                point,
                segmentIndex,
                segmentIndex + 1);
            var gapMeters = ApproximateDistanceMeters(
                point.Latitude,
                point.Longitude,
                anchor.Latitude,
                anchor.Longitude);
            projections.Add((anchor, gapMeters));
        }

        if (projections.Count == 0)
            return [];

        var bestGapMeters = projections.Min(candidate => candidate.GapMeters);
        var occurrenceToleranceMeters = Math.Max(
            5.0,
            _options.JourneyLegContinuityToleranceMeters);

        return projections
            .Where(candidate =>
                candidate.GapMeters <= bestGapMeters + occurrenceToleranceMeters)
            .OrderBy(candidate => candidate.GapMeters)
            .ThenBy(candidate => candidate.Anchor.DistanceFromRouteStartMeters)
            .ToList();
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
