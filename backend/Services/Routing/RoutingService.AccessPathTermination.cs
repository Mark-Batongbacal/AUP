using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Creates deferred destination-completion edges from viable origin-access
    /// states. Discovery is cheap and bounded; no completed transit itinerary
    /// is required. Valhalla later proves whether the road path to the proposed
    /// board really contains a legal, shorter termination at the destination.
    /// </summary>
    private List<AccessPathDestinationCompletionEdge>
        BuildAccessPathDestinationCompletionEdges(
            IReadOnlyDictionary<string, IReadOnlyList<AccessCandidate>[]>
                boardPrefixes,
            IReadOnlyDictionary<string, List<RouteConnectionCandidate>>
                directConnectionsByRoute,
            double destinationLatitude,
            double destinationLongitude)
    {
        var all = boardPrefixes
            .SelectMany(pair => pair.Value
                .SelectMany(states => states)
                .Select(access => (RouteId: pair.Key, Access: access)))
            .Concat(directConnectionsByRoute.SelectMany(pair => pair.Value
                .Select(connection => (
                    RouteId: pair.Key,
                    Access: connection.BoardAccess))))
            .SelectMany(item => item.Access.AllAlternatives
                .Where(access => access.Mode == AccessMode.Trike)
                .Select(access => (
                    item.RouteId,
                    Access: access with { Alternatives = null })))
            // Cheap discovery only: a destination lying on a plausible road
            // prefix has a small triangle excess between TODA, destination,
            // and board. This does not declare reachability; both full path
            // geometry and a legal direct route are still mandatory below.
            .Where(item => AccessPathProvisionalExcessMeters(
                    item.Access,
                    destinationLatitude,
                    destinationLongitude) <=
                _options.BoardingDiversityBucketMeters)
            .GroupBy(item => AccessPathEdgeKey(item.RouteId, item.Access),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.Access.GeneralizedCostPesos)
                .First())
            .ToList();

        var routeQueues = all
            .GroupBy(item => item.RouteId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Queue<AccessPathDestinationCompletionEdge>(
                group.OrderBy(item => AccessPathProvisionalExcessMeters(
                        item.Access,
                        destinationLatitude,
                        destinationLongitude))
                    .ThenBy(item => item.Access.GeneralizedCostPesos)
                    .ThenBy(item => GetAccessProgressMeters(item.Access))
                    .Select(item => new AccessPathDestinationCompletionEdge(
                        item.Access,
                        AccessPathEdgeKey(item.RouteId, item.Access)))))
            .ToList();
        var selected = new List<AccessPathDestinationCompletionEdge>();

        while (selected.Count < MaxCandidatesToConfirm)
        {
            var addedAny = false;
            foreach (var queue in routeQueues)
            {
                if (!queue.TryDequeue(out var edge))
                    continue;

                selected.Add(edge);
                addedAny = true;
                if (selected.Count >= MaxCandidatesToConfirm)
                    break;
            }

            if (!addedAny)
                break;
        }

        return selected;
    }

    private static string AccessPathEdgeKey(
        string routeId,
        AccessCandidate access) => string.Join('|',
        routeId,
        access.TrikePoint?.Id ?? string.Empty,
        Math.Round(access.Anchor.Latitude, 6),
        Math.Round(access.Anchor.Longitude, 6),
        Math.Round(GetAccessProgressMeters(access), 1));

    private static double AccessPathProvisionalExcessMeters(
        AccessCandidate access,
        double destinationLatitude,
        double destinationLongitude)
    {
        if (access.TrikePoint is not { } trikePoint)
            return double.PositiveInfinity;

        return Math.Max(0,
            ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                destinationLatitude,
                destinationLongitude) +
            ApproximateDistanceMeters(
                destinationLatitude,
                destinationLongitude,
                access.Anchor.Latitude,
                access.Anchor.Longitude) -
            ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                access.Anchor.Latitude,
                access.Anchor.Longitude));
    }

    /// <summary>
    /// Confirms access-state destination-completion edges. Geometric proximity
    /// alone is never sufficient: the board path must pass the destination and
    /// a separate legal route to the destination must be no longer than the
    /// corresponding confirmed path prefix.
    /// </summary>
    private async Task<List<JeepneyTripPlan>>
        ConfirmOriginAccessPathCompletionsAsync(
            IReadOnlyList<AccessPathDestinationCompletionEdge> edges,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken)
    {
        var confirmedAccessTasks = edges.Select(async edge =>
        {
            try
            {
                var confirmed = await ConfirmAccessAsync(
                    edge.AccessToBoard,
                    (originLatitude, originLongitude),
                    edge.AccessToBoard.Anchor,
                    cancellationToken);
                if (confirmed is null || confirmed.Mode != AccessMode.Trike)
                    return null;

                var trikeLeg = BuildAccessLegs(
                        confirmed,
                        (originLatitude, originLongitude),
                        edge.AccessToBoard.Anchor)
                    .LastOrDefault(leg => leg.Mode == AccessMode.Trike);
                return trikeLeg is null
                    ? null
                    : new AccessPathContext(
                        edge.IdentityKey,
                        confirmed,
                        trikeLeg);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogDebug(
                    exception,
                    "Could not confirm origin access state for destination completion");
                return null;
            }
        });
        var contexts = (await Task.WhenAll(confirmedAccessTasks))
            .Where(context => context is not null)
            .Select(context => context!)
            .GroupBy(context => context.Key, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(context => context.ConfirmedAccess.GeneralizedCostPesos)
                .ThenBy(context => context.ConfirmedAccess.TotalTimeSeconds)
                .First())
            .ToList();

        if (contexts.Count == 0)
            return [];

        var boardRoutes = new Dictionary<string, Task<ValhallaRouteResponse>>(
            StringComparer.Ordinal);
        var directRoutes = new Dictionary<string, Task<ValhallaRouteResponse>>(
            StringComparer.Ordinal);

        Task<ValhallaRouteResponse> GetBoardRoute(AccessPathContext context)
        {
            if (!boardRoutes.TryGetValue(context.Key, out var task))
            {
                task = GetRouteAsync(
                    context.TrikeLeg.OriginLatitude,
                    context.TrikeLeg.OriginLongitude,
                    context.TrikeLeg.DestinationLatitude,
                    context.TrikeLeg.DestinationLongitude,
                    TrikeCostingModel,
                    cancellationToken);
                boardRoutes[context.Key] = task;
            }

            return task;
        }

        Task<ValhallaRouteResponse> GetDirectRoute(AccessPathContext context)
        {
            var key = string.Join('|',
                context.TrikeLeg.TrikePointId ?? string.Empty,
                context.TrikeLeg.OriginLatitude.ToString("F7"),
                context.TrikeLeg.OriginLongitude.ToString("F7"),
                destinationLatitude.ToString("F7"),
                destinationLongitude.ToString("F7"));
            if (!directRoutes.TryGetValue(key, out var task))
            {
                task = GetRouteAsync(
                    context.TrikeLeg.OriginLatitude,
                    context.TrikeLeg.OriginLongitude,
                    destinationLatitude,
                    destinationLongitude,
                    TrikeCostingModel,
                    cancellationToken);
                directRoutes[key] = task;
            }

            return task;
        }

        var tasks = contexts.Select(async context =>
        {
            try
            {
                var boardRoute = await GetBoardRoute(context);
                var boardPoints = GetValhallaRoutePoints(boardRoute);
                var closest = ClosestPointOnRoadPath(
                    boardPoints,
                    destinationLatitude,
                    destinationLongitude);

                if (closest is null ||
                    closest.DistanceMeters >
                        _options.JourneyLegContinuityToleranceMeters ||
                    closest.TotalMeters <= 0)
                {
                    return null;
                }

                var directRoute = await GetDirectRoute(context);
                var summary = directRoute.Trip?.Summary;
                if (summary is null ||
                    !double.IsFinite(summary.Length) ||
                    !double.IsFinite(summary.Time) ||
                    summary.Length <= 0 ||
                    summary.Time <= 0)
                {
                    return null;
                }

                var directDistance = summary.Length * 1_000;
                var confirmedPrefixDistance =
                    context.TrikeLeg.DistanceMeters *
                    closest.AlongMeters / closest.TotalMeters;

                // A path merely passing close on the wrong side of a barrier
                // can require a long legal detour to terminate there. Such a
                // route is not a prefix completion and must leave transit in
                // place.
                if (directDistance > confirmedPrefixDistance +
                        _options.JourneyLegContinuityToleranceMeters ||
                    directDistance >= context.TrikeLeg.DistanceMeters)
                {
                    return null;
                }

                var directAccess = BuildTrikePathCompletionAccess(
                    context.ConfirmedAccess,
                    directDistance,
                    summary.Time);
                var legs = BuildAccessLegs(
                    directAccess,
                    (originLatitude, originLongitude),
                    (destinationLatitude, destinationLongitude));
                if (!IsWithinTotalWalkingLimit(legs))
                    return null;

                var plan = CreateTripPlan(
                    legs,
                    directAccess,
                    EmptyAccessSegment());

                _logger.LogDebug(
                    "Confirmed destination on origin access path; toda={Toda} " +
                    "board={BoardLatitude:F6},{BoardLongitude:F6} " +
                    "closest={Closest:F1}m prefix={Prefix:F0}m direct={Direct:F0}m",
                    directAccess.TrikePointId,
                    context.TrikeLeg.DestinationLatitude,
                    context.TrikeLeg.DestinationLongitude,
                    closest.DistanceMeters,
                    confirmedPrefixDistance,
                    directDistance);
                return plan;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Route geometry is an additional proof requirement for this
                // completion. Failure leaves the independent continue/transfer
                // search choices untouched.
                _logger.LogDebug(
                    exception,
                    "Could not verify destination termination on origin access path");
                return null;
            }
        });

        return (await Task.WhenAll(tasks))
            .Where(plan => plan is not null)
            .Select(plan => plan!)
            .GroupBy(GetPlanKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(plan => plan.GeneralizedCostPesos)
                .ThenBy(plan => plan.TotalTimeSeconds)
                .First())
            .ToList();

    }

    private JeepneyAccessSegment BuildTrikePathCompletionAccess(
        JeepneyAccessSegment source,
        double rideDistanceMeters,
        double rideTimeSeconds)
    {
        var fare = ComputeTrikeFarePesos(rideDistanceMeters);
        var totalTime = source.WalkTimeSeconds + rideTimeSeconds;
        return new JeepneyAccessSegment
        {
            Mode = AccessMode.Trike,
            WalkDistanceMeters = source.WalkDistanceMeters,
            WalkTimeSeconds = source.WalkTimeSeconds,
            TrikePointId = source.TrikePointId,
            TrikePointName = source.TrikePointName,
            TrikePointLatitude = source.TrikePointLatitude,
            TrikePointLongitude = source.TrikePointLongitude,
            TrikeRideDistanceMeters = rideDistanceMeters,
            TrikeRideTimeSeconds = rideTimeSeconds,
            TotalTimeSeconds = totalTime,
            TotalFarePesos = fare,
            GeneralizedCostPesos =
                GeneralizedCostFromTimeAndFare(totalTime, fare) +
                source.WalkDistanceMeters / 1_000 *
                WalkingFatiguePesosPerKilometer
        };
    }

    private static List<(double Latitude, double Longitude)>
        GetValhallaRoutePoints(ValhallaRouteResponse response)
    {
        var points = new List<(double Latitude, double Longitude)>();
        foreach (var point in response.Trip?.Legs.SelectMany(leg => leg.Points) ?? [])
        {
            if (point is not { Length: >= 2 } ||
                !double.IsFinite(point[0]) ||
                !double.IsFinite(point[1]))
            {
                continue;
            }

            var candidate = (Latitude: point[1], Longitude: point[0]);
            if (points.Count == 0 || points[^1] != candidate)
                points.Add(candidate);
        }

        return points;
    }

    private static RoadPathClosestPoint? ClosestPointOnRoadPath(
        IReadOnlyList<(double Latitude, double Longitude)> points,
        double latitude,
        double longitude)
    {
        if (points.Count < 2)
            return null;

        var total = 0.0;
        var segmentLengths = new double[points.Count - 1];
        for (var index = 0; index < points.Count - 1; index++)
        {
            segmentLengths[index] = ApproximateDistanceMeters(
                points[index].Latitude,
                points[index].Longitude,
                points[index + 1].Latitude,
                points[index + 1].Longitude);
            total += segmentLengths[index];
        }

        var bestDistance = double.PositiveInfinity;
        var bestAlong = 0.0;
        var traversed = 0.0;
        var latitudeRadians = latitude * Math.PI / 180;
        var longitudeScale = Math.Cos(latitudeRadians);

        for (var index = 0; index < points.Count - 1; index++)
        {
            var from = points[index];
            var to = points[index + 1];
            var ax = (from.Longitude - longitude) * longitudeScale;
            var ay = from.Latitude - latitude;
            var bx = (to.Longitude - longitude) * longitudeScale;
            var by = to.Latitude - latitude;
            var dx = bx - ax;
            var dy = by - ay;
            var denominator = dx * dx + dy * dy;
            var fraction = denominator <= 0
                ? 0
                : Math.Clamp(-(ax * dx + ay * dy) / denominator, 0, 1);
            var projectedLatitude = from.Latitude +
                (to.Latitude - from.Latitude) * fraction;
            var projectedLongitude = from.Longitude +
                (to.Longitude - from.Longitude) * fraction;
            var distance = ApproximateDistanceMeters(
                latitude,
                longitude,
                projectedLatitude,
                projectedLongitude);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestAlong = traversed + segmentLengths[index] * fraction;
            }

            traversed += segmentLengths[index];
        }

        return new RoadPathClosestPoint(bestDistance, bestAlong, total);
    }

    private sealed record AccessPathContext(
        string Key,
        JeepneyAccessSegment ConfirmedAccess,
        JeepneyTripLeg TrikeLeg);

    private sealed record AccessPathDestinationCompletionEdge(
        AccessCandidate AccessToBoard,
        string IdentityKey) : DestinationCompletionEdge;

    private sealed record RoadPathClosestPoint(
        double DistanceMeters,
        double AlongMeters,
        double TotalMeters);
}
