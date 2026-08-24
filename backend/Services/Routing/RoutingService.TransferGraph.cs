namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Enumerates transfer journeys level by level: every interchange a route
    /// offers contributes its one-transfer journey before any two-transfer
    /// journey is built, and every depth gets its own share of the budget.
    ///
    /// The previous implementation walked each starting route's interchange
    /// list depth-first and stopped the whole route once a per-route quota was
    /// spent. A single edge's subtree could therefore consume the entire
    /// budget, leaving every later interchange on that route unexplored -- and
    /// which edge comes first is an accident of route ordering in the
    /// database, not a statement about usefulness. A route with fifty-five
    /// interchanges would explore one of them.
    ///
    /// Level-synchronous expansion removes that: fairness across interchange
    /// regions and fairness across transfer depth both fall out of the
    /// traversal order instead of depending on quota accounting.
    /// </summary>
    private IEnumerable<JourneyCandidate> FindTransferCandidates(
        IReadOnlyDictionary<string, (double[] Cost, AccessCandidate?[] Access)> boardPrefixes,
        IReadOnlyDictionary<string, (double[] Cost, AccessCandidate?[] Access)> alightSuffixes,
        CancellationToken cancellationToken)
    {
        if (MaxTransfers == 0) yield break;

        var routeNames = _routes.ToDictionary(route => route.RouteId, route => route.RouteName);
        var emitted = 0;
        // A safety ceiling only. Per-route and per-level bounds below are what
        // actually shape the pool; this exists so a pathological network
        // cannot run away.
        var globalLimit = MaxCandidatesToConfirm *
            Math.Max(1, MaxTransfers + 1) *
            Math.Max(1, _routes.Count);
        var dominance = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var startRoute in _routes)
        {
            if (!_interchangesByRoute.TryGetValue(startRoute.RouteId, out var firstEdges))
                continue;

            var frontier = BuildInitialFrontier(startRoute.RouteId, firstEdges);
            if (frontier.Count == 0)
                continue;

            // Every interchange region of this route gets a place at the first
            // level, and each further level gets the same allowance, so a
            // deeper journey is never crowded out by shallower ones.
            var perLevelLimit = Math.Max(MinTransferCandidatesPerRoute, frontier.Count);

            for (var depth = 1; depth <= MaxTransfers && frontier.Count > 0; depth++)
            {
                var emittedAtDepth = 0;

                foreach (var state in frontier)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var candidate = BuildDestinationCandidate(state);
                    if (candidate is null)
                        continue;

                    yield return candidate;
                    emitted++;
                    emittedAtDepth++;

                    if (emitted >= globalLimit)
                    {
                        _logger.LogDebug(
                            "Transfer candidate generation reached global pool limit {GlobalLimit}",
                            globalLimit);
                        yield break;
                    }

                    if (emittedAtDepth >= perLevelLimit)
                        break;
                }

                _logger.LogDebug(
                    "Transfer candidates for {RouteId} at depth {Depth}: {Count} from {Frontier} states",
                    startRoute.RouteId,
                    depth,
                    emittedAtDepth,
                    frontier.Count);

                if (depth >= MaxTransfers)
                    break;

                frontier = BuildNextFrontier(frontier, perLevelLimit);
            }
        }

        yield break;

        List<TransferSearchState> BuildInitialFrontier(
            string startRouteId,
            List<RouteInterchange> firstEdges)
        {
            var states = new List<TransferSearchState>();

            foreach (var first in firstEdges)
            {
                var isSelfInterchange = string.Equals(
                    startRouteId,
                    first.OtherRouteId,
                    StringComparison.Ordinal);

                if (first.OwnIndex <= 0 ||
                    first.DistanceMeters > MaxTransferWalkMeters ||
                    (isSelfInterchange && !IsForwardSelfInterchange(first)))
                {
                    continue;
                }

                var board = boardPrefixes[startRouteId].Access[first.OwnIndex];
                if (board is null)
                    continue;


                states.Add(new TransferSearchState(
                    first.OtherRouteId,
                    first.OtherIndex,
                    board,
                    [new TransferSearchStep(startRouteId, first)],
                    new HashSet<string>(StringComparer.Ordinal)
                        { startRouteId, first.OtherRouteId },
                    new HashSet<RouteProgressState>
                        { new(first.OtherRouteId, first.OtherIndex) },
                    first.DistanceMeters,
                    board.GeneralizedCostPesos + GeneralizedCostFromWalking(
                        first.DistanceMeters / WalkingSpeedMetersPerSecond,
                        first.DistanceMeters)));
            }

            return states;
        }

        /// Children are taken round-robin across the whole frontier, so one
        /// busy interchange cannot fill the next level on its own.
        List<TransferSearchState> BuildNextFrontier(
            List<TransferSearchState> current,
            int maxStates)
        {
            var enumerators = current
                .Select(state => NextStates(state).GetEnumerator())
                .ToList();

            try
            {
                var next = new List<TransferSearchState>();

                while (next.Count < maxStates)
                {
                    var addedAny = false;

                    foreach (var enumerator in enumerators)
                    {
                        if (!enumerator.MoveNext())
                            continue;

                        next.Add(enumerator.Current);
                        addedAny = true;

                        if (next.Count >= maxStates)
                            break;
                    }

                    if (!addedAny)
                        break;
                }

                return next;
            }
            finally
            {
                foreach (var enumerator in enumerators)
                    enumerator.Dispose();
            }
        }

        IEnumerable<TransferSearchState> NextStates(TransferSearchState state)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_interchangesByRoute.TryGetValue(state.CurrentRouteId, out var edges))
                yield break;

            foreach (var edge in edges)
            {
                var isSelfInterchange = string.Equals(
                    edge.OtherRouteId,
                    state.CurrentRouteId,
                    StringComparison.Ordinal);
                var nextProgressState = new RouteProgressState(
                    edge.OtherRouteId, edge.OtherIndex);

                if (edge.OwnIndex <= state.EntryIndex ||
                    edge.DistanceMeters > MaxTransferWalkMeters ||
                    (!isSelfInterchange && state.VisitedRoutes.Contains(edge.OtherRouteId)) ||
                    (isSelfInterchange && !IsForwardSelfInterchange(edge)) ||
                    state.VisitedProgressStates.Contains(nextProgressState) ||
                    edge.OtherIndex >= _routeSamples[edge.OtherRouteId].Count - 1)
                {
                    continue;
                }

                var totalWalking = state.TransferWalkingMeters + edge.DistanceMeters;
                if (totalWalking > MaxTotalWalkingMetersPerJourney)
                    continue;

                var accumulatedCost = state.AccumulatedCost + GeneralizedCostFromWalking(
                    edge.DistanceMeters / WalkingSpeedMetersPerSecond,
                    edge.DistanceMeters);

                var key = $"{edge.OtherRouteId}:{edge.OtherIndex}:{state.Steps.Count + 1}:" +
                    string.Join(',', state.VisitedRoutes.OrderBy(value => value));
                if (dominance.TryGetValue(key, out var best) && best <= accumulatedCost)
                    continue;
                dominance[key] = accumulatedCost;

                yield return new TransferSearchState(
                    edge.OtherRouteId,
                    edge.OtherIndex,
                    state.BoardAccess,
                    [.. state.Steps, new TransferSearchStep(state.CurrentRouteId, edge)],
                    new HashSet<string>(state.VisitedRoutes, StringComparer.Ordinal)
                        { edge.OtherRouteId },
                    new HashSet<RouteProgressState>(state.VisitedProgressStates)
                        { nextProgressState },
                    totalWalking,
                    accumulatedCost);
            }
        }

        JourneyCandidate? BuildDestinationCandidate(TransferSearchState state)
        {
            if (!alightSuffixes.TryGetValue(state.CurrentRouteId, out var suffix)) return null;
            var alight = suffix.Access[state.EntryIndex];
            if (alight is null) return null;
            var alightIndex = alight.RouteSampleIndex ?? GetNearestSampleIndex(
                _routeSamples[state.CurrentRouteId], alight.Anchor);
            if (alightIndex <= state.EntryIndex) return null;
            var journeyLegs = new List<JourneyLegCandidate>();
            for (var index = 0; index < state.Steps.Count; index++)
            {
                var step = state.Steps[index];
                var samples = _routeSamples[step.FromRouteId];
                var boardPoint = index == 0
                    ? state.BoardAccess.Anchor
                    : _routeSamples[state.Steps[index - 1].Edge.OtherRouteId]
                        [state.Steps[index - 1].Edge.OtherIndex];
                var boardIndex = index == 0
                    ? state.BoardAccess.RouteSampleIndex ?? GetNearestSampleIndex(samples, boardPoint)
                    : state.Steps[index - 1].Edge.OtherIndex;
                var alightPoint = samples[step.Edge.OwnIndex];
                journeyLegs.Add(new(step.FromRouteId, routeNames[step.FromRouteId],
                    boardPoint, alightPoint, boardIndex, step.Edge.OwnIndex,
                    index == 0 ? state.BoardAccess.FullRouteAnchor : GetRouteAnchor(step.FromRouteId, boardIndex, boardPoint),
                    GetRouteAnchor(step.FromRouteId, step.Edge.OwnIndex, alightPoint)));
            }
            var lastEntryPoint = _routeSamples[state.CurrentRouteId][state.EntryIndex];
            journeyLegs.Add(new(state.CurrentRouteId, routeNames[state.CurrentRouteId],
                lastEntryPoint, alight.Anchor, state.EntryIndex, alightIndex,
                GetRouteAnchor(state.CurrentRouteId, state.EntryIndex, lastEntryPoint),
                alight.FullRouteAnchor));
            if (journeyLegs.Any(leg => RouteDistanceBetweenAnchors(
                    leg.BoardFullRouteAnchor!, leg.AlightFullRouteAnchor!) <= 0))
            {
                return null;
            }
            var walks = state.Steps.Select(step => new WalkSegmentCandidate(
                _routeSamples[step.FromRouteId][step.Edge.OwnIndex],
                _routeSamples[step.Edge.OtherRouteId][step.Edge.OtherIndex],
                step.Edge.DistanceMeters)).ToList();
            return new JourneyCandidate(journeyLegs, state.BoardAccess, alight, walks,
                state.AccumulatedCost + alight.GeneralizedCostPesos +
                GeneralizedCostFromTimeAndFare(
                    EstimateJeepneyTravelTimeSeconds(journeyLegs),
                    journeyLegs.Count * JeepneyBaseFarePesos));
        }

        bool IsForwardSelfInterchange(RouteInterchange edge)
        {
            if (edge.OtherIndex <= edge.OwnIndex)
                return false;

            var anchors = _routeSearchAnchors[edge.OtherRouteId];
            return anchors[edge.OtherIndex].DistanceFromRouteStartMeters -
                anchors[edge.OwnIndex].DistanceFromRouteStartMeters >=
                MinimumSelfTransferProgressMeters;
        }
    }

    private sealed record TransferSearchStep(string FromRouteId, RouteInterchange Edge);
    private sealed record RouteProgressState(string RouteId, int EntryIndex);
    private sealed record TransferSearchState(
        string CurrentRouteId, int EntryIndex, AccessCandidate BoardAccess,
        List<TransferSearchStep> Steps, HashSet<string> VisitedRoutes,
        HashSet<RouteProgressState> VisitedProgressStates,
        double TransferWalkingMeters, double AccumulatedCost);
}
