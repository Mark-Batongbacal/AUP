using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    public async Task<List<JeepneyTripOption>> FindConnectingRoutesAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var candidates = new List<RouteConnectionCandidate>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeSamples.ContainsKey(route.RouteId))
                continue;

            candidates.AddRange(FindBestConnections(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude));
        }

        var ranked = candidates
            .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
            .Take(MaxCandidatesToConfirm)
            .ToList();

        var confirmTasks = ranked.Select(async candidate =>
        {
            try
            {
                var boardTask = ConfirmAccessAsync(
                    candidate.BoardAccess,
                    (originLatitude, originLongitude),
                    candidate.BoardAccess.Anchor,
                    cancellationToken);

                var alightTask = ConfirmAccessAsync(
                    candidate.AlightAccess,
                    candidate.AlightAccess.Anchor,
                    (destinationLatitude, destinationLongitude),
                    cancellationToken);

                await Task.WhenAll(boardTask, alightTask);

                var board = await boardTask;
                var alight = await alightTask;

                if (board is null || alight is null ||
                    !IsTransitAccessWithinLimit(board) ||
                    !IsTransitAccessWithinLimit(alight))
                {
                    return null;
                }

                var jeepneyTime = GetJeepneyLegTimeSeconds(
                    candidate.RouteId,
                    candidate.BoardIndex,
                    candidate.AlightIndex,
                    candidate.BoardAccess.FullRouteAnchor,
                    candidate.AlightAccess.FullRouteAnchor);

                return new JeepneyTripOption
                {
                    RouteId = candidate.RouteId,
                    RouteName = candidate.RouteName,
                    BoardLatitude = candidate.BoardAccess.Anchor.Latitude,
                    BoardLongitude = candidate.BoardAccess.Anchor.Longitude,
                    BoardAccess = board,
                    AlightLatitude = candidate.AlightAccess.Anchor.Latitude,
                    AlightLongitude = candidate.AlightAccess.Anchor.Longitude,
                    AlightAccess = alight,
                    TotalTimeSeconds = board.TotalTimeSeconds + jeepneyTime + alight.TotalTimeSeconds,
                    TotalFarePesos = board.TotalFarePesos + alight.TotalFarePesos + JeepneyBaseFarePesos,
                    GeneralizedCostPesos =
                        board.GeneralizedCostPesos +
                        GeneralizedCostFromTimeAndFare(jeepneyTime, JeepneyBaseFarePesos) +
                        alight.GeneralizedCostPesos
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to confirm trip option for route {RouteId}",
                    candidate.RouteId);
                return null;
            }
        });

        var results = await Task.WhenAll(confirmTasks);

        return results
            .Where(option => option is not null)
            .Select(option => option!)
            .GroupBy(option => option.RouteId, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(option => option.GeneralizedCostPesos)
                .ThenBy(option => option.TotalTimeSeconds)
                .ThenBy(option => option.BoardAccess.WalkDistanceMeters)
                .First())
            .OrderBy(option => option.GeneralizedCostPesos)
            .ThenBy(option => option.TotalTimeSeconds)
            .ThenBy(option => option.TotalFarePesos)
            .ThenBy(option => option.RouteId)
            .Take(MaxTripOptions)
            .ToList();
    }

    private RouteConnectionCandidate? FindBestConnection(
        StaticJeepneyRoute route,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude) =>
        FindBestConnections(
            route,
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude)
        .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
        .ThenBy(candidate => candidate.BoardAccess.TotalTimeSeconds)
        .FirstOrDefault();

    /// <summary>
    /// Keeps a small, deliberately diverse set of boarding variants for one
    /// jeepney route. Exact projections on the full route are injected for both
    /// boarding and alighting, while sampled anchors remain useful for broader
    /// candidate search. Exact projections can use walk or tricycle access.
    /// </summary>
    private List<RouteConnectionCandidate> FindBestConnections(
        StaticJeepneyRoute route,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        var samples = _routeSamples[route.RouteId];

        if (samples.Count < 2)
            return [];

        var boardAccessOptions = ComputeBoardAccessOptions(
            route.RouteId,
            samples,
            originLatitude,
            originLongitude);
        var alightAccessOptions = ComputeAlightAccessOptions(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude);

        var boardCandidates = boardAccessOptions
            .Select(ConstrainTransitAccess)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var exactBoard = BuildExactFullRouteBoardAccess(
            route.RouteId,
            samples,
            originLatitude,
            originLongitude);
        AddUniqueAccessCandidate(boardCandidates, exactBoard);

        var alightCandidates = alightAccessOptions
            .Select(ConstrainTransitAccess)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var exactAlight = BuildExactFullRouteAlightAccess(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude);
        AddUniqueAccessCandidate(alightCandidates, exactAlight);

        var all = new List<RouteConnectionCandidate>();

        foreach (var alightAccess in alightCandidates)
        {
            var alightIndex = alightAccess.RouteSampleIndex ??
                GetNearestSampleIndex(samples, alightAccess.Anchor);
            var alightAnchor = alightAccess.FullRouteAnchor ??
                GetRouteAnchor(route.RouteId, alightIndex, alightAccess.Anchor);

            foreach (var boardAccess in boardCandidates)
            {
                var boardIndex = boardAccess.RouteSampleIndex ??
                    GetNearestSampleIndex(samples, boardAccess.Anchor);
                var boardAnchor = boardAccess.FullRouteAnchor ??
                    GetRouteAnchor(route.RouteId, boardIndex, boardAccess.Anchor);

                // Direction is based on authoritative full-route progress, not
                // merely sample indices. This remains correct on bends/loops.
                var rideDistance = RouteDistanceBetweenAnchors(boardAnchor, alightAnchor);
                if (rideDistance <= 0)
                    continue;

                var jeepneyTime = JeepneyBoardingWaitTimeSeconds +
                    rideDistance / JeepneySpeedMetersPerSecond;

                var total =
                    boardAccess.GeneralizedCostPesos +
                    alightAccess.GeneralizedCostPesos +
                    GeneralizedCostFromTimeAndFare(
                        jeepneyTime,
                        JeepneyBaseFarePesos);

                all.Add(new RouteConnectionCandidate(
                    route.RouteId,
                    route.RouteName,
                    boardAccess,
                    alightAccess,
                    boardIndex,
                    alightIndex,
                    total));
            }
        }

        if (all.Count == 0)
            return [];

        // Identity is the physical board/alight position PLUS where each sits
        // along the route, NOT the sample index that produced it. Many
        // neighbouring samples clamp onto the same projected point, and
        // keying by index would let one physical boarding position consume
        // the entire per-route quota as "different" candidates -- those
        // duplicates share a projection, so they share progress too and still
        // collapse here.
        //
        // Progress has to be part of the key because a route may legitimately
        // traverse the same road twice (out and back, or a loop). Those are
        // the same coordinate but genuinely different boarding opportunities:
        // collapsing them to whichever is physically nearest can strand the
        // passenger on the outbound pass when the return pass is the one
        // heading towards their destination.
        string PositionKey(RouteConnectionCandidate candidate) => string.Join(':',
            Math.Round(candidate.BoardAccess.Anchor.Latitude, 6),
            Math.Round(candidate.BoardAccess.Anchor.Longitude, 6),
            Math.Round(candidate.AlightAccess.Anchor.Latitude, 6),
            Math.Round(candidate.AlightAccess.Anchor.Longitude, 6),
            Math.Round(GetBoardProgressMeters(candidate), 1),
            Math.Round(GetAlightProgressMeters(candidate), 1));

        var distinct = all
            .GroupBy(PositionKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                .First())
            .ToList();

        var selected = new List<RouteConnectionCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(RouteConnectionCandidate candidate)
        {
            if (selected.Count >= MaxBoardingVariantsPerRoute)
                return;

            if (seen.Add(PositionKey(candidate)))
                selected.Add(candidate);
        }

        // 0) Earliest full-route board progress. Cost/time/fare heuristics
        // below are computed from cheap, unconfirmed straight-line access
        // estimates and can rank a downstream board above a perfectly
        // reasonable early one. Guaranteeing the earliest-progress board a
        // slot means it always reaches Valhalla confirmation, so later
        // feeder-shadowing pruning has a real early baseline to compare
        // against instead of comparing only cost-optimistic downstream
        // candidates against each other.
        Add(distinct
            .OrderBy(GetBoardProgressMeters)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 1) Nearest directionally-valid boarding opportunity on full geometry.
        Add(distinct
            .OrderBy(candidate =>
                StraightLineBoardAccessMeters(
                    candidate,
                    originLatitude,
                    originLongitude))
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 2) Best provisional balanced cost.
        Add(distinct
            .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
            .ThenBy(candidate =>
                StraightLineBoardAccessMeters(
                    candidate,
                    originLatitude,
                    originLongitude))
            .First());

        // 3) Fastest provisional complete journey.
        Add(distinct
            .OrderBy(EstimateConnectionTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 4) Cheapest provisional complete journey.
        Add(distinct
            .OrderBy(EstimateConnectionFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 5) Fill the remaining quota with boards spread along the route
        // rather than more of the same cluster. All the objectives above
        // rank on straight-line access, so they crowd around whichever part
        // of the corridor happens to be geometrically closest -- which is
        // exactly the part real road access may turn out to be poor at.
        // Taking the cheapest candidate from each distinct progress bucket
        // keeps a genuinely different boarding region available for
        // Valhalla to confirm.
        // 5) Fill the remaining quota with boards spread along the route
        // rather than more of the same cluster. All the objectives above
        // rank on straight-line access, so they crowd around whichever part
        // of the corridor happens to be geometrically closest -- which is
        // exactly the part real road access may turn out to be poor at.
        //
        // The buckets are sampled ACROSS the whole progress range rather
        // than taken lowest-first: taking the lowest buckets would just
        // rebuild a cluster at the route start and would never surface the
        // only reachable board when a near obstacle blocks the early
        // corridor.
        if (selected.Count < MaxBoardingVariantsPerRoute)
        {
            var bucketSize = Math.Max(1, _options.BoardingDiversityBucketMeters);
            var bucketRepresentatives = distinct
                .GroupBy(candidate =>
                    (long)Math.Floor(GetBoardProgressMeters(candidate) / bucketSize))
                .OrderBy(group => group.Key)
                .Select(group => group
                    .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                    .First())
                .ToList();

            foreach (var candidate in SpreadEvenly(
                         bucketRepresentatives,
                         MaxBoardingVariantsPerRoute - selected.Count))
            {
                Add(candidate);
                if (selected.Count >= MaxBoardingVariantsPerRoute)
                    break;
            }
        }

        return selected;
    }

    /// <summary>
    /// Samples up to <paramref name="count"/> items spread evenly across the
    /// ordered source, always including both ends. Used so boarding-variant
    /// diversity covers the whole route instead of clustering at one end.
    /// </summary>
    private static IEnumerable<T> SpreadEvenly<T>(IReadOnlyList<T> source, int count)
    {
        if (source.Count == 0 || count <= 0)
            yield break;

        if (count >= source.Count)
        {
            foreach (var item in source)
                yield return item;
            yield break;
        }

        if (count == 1)
        {
            yield return source[0];
            yield break;
        }

        var emitted = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var index = (int)Math.Round(
                (double)i * (source.Count - 1) / (count - 1));

            if (emitted.Add(index))
                yield return source[index];
        }
    }

    /// <summary>
    /// Adds an access candidate unless the list already holds the same
    /// boarding/alighting opportunity. Two candidates are the same only when
    /// they sit at the same physical point AND at the same place along the
    /// route: a route that traverses one road twice offers two genuinely
    /// different opportunities at identical coordinates, and discarding the
    /// second would hide whichever pass actually heads for the destination.
    /// </summary>
    private static void AddUniqueAccessCandidate(
        List<AccessCandidate> candidates,
        AccessCandidate? candidate)
    {
        if (candidate is null)
            return;

        var duplicate = candidates.Any(existing =>
            ApproximateDistanceMeters(
                existing.Anchor.Latitude,
                existing.Anchor.Longitude,
                candidate.Anchor.Latitude,
                candidate.Anchor.Longitude) <= 1.0 &&
            IsSameRouteOccurrence(existing.FullRouteAnchor, candidate.FullRouteAnchor));

        if (!duplicate)
            candidates.Add(candidate);
    }

    /// <summary>
    /// Occurrences match when both anchors report effectively the same
    /// distance travelled from the route start. Unknown progress on either
    /// side falls back to treating them as the same, preserving the previous
    /// coordinate-only behaviour.
    /// </summary>
    private static bool IsSameRouteOccurrence(
        RouteAnchor? left,
        RouteAnchor? right) =>
        left is null || right is null ||
        Math.Abs(
            left.DistanceFromRouteStartMeters -
            right.DistanceFromRouteStartMeters) <= 1.0;

    private double EstimateConnectionTimeSeconds(RouteConnectionCandidate candidate) =>
        candidate.BoardAccess.TotalTimeSeconds +
        GetJeepneyLegTimeSeconds(
            candidate.RouteId,
            candidate.BoardIndex,
            candidate.AlightIndex,
            candidate.BoardAccess.FullRouteAnchor,
            candidate.AlightAccess.FullRouteAnchor) +
        candidate.AlightAccess.TotalTimeSeconds;

    private double EstimateConnectionFarePesos(RouteConnectionCandidate candidate) =>
        candidate.BoardAccess.FarePesos +
        JeepneyBaseFarePesos +
        candidate.AlightAccess.FarePesos;

    private static double StraightLineBoardAccessMeters(
        RouteConnectionCandidate candidate,
        double originLatitude,
        double originLongitude) =>
        ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            candidate.BoardAccess.Anchor.Latitude,
            candidate.BoardAccess.Anchor.Longitude);

    private double GetBoardProgressMeters(RouteConnectionCandidate candidate) =>
        (candidate.BoardAccess.FullRouteAnchor ??
            GetRouteAnchor(
                candidate.RouteId,
                candidate.BoardIndex,
                candidate.BoardAccess.Anchor))
        .DistanceFromRouteStartMeters;

    private double GetAlightProgressMeters(RouteConnectionCandidate candidate) =>
        (candidate.AlightAccess.FullRouteAnchor ??
            GetRouteAnchor(
                candidate.RouteId,
                candidate.AlightIndex,
                candidate.AlightAccess.Anchor))
        .DistanceFromRouteStartMeters;

    private AccessCandidate? BuildExactFullRouteBoardAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double originLatitude,
        double originLongitude)
    {
        var anchor = ProjectOntoFullRoute(
            routeId,
            (originLatitude, originLongitude),
            0);
        var point = (anchor.Latitude, anchor.Longitude);
        var sampleIndex = GetNearestSampleIndex(samples, point);
        var alternatives = new List<AccessCandidate>();

        var walkDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            anchor.Latitude,
            anchor.Longitude);
        if (walkDistance <= MaxWalkAccessDistanceMeters)
        {
            alternatives.Add(WalkAccess(
                point,
                walkDistance,
                sampleIndex,
                anchor));
        }

        foreach (var trikePoint in FindNearbyTrikePoints(
                     originLatitude,
                     originLongitude))
        {
            var walkToTrikeMeters = ApproximateDistanceMeters(
                originLatitude,
                originLongitude,
                trikePoint.Latitude,
                trikePoint.Longitude);
            var rideDistanceMeters = ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                anchor.Latitude,
                anchor.Longitude);

            alternatives.Add(TrikeAccess(
                point,
                trikePoint,
                walkToTrikeMeters,
                rideDistanceMeters,
                sampleIndex,
                anchor));
        }

        if (alternatives.Count == 0)
            return null;

        return ConstrainTransitAccess(WithAlternatives(alternatives));
    }

    private AccessCandidate? BuildExactFullRouteAlightAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double destinationLatitude,
        double destinationLongitude)
    {
        var anchor = ProjectOntoFullRoute(
            routeId,
            (destinationLatitude, destinationLongitude),
            0);
        var point = (anchor.Latitude, anchor.Longitude);
        var sampleIndex = GetNearestSampleIndex(samples, point);
        var alternatives = new List<AccessCandidate>();

        var walkDistance = ApproximateDistanceMeters(
            anchor.Latitude,
            anchor.Longitude,
            destinationLatitude,
            destinationLongitude);
        if (walkDistance <= MaxWalkAccessDistanceMeters)
        {
            alternatives.Add(WalkAccess(
                point,
                walkDistance,
                sampleIndex,
                anchor));
        }

        foreach (var trikePoint in FindNearbyTrikePoints(
                     anchor.Latitude,
                     anchor.Longitude))
        {
            var walkToTrikeMeters = ApproximateDistanceMeters(
                anchor.Latitude,
                anchor.Longitude,
                trikePoint.Latitude,
                trikePoint.Longitude);
            var rideDistanceMeters = ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                destinationLatitude,
                destinationLongitude);

            alternatives.Add(TrikeAccess(
                point,
                trikePoint,
                walkToTrikeMeters,
                rideDistanceMeters,
                sampleIndex,
                anchor));
        }

        if (alternatives.Count == 0)
            return null;

        return ConstrainTransitAccess(WithAlternatives(alternatives));
    }

    private AccessCandidate? ConstrainTransitAccess(AccessCandidate candidate)
    {
        var alternatives = candidate.AllAlternatives
            .Where(alternative =>
                alternative.Mode != AccessMode.Walk ||
                alternative.WalkDistanceMeters <= MaxWalkAccessDistanceMeters)
            .OrderBy(alternative => alternative.GeneralizedCostPesos)
            .ThenBy(alternative => alternative.Mode)
            .ToList();

        if (alternatives.Count == 0)
            return null;

        return alternatives[0] with { Alternatives = alternatives };
    }

    private bool IsTransitAccessWithinLimit(JeepneyAccessSegment access) =>
        access.Mode != AccessMode.Walk ||
        access.WalkDistanceMeters <= MaxWalkAccessDistanceMeters;

    // -------------------------------------------------------------------
    // Full journey planning
    // -------------------------------------------------------------------

}