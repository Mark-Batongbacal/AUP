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
        IReadOnlyDictionary<string, IReadOnlyList<AccessCandidate>[]> boardPrefixes,
        IReadOnlyDictionary<string, IReadOnlyList<AccessCandidate>>
            destinationAccessByRoute,
        CancellationToken cancellationToken)
    {
        if (MaxTransfers == 0) yield break;

        var interchangeCandidatesEvaluated = 0L;
        var routeNames = _routes.ToDictionary(
            route => route.RouteId,
            route => route.RouteName);
        var dominance = new Dictionary<string, double>(StringComparer.Ordinal);
        try
        {
        var emitted = 0;
        var rootCompletionsEmitted = 0;
        // A safety ceiling only. Per-route and per-level bounds below are what
        // actually shape the pool; this exists so a pathological network
        // cannot run away.
        var globalLimit = MaxCandidatesToConfirm *
            Math.Max(1, MaxTransfers + 1) *
            Math.Max(1, _routes.Count);
        var rootCompletionLimit = MaxCandidatesToConfirm *
            Math.Max(1, _routes.Count);
        foreach (var startRoute in _routes)
        {
            if (!_interchangesByRoute.TryGetValue(startRoute.RouteId, out var firstEdges))
                continue;

            var originStates = BuildOriginStates(startRoute.RouteId, firstEdges);
            var frontier = BuildInitialFrontier(startRoute.RouteId, firstEdges);
            if (frontier.Count == 0)
                continue;

            // Every interchange region of this route gets a place at the first
            // level, and each further level gets the same allowance, so a
            // deeper journey is never crowded out by shallower ones.
            var perLevelLimit = Math.Max(MinTransferCandidatesPerRoute, frontier.Count);

            // The origin-boarded route is a viable search state too. Give it
            // the same destination-completion edge as every post-transfer
            // state, before considering an outgoing transfer. These terminal
            // edges do not consume transfer-frontier capacity.
            foreach (var completion in SelectDestinationCompletions(
                         originStates,
                         perLevelLimit))
            {
                if (rootCompletionsEmitted >= rootCompletionLimit)
                    break;

                yield return completion;
                rootCompletionsEmitted++;
            }

            for (var depth = 1; depth <= MaxTransfers && frontier.Count > 0; depth++)
            {
                var emittedAtDepth = 0;

                foreach (var candidate in SelectDestinationCompletions(
                             frontier,
                             perLevelLimit))
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

        }
        finally
        {
            _telemetry.IncrementRouting(
                "transfer_interchange_candidates_evaluated",
                interchangeCandidatesEvaluated);
        }

        yield break;

        List<TransferSearchState> BuildOriginStates(
            string startRouteId,
            IReadOnlyList<RouteInterchange> firstEdges)
        {
            var states = new Dictionary<string, TransferSearchState>(
                StringComparer.Ordinal);
            var samples = _routeSamples[startRouteId];

            foreach (var first in firstEdges)
            {
                interchangeCandidatesEvaluated++;
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

                foreach (var board in boardPrefixes[startRouteId][first.OwnIndex])
                {
                    var boardIndex = board.RouteSampleIndex ??
                        GetNearestSampleIndex(samples, board.Anchor);
                    var key = AccessOccurrenceKey(board);
                    var state = new TransferSearchState(
                        startRouteId,
                        boardIndex,
                        board,
                        [],
                        new HashSet<string>(StringComparer.Ordinal)
                            { startRouteId },
                        new HashSet<RouteProgressState>
                            { new(startRouteId, boardIndex) },
                        0,
                        board.GeneralizedCostPesos);

                    if (!states.TryGetValue(key, out var existing) ||
                        state.AccumulatedCost < existing.AccumulatedCost)
                    {
                        states[key] = state;
                    }
                }
            }

            return states.Values
                .OrderBy(state => GetAccessProgressMeters(state.BoardAccess))
                .ThenBy(state => state.AccumulatedCost)
                .ToList();
        }

        List<TransferSearchState> BuildInitialFrontier(
            string startRouteId,
            IReadOnlyList<RouteInterchange> firstEdges)
        {
            var states = new List<TransferSearchState>();

            foreach (var first in firstEdges)
            {
                interchangeCandidatesEvaluated++;
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

                foreach (var board in boardPrefixes[startRouteId][first.OwnIndex])
                {
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
            }

            return states;
        }

        /// Reserve one child for every distinct downstream transfer occurrence
        /// before admitting a second origin-boarding/path variant for any of
        /// them. Bounded boarding prefixes intentionally put several access
        /// occurrences in <paramref name="current"/>; treating each occurrence
        /// as its own first-class queue lets those variants fill the whole next
        /// level with whichever route happens to be enumerated first.
        ///
        /// Exact route indices are part of the bucket. Two visits to the same
        /// physical area on a loop therefore remain distinct route occurrences.
        List<TransferSearchState> BuildNextFrontier(
            List<TransferSearchState> current,
            int maxStates)
        {
            // Resolve dominance across the complete level before applying the
            // bound. This makes both the winning state and bucket ordering
            // independent of repository/interchange enumeration order. Do not
            // publish unselected states to the cross-frontier dominance table:
            // a state that never entered search must not suppress a later one.
            var bestByDominanceKey = new Dictionary<string, TransferExpansion>(
                StringComparer.Ordinal);

            foreach (var state in current)
            {
                foreach (var expansion in NextStates(state))
                {
                    if (dominance.TryGetValue(
                            expansion.DominanceKey,
                            out var priorCost) &&
                        priorCost <= expansion.State.AccumulatedCost)
                    {
                        continue;
                    }

                    if (!bestByDominanceKey.TryGetValue(
                            expansion.DominanceKey,
                            out var existing) ||
                        CompareExpansions(expansion, existing) < 0)
                    {
                        bestByDominanceKey[expansion.DominanceKey] = expansion;
                    }
                }
            }

            // Two levels of rotation are intentional. First give each
            // downstream route a turn; within that route, give each exact
            // interchange occurrence a turn before taking another access/path
            // variant from the same occurrence. Route groups start with their
            // best provisional state, with route ID only as a stable tie-break,
            // so a tight bound is not allocated by lexical route order.
            var routeQueues = bestByDominanceKey.Values
                .GroupBy(expansion => expansion.Bucket.OtherRouteId)
                .Select(group => (
                    RouteId: group.Key,
                    BestCost: group.Min(expansion =>
                        expansion.State.AccumulatedCost),
                    States: BuildOccurrenceRoundRobin(group)))
                .OrderBy(group => group.BestCost)
                .ThenBy(group => group.RouteId, StringComparer.Ordinal)
                .ToList();

            var selected = new List<TransferExpansion>();
            while (selected.Count < maxStates)
            {
                var addedAny = false;

                foreach (var route in routeQueues)
                {
                    if (!route.States.TryDequeue(out var expansion))
                        continue;

                    selected.Add(expansion);
                    addedAny = true;
                    if (selected.Count >= maxStates)
                        break;
                }

                if (!addedAny)
                    break;
            }

            foreach (var expansion in selected)
                dominance[expansion.DominanceKey] =
                    expansion.State.AccumulatedCost;

            return selected.Select(expansion => expansion.State).ToList();
        }

        Queue<TransferExpansion> BuildOccurrenceRoundRobin(
            IEnumerable<TransferExpansion> routeExpansions)
        {
            var occurrenceQueues = routeExpansions
                .GroupBy(expansion => expansion.Bucket)
                .OrderBy(group => group.Min(expansion =>
                    expansion.State.AccumulatedCost))
                .ThenBy(group => group.Key.FromRouteId, StringComparer.Ordinal)
                .ThenBy(group => group.Key.OwnIndex)
                .ThenBy(group => group.Key.OtherIndex)
                .Select(group => new Queue<TransferExpansion>(group
                    .OrderBy(expansion => expansion.State.AccumulatedCost)
                    .ThenBy(expansion => expansion.State.TransferWalkingMeters)
                    .ThenBy(expansion => GetAccessProgressMeters(
                        expansion.State.BoardAccess))
                    .ThenBy(expansion => expansion.State.BoardAccess.Anchor.Latitude)
                    .ThenBy(expansion => expansion.State.BoardAccess.Anchor.Longitude)
                    .ThenBy(expansion => TransferPathKey(expansion.State),
                        StringComparer.Ordinal)))
                .ToList();
            var result = new Queue<TransferExpansion>();

            while (true)
            {
                var addedAny = false;
                foreach (var occurrence in occurrenceQueues)
                {
                    if (!occurrence.TryDequeue(out var expansion))
                        continue;

                    result.Enqueue(expansion);
                    addedAny = true;
                }

                if (!addedAny)
                    return result;
            }
        }

        IEnumerable<TransferExpansion> NextStates(TransferSearchState state)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_interchangesByRoute.TryGetValue(state.CurrentRouteId, out var edges))
                yield break;

            foreach (var edge in edges)
            {
                interchangeCandidatesEvaluated++;
                cancellationToken.ThrowIfCancellationRequested();

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
                    $"{AccessOccurrenceKey(state.BoardAccess)}:" +
                    string.Join(',', state.VisitedRoutes.OrderBy(value => value));
                var child = new TransferSearchState(
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

                yield return new TransferExpansion(
                    child,
                    new TransferExpansionBucket(
                        state.CurrentRouteId,
                        edge.OwnIndex,
                        edge.OtherRouteId,
                        edge.OtherIndex),
                    key);
            }
        }

        static int CompareExpansions(
            TransferExpansion left,
            TransferExpansion right)
        {
            var cost = left.State.AccumulatedCost.CompareTo(
                right.State.AccumulatedCost);
            if (cost != 0)
                return cost;

            var walking = left.State.TransferWalkingMeters.CompareTo(
                right.State.TransferWalkingMeters);
            if (walking != 0)
                return walking;

            return StringComparer.Ordinal.Compare(
                TransferPathKey(left.State),
                TransferPathKey(right.State));
        }

        static string TransferPathKey(TransferSearchState state) =>
            string.Join('|', state.Steps.Select(step => string.Join(':',
                step.FromRouteId,
                step.Edge.OwnIndex,
                step.Edge.OtherRouteId,
                step.Edge.OtherIndex))) +
            $"|{AccessOccurrenceKey(state.BoardAccess)}";

        List<JourneyCandidate> SelectDestinationCompletions(
            IReadOnlyList<TransferSearchState> states,
            int maxCandidates)
        {
            var queues = states
                .Select(state => new Queue<JourneyCandidate>(
                    BuildDestinationCandidates(state)
                        .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                        .ThenBy(candidate => GetAlightProgressMeters(
                            candidate.Legs[^1]))))
                .Where(queue => queue.Count > 0)
                .ToList();
            var selected = new List<JourneyCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (selected.Count < maxCandidates)
            {
                var addedAny = false;
                foreach (var queue in queues)
                {
                    while (queue.TryDequeue(out var candidate))
                    {
                        if (!seen.Add(GetJourneyCandidateKey(candidate)))
                            continue;

                        selected.Add(candidate);
                        addedAny = true;
                        break;
                    }

                    if (selected.Count >= maxCandidates)
                        break;
                }

                if (!addedAny)
                    break;
            }

            return selected;
        }

        IEnumerable<JourneyCandidate> BuildDestinationCandidates(
            TransferSearchState state)
        {
            if (!destinationAccessByRoute.TryGetValue(
                    state.CurrentRouteId,
                    out var allDestinationAccess))
            {
                yield break;
            }

            var currentSamples = _routeSamples[state.CurrentRouteId];
            var entryPoint = state.Steps.Count == 0
                ? state.BoardAccess.Anchor
                : currentSamples[state.EntryIndex];
            var entryAnchor = state.Steps.Count == 0
                ? state.BoardAccess.FullRouteAnchor ?? GetRouteAnchor(
                    state.CurrentRouteId,
                    state.EntryIndex,
                    entryPoint)
                : GetRouteAnchor(
                    state.CurrentRouteId,
                    state.EntryIndex,
                    entryPoint);
            var eligible = allDestinationAccess
                .Where(access => GetAccessProgressMeters(access) >
                    entryAnchor.DistanceFromRouteStartMeters)
                .ToList();

            foreach (var alight in SelectUsefulAccessStates(eligible, []))
            {
                var alightIndex = alight.RouteSampleIndex ?? GetNearestSampleIndex(
                    currentSamples,
                    alight.Anchor);
                var alightAnchor = alight.FullRouteAnchor ?? GetRouteAnchor(
                    state.CurrentRouteId,
                    alightIndex,
                    alight.Anchor);
                if (alightAnchor.DistanceFromRouteStartMeters <=
                    entryAnchor.DistanceFromRouteStartMeters)
                {
                    continue;
                }

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
                        ? state.BoardAccess.RouteSampleIndex ??
                          GetNearestSampleIndex(samples, boardPoint)
                        : state.Steps[index - 1].Edge.OtherIndex;
                    var alightPoint = samples[step.Edge.OwnIndex];
                    journeyLegs.Add(new(
                        step.FromRouteId,
                        routeNames[step.FromRouteId],
                        boardPoint,
                        alightPoint,
                        boardIndex,
                        step.Edge.OwnIndex,
                        index == 0
                            ? state.BoardAccess.FullRouteAnchor
                            : GetRouteAnchor(
                                step.FromRouteId,
                                boardIndex,
                                boardPoint),
                        GetRouteAnchor(
                            step.FromRouteId,
                            step.Edge.OwnIndex,
                            alightPoint)));
                }

                journeyLegs.Add(new(
                    state.CurrentRouteId,
                    routeNames[state.CurrentRouteId],
                    entryPoint,
                    alight.Anchor,
                    state.EntryIndex,
                    alightIndex,
                    entryAnchor,
                    alightAnchor));
                if (journeyLegs.Any(leg => RouteDistanceBetweenAnchors(
                        leg.BoardFullRouteAnchor!,
                        leg.AlightFullRouteAnchor!) <= 0))
                {
                    continue;
                }

                var walks = state.Steps.Select(step => new WalkSegmentCandidate(
                    _routeSamples[step.FromRouteId][step.Edge.OwnIndex],
                    _routeSamples[step.Edge.OtherRouteId][step.Edge.OtherIndex],
                    step.Edge.DistanceMeters)).ToList();
                yield return new JourneyCandidate(
                    journeyLegs,
                    state.BoardAccess,
                    alight,
                    walks,
                    state.AccumulatedCost + alight.GeneralizedCostPesos +
                    GeneralizedCostFromTimeAndFare(
                        EstimateJeepneyTravelTimeSeconds(journeyLegs),
                        journeyLegs.Count * JeepneyBaseFarePesos));
            }
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
    internal sealed record TransferExpansionBucket(
        string FromRouteId,
        int OwnIndex,
        string OtherRouteId,
        int OtherIndex);
    private sealed record TransferExpansion(
        TransferSearchState State,
        TransferExpansionBucket Bucket,
        string DominanceKey);
    private sealed record TransferSearchState(
        string CurrentRouteId, int EntryIndex, AccessCandidate BoardAccess,
        List<TransferSearchStep> Steps, HashSet<string> VisitedRoutes,
        HashSet<RouteProgressState> VisitedProgressStates,
        double TransferWalkingMeters, double AccumulatedCost);
}
