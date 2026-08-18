namespace backend.Services.Routing;

public partial class RoutingService
{
    private IEnumerable<JourneyCandidate> FindTransferCandidates(
        IReadOnlyDictionary<string, (double[] Cost, AccessCandidate?[] Access)> boardPrefixes,
        IReadOnlyDictionary<string, (double[] Cost, AccessCandidate?[] Access)> alightSuffixes,
        CancellationToken cancellationToken)
    {
        if (MaxTransfers == 0) yield break;
        var routeNames = _routes.ToDictionary(route => route.RouteId, route => route.RouteName);
        var emitted = 0;
        var dominance = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var startRoute in _routes)
        {
            if (!_interchangesByRoute.TryGetValue(startRoute.RouteId, out var firstEdges) ||
                !_routeSamples.TryGetValue(startRoute.RouteId, out var startSamples)) continue;
            foreach (var first in firstEdges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (first.OwnIndex <= 0 || first.DistanceMeters > MaxTransferWalkMeters) continue;
                var board = boardPrefixes[startRoute.RouteId].Access[first.OwnIndex];
                if (board is null) continue;
                var state = new TransferSearchState(
                    first.OtherRouteId, first.OtherIndex, board,
                    [new TransferSearchStep(startRoute.RouteId, first)],
                    new HashSet<string>(StringComparer.Ordinal)
                        { startRoute.RouteId, first.OtherRouteId },
                    first.DistanceMeters,
                    board.GeneralizedCostPesos + GeneralizedCostFromWalking(
                        first.DistanceMeters / WalkingSpeedMetersPerSecond,
                        first.DistanceMeters));
                foreach (var candidate in Expand(state))
                {
                    yield return candidate;
                    if (++emitted >= MaxCandidatesToConfirm) yield break;
                }
            }
        }

        IEnumerable<JourneyCandidate> Expand(TransferSearchState state)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = $"{state.CurrentRouteId}:{state.EntryIndex / 2}:{state.Steps.Count}:" +
                string.Join(',', state.VisitedRoutes.OrderBy(value => value));
            if (dominance.TryGetValue(key, out var best) && best <= state.AccumulatedCost)
                yield break;
            dominance[key] = state.AccumulatedCost;

            var destination = BuildDestinationCandidate(state);
            if (destination is not null) yield return destination;
            if (state.Steps.Count >= MaxTransfers) yield break;
            if (!_interchangesByRoute.TryGetValue(state.CurrentRouteId, out var edges)) yield break;
            foreach (var edge in edges)
            {
                if (edge.OwnIndex <= state.EntryIndex ||
                    edge.DistanceMeters > MaxTransferWalkMeters ||
                    state.VisitedRoutes.Contains(edge.OtherRouteId) ||
                    edge.OtherIndex >= _routeSamples[edge.OtherRouteId].Count - 1) continue;
                var totalWalking = state.TransferWalkingMeters + edge.DistanceMeters;
                if (totalWalking > MaxTotalWalkingMetersPerJourney) continue;
                var visited = new HashSet<string>(state.VisitedRoutes, StringComparer.Ordinal)
                    { edge.OtherRouteId };
                var next = new TransferSearchState(
                    edge.OtherRouteId, edge.OtherIndex, state.BoardAccess,
                    [.. state.Steps, new TransferSearchStep(state.CurrentRouteId, edge)],
                    visited, totalWalking,
                    state.AccumulatedCost + GeneralizedCostFromWalking(
                        edge.DistanceMeters / WalkingSpeedMetersPerSecond,
                        edge.DistanceMeters));
                foreach (var candidate in Expand(next)) yield return candidate;
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
    }

    private sealed record TransferSearchStep(string FromRouteId, RouteInterchange Edge);
    private sealed record TransferSearchState(
        string CurrentRouteId, int EntryIndex, AccessCandidate BoardAccess,
        List<TransferSearchStep> Steps, HashSet<string> VisitedRoutes,
        double TransferWalkingMeters, double AccumulatedCost);
}
