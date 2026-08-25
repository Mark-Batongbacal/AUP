using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private const double PhysicalBoardingRegionToleranceMeters = 120;
    private const double RouteOccurrenceIdentityToleranceMeters = 1;

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

            var boardDiscovery = await DiscoverBoardAccessOptionsAsync(
                route.RouteId,
                _routeSamples[route.RouteId],
                originLatitude,
                originLongitude,
                cancellationToken);
            candidates.AddRange(FindBestConnections(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                boardDiscovery));
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
        double destinationLongitude,
        BoardAccessDiscovery boardDiscovery,
        double? walkAccessDistanceLimitMeters = null)
    {
        var walkAccessLimit = walkAccessDistanceLimitMeters ??
            GetWalkAccessDistanceLimit(null);
        var samples = _routeSamples[route.RouteId];

        if (samples.Count < 2)
            return [];

        var boardAccessOptions = boardDiscovery.Projected;
        var alightAccessOptions = ComputeAlightAccessOptions(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude);

        var boardCandidates = boardAccessOptions
            .Select(candidate => ConstrainTransitAccess(
                candidate,
                walkAccessLimit))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        // A search sample identifies a viable route occurrence, while the
        // local projection refines the physical board within that region.
        // On a tight retrace the previous-to-next search window can overlap a
        // later pass and project completely out of the sample's physical
        // region. Keep the authoritative on-route sample anchor in that case,
        // or when its confirmed pedestrian access is better even within the
        // same coarse physical region, so the overlapping geometric
        // projection cannot make that decision.
        var searchAnchorBoardOptions = boardDiscovery.SearchAnchors;
        for (var index = 0; index < searchAnchorBoardOptions.Length; index++)
        {
            var samePhysicalRegion = ApproximateDistanceMeters(
                    boardAccessOptions[index].Anchor.Latitude,
                    boardAccessOptions[index].Anchor.Longitude,
                    searchAnchorBoardOptions[index].Anchor.Latitude,
                    searchAnchorBoardOptions[index].Anchor.Longitude) <=
                PhysicalBoardingRegionToleranceMeters;
            if (samePhysicalRegion &&
                BestNetworkWalkAccessCost(searchAnchorBoardOptions[index]) >=
                BestNetworkWalkAccessCost(boardAccessOptions[index]))
            {
                continue;
            }

            AddUniqueAccessCandidate(
                boardCandidates,
                ConstrainTransitAccess(
                    searchAnchorBoardOptions[index],
                    walkAccessLimit));
        }

        var exactBoard = boardDiscovery.Exact;
        AddUniqueAccessCandidate(boardCandidates, exactBoard);

        var alightCandidates = alightAccessOptions
            .Select(candidate => ConstrainTransitAccess(
                candidate,
                walkAccessLimit))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var exactAlight = BuildExactFullRouteAlightAccess(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude,
            walkAccessLimit);
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

        // 1) Best confirmed pedestrian-network access within the geometrically
        // discovered corridor. Straight-line distance only breaks ties or is
        // used when the discovery matrix was temporarily unavailable.
        Add(distinct
            .OrderBy(BestNetworkWalkAccessCost)
            .ThenBy(candidate =>
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
            var physicalRepresentatives = SelectPhysicalBoardingRepresentatives(
                distinct);
            var bucketRepresentatives = physicalRepresentatives
                .Where(candidate => !selected.Any(existing =>
                    IsSamePhysicalBoardingRegion(existing, candidate)))
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

            // Once every available physical region has had a chance to
            // contribute, use remaining capacity for distinct route
            // occurrences, including retraced occurrences at a coordinate
            // already represented above. Occurrence identity remains intact,
            // but it cannot crowd a not-yet-represented physical region out
            // of the diversity pass.
            if (selected.Count < MaxBoardingVariantsPerRoute)
            {
                foreach (var candidate in distinct
                             .Where(candidate => !selected.Any(existing =>
                                 PositionKey(existing) == PositionKey(candidate)))
                             .OrderByDescending(candidate =>
                                 selected.Min(existing => Math.Abs(
                                     GetBoardProgressMeters(candidate) -
                                     GetBoardProgressMeters(existing))))
                             .ThenBy(candidate => candidate.TotalGeneralizedCostPesos))
                {
                    Add(candidate);
                    if (selected.Count >= MaxBoardingVariantsPerRoute)
                        break;
                }
            }
        }

        return selected;
    }

    private static bool IsSamePhysicalBoardingRegion(
        RouteConnectionCandidate left,
        RouteConnectionCandidate right) =>
        ApproximateDistanceMeters(
            left.BoardAccess.Anchor.Latitude,
            left.BoardAccess.Anchor.Longitude,
            right.BoardAccess.Anchor.Latitude,
            right.BoardAccess.Anchor.Longitude) <=
        PhysicalBoardingRegionToleranceMeters;

    internal List<RouteConnectionCandidate>
        SelectPhysicalBoardingRepresentatives(
            IReadOnlyList<RouteConnectionCandidate> candidates)
    {
        var regions = new List<List<RouteConnectionCandidate>>();

        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                     .ThenBy(GetBoardProgressMeters))
        {
            var region = regions.FirstOrDefault(existing =>
                IsSamePhysicalBoardingRegion(existing[0], candidate));

            if (region is null)
                regions.Add([candidate]);
            else
                region.Add(candidate);
        }

        return regions
            .Select(region => region[0])
            .ToList();
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
            right.DistanceFromRouteStartMeters) <=
        RouteOccurrenceIdentityToleranceMeters;

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

    private static double BestNetworkWalkAccessCost(
        RouteConnectionCandidate candidate) =>
        BestNetworkWalkAccessCost(candidate.BoardAccess);

    private static double BestNetworkWalkAccessCost(
        AccessCandidate candidate) =>
        candidate.AllAlternatives
            .Where(alternative =>
                alternative.Mode == AccessMode.Walk &&
                alternative.IsNetworkWalkConfirmed)
            .Select(alternative => alternative.GeneralizedCostPesos)
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();

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
        double originLongitude,
        double? walkAccessDistanceLimitMeters = null)
    {
        var walkAccessLimit = walkAccessDistanceLimitMeters ??
            GetWalkAccessDistanceLimit(null);
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
        if (walkDistance <= walkAccessLimit)
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

        return ConstrainTransitAccess(
            WithAlternatives(alternatives),
            walkAccessLimit);
    }

    private AccessCandidate? BuildExactFullRouteAlightAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double destinationLatitude,
        double destinationLongitude,
        double? walkAccessDistanceLimitMeters = null)
    {
        var walkAccessLimit = walkAccessDistanceLimitMeters ??
            GetWalkAccessDistanceLimit(null);
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
        if (walkDistance <= walkAccessLimit)
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

        return ConstrainTransitAccess(
            WithAlternatives(alternatives),
            walkAccessLimit);
    }

    private AccessCandidate? ConstrainTransitAccess(
        AccessCandidate candidate,
        double? walkAccessDistanceLimitMeters = null)
    {
        var walkAccessLimit = walkAccessDistanceLimitMeters ??
            GetWalkAccessDistanceLimit(null);
        var alternatives = candidate.AllAlternatives
            .Where(alternative =>
                alternative.Mode != AccessMode.Walk ||
                alternative.WalkDistanceMeters <= walkAccessLimit)
            .OrderBy(alternative => alternative.GeneralizedCostPesos)
            .ThenBy(alternative => alternative.Mode)
            .ToList();

        if (alternatives.Count == 0)
            return null;

        return alternatives[0] with { Alternatives = alternatives };
    }

    /// <summary>
    /// Applies <see cref="ConstrainTransitAccess"/> across a whole route's
    /// per-sample access options. Samples left with no usable option become
    /// null, which the bounded boarding and destination-access representations
    /// already treat as unavailable.
    /// </summary>
    private AccessCandidate?[] ConstrainTransitAccessOptions(
        AccessCandidate[] options,
        double? walkAccessDistanceLimitMeters = null) =>
        options.Select(candidate => ConstrainTransitAccess(
            candidate,
            walkAccessDistanceLimitMeters)).ToArray();

    private bool IsTransitAccessWithinLimit(
        JeepneyAccessSegment access,
        double? walkAccessDistanceLimitMeters = null) =>
        access.Mode != AccessMode.Walk ||
        access.WalkDistanceMeters <= (walkAccessDistanceLimitMeters ??
            GetWalkAccessDistanceLimit(null));

    // -------------------------------------------------------------------
    // Full journey planning
    // -------------------------------------------------------------------

}
