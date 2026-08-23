using backend.Models.Routing;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Reserves confirmation capacity for distinct route/boarding regions in
    /// addition to the normal cost, fare, time, and low-access objectives.
    /// Dense samples from one corridor therefore cannot crowd every other
    /// useful route out before Valhalla confirmation.
    /// </summary>
    private List<JourneyCandidate> SelectCandidatesToConfirmWithDiversity(
        List<JourneyCandidate> candidates)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var selected = new List<JourneyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var diversityQuota = Math.Max(1, MaxCandidatesToConfirm / 4);
        var objectiveQuota = Math.Max(
            1,
            (MaxCandidatesToConfirm - diversityQuota) / 4);

        AddDiverse(diversityQuota);
        Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateOriginAccessDistanceMeters)
            .ThenBy(candidate => candidate.OriginAccess.TotalTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);

        if (selected.Count < MaxCandidatesToConfirm)
        {
            Add(
                candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos),
                MaxCandidatesToConfirm - selected.Count);
        }

        _logger.LogDebug(
            "Routing candidate diversity selected {SelectedCount} of {CandidateCount} candidates for confirmation",
            selected.Count,
            candidates.Count);

        return selected;

        void Add(IEnumerable<JourneyCandidate> source, int limit)
        {
            var added = 0;
            foreach (var candidate in source)
            {
                if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                    break;

                if (!seen.Add(GetJourneyCandidateKey(candidate)))
                    continue;

                selected.Add(candidate);
                added++;
            }
        }

        void AddDiverse(int limit)
        {
            var buckets = candidates
                .GroupBy(GetBoardingDiversityKey, StringComparer.Ordinal)
                .Select(group => new Queue<JourneyCandidate>(group
                    .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                    .ThenBy(EstimateCandidateTimeSeconds)))
                .OrderBy(queue => queue.Peek().TotalGeneralizedCostPesos)
                .ToList();

            var added = 0;
            while (added < limit &&
                   selected.Count < MaxCandidatesToConfirm &&
                   buckets.Any(queue => queue.Count > 0))
            {
                foreach (var queue in buckets)
                {
                    while (queue.Count > 0)
                    {
                        var candidate = queue.Dequeue();
                        if (!seen.Add(GetJourneyCandidateKey(candidate)))
                            continue;

                        selected.Add(candidate);
                        added++;
                        break;
                    }

                    if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                        break;
                }
            }
        }
    }

    private string GetBoardingDiversityKey(JourneyCandidate candidate)
    {
        var bucketSize = _options.BoardingDiversityBucketMeters;
        return string.Join('>', candidate.Legs.Select(leg =>
        {
            var boardBucket = (long)Math.Floor(
                GetBoardProgressMeters(leg) / bucketSize);
            return $"{leg.RouteId}@{boardBucket}";
        }));
    }

    /// <summary>
    /// Applies the same feeder-replaces-transit principle (see
    /// PruneConfirmedFeederShadowing) to transfer boarding. A passenger
    /// should not walk/trike substantially downstream along the next jeepney
    /// route when an earlier confirmed transfer boarding is already
    /// accessible on the same route: that is the transfer replacing most of
    /// route B, not a network-access optimization, and no fastest/cheapest
    /// gain excuses it.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneConfirmedTransferBoardingShadowing(
        List<ConfirmedJourneyCandidate> candidates)
    {
        var kept = candidates;
        var maxLegCount = candidates.Count == 0
            ? 0
            : candidates.Max(candidate => candidate.Candidate.Legs.Count);

        for (var legIndex = 1; legIndex < maxLegCount; legIndex++)
        {
            var next = new List<ConfirmedJourneyCandidate>();

            foreach (var group in kept.GroupBy(
                         candidate => GetTransferBoardingComparisonKey(candidate, legIndex),
                         StringComparer.Ordinal))
            {
                var variants = group
                    .Where(candidate => candidate.Candidate.Legs.Count > legIndex)
                    .ToList();
                var passthrough = group
                    .Where(candidate => candidate.Candidate.Legs.Count <= legIndex)
                    .ToList();
                next.AddRange(passthrough);

                if (variants.Count <= 1)
                {
                    next.AddRange(variants);
                    continue;
                }

                var accessibleVariants = variants
                    .Where(candidate => ConfirmedTransferDistanceMeters(candidate, legIndex) is not null)
                    .ToList();

                foreach (var variant in variants)
                {
                    var variantTransferDistance =
                        ConfirmedTransferDistanceMeters(variant, legIndex);
                    if (variantTransferDistance is null)
                    {
                        next.Add(variant);
                        continue;
                    }

                    var baseline = FindEarlierAccessibleBaseline(
                        accessibleVariants,
                        variant,
                        candidate => GetBoardProgressMeters(candidate.Candidate.Legs[legIndex]),
                        candidate => ConfirmedTransferDistanceMeters(candidate, legIndex)!.Value);

                    if (baseline is null)
                    {
                        next.Add(variant);
                        continue;
                    }

                    var baselineTransferDistance =
                        ConfirmedTransferDistanceMeters(baseline, legIndex)!.Value;

                    if (IsFeederReplacingTransit(
                            laterProgress: GetBoardProgressMeters(variant.Candidate.Legs[legIndex]),
                            earlierProgress: GetBoardProgressMeters(baseline.Candidate.Legs[legIndex]),
                            laterAccessDistance: variantTransferDistance.Value,
                            earlierAccessDistance: baselineTransferDistance,
                            out var skippedProgress,
                            out var extraTransferDistance))
                    {
                        LogFeederShadowRejection(
                            $"transfer feeder shadowing at leg {legIndex}",
                            variant,
                            baseline,
                            variant.Candidate.Legs[legIndex].RouteId,
                            legIndex,
                            baselineTransferDistance,
                            variantTransferDistance.Value,
                            skippedProgress,
                            extraTransferDistance);
                        continue;
                    }

                    next.Add(variant);
                }
            }

            kept = next;
        }

        return kept;
    }

    /// <summary>
    /// Identifies the downstream itinerary from the target leg onward: the
    /// route/alighting sequence, with the target leg's own boarding position
    /// masked out since that is exactly the alternative being compared. How
    /// the passenger reached the trip's first jeepney (origin access mode/
    /// TODA identity) and how they finish from the last one (destination
    /// access mode/TODA identity) are excluded -- neither is the axis of
    /// variation this comparison evaluates, and including them would
    /// fragment equivalent transfer alternatives the same way origin access
    /// mode fragmented origin-side boarding comparisons.
    /// </summary>
    private string GetTransferBoardingComparisonKey(
        ConfirmedJourneyCandidate candidate,
        int targetLegIndex)
    {
        var bucketSize = _options.BoardingDiversityBucketMeters;
        return string.Join('>', candidate.Candidate.Legs.Select((leg, index) =>
        {
            var alightBucket = (long)Math.Floor(
                GetAlightProgressMeters(leg) / bucketSize);
            var boardPart = index == targetLegIndex
                ? "*"
                : ((long)Math.Floor(GetBoardProgressMeters(leg) / bucketSize)).ToString();
            return $"{leg.RouteId}:{boardPart}-{alightBucket}";
        }));
    }

    private static double? ConfirmedTransferDistanceMeters(
        ConfirmedJourneyCandidate candidate,
        int boardingLegIndex)
    {
        var transferIndex = boardingLegIndex - 1;
        return transferIndex >= 0 &&
               transferIndex < candidate.Plan.TransferWalkDistancesMeters.Count
            ? candidate.Plan.TransferWalkDistancesMeters[transferIndex]
            : null;
    }

    /// <summary>
    /// Conservative Pareto pruning over confirmed journeys. A plan is removed
    /// only if another confirmed plan is no worse in every user-visible burden
    /// dimension and strictly better in at least one.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneDominatedConfirmedCandidates(
        List<ConfirmedJourneyCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        const double epsilon = 0.001;
        var metrics = candidates.ToDictionary(
            candidate => candidate,
            BuildConfirmedParetoMetrics);
        var kept = new List<ConfirmedJourneyCandidate>();

        foreach (var candidate in candidates)
        {
            var current = metrics[candidate];
            var dominator = candidates.FirstOrDefault(other =>
            {
                if (ReferenceEquals(other, candidate))
                    return false;

                var otherMetrics = metrics[other];
                var noWorse =
                    otherMetrics.TimeSeconds <= current.TimeSeconds + epsilon &&
                    otherMetrics.FarePesos <= current.FarePesos + epsilon &&
                    otherMetrics.GeneralizedCostPesos <= current.GeneralizedCostPesos + epsilon &&
                    otherMetrics.AccessBurdenSeconds <= current.AccessBurdenSeconds + epsilon &&
                    otherMetrics.WalkingMeters <= current.WalkingMeters + epsilon &&
                    otherMetrics.TransferCount <= current.TransferCount;
                var strictlyBetter =
                    otherMetrics.TimeSeconds < current.TimeSeconds - epsilon ||
                    otherMetrics.FarePesos < current.FarePesos - epsilon ||
                    otherMetrics.GeneralizedCostPesos < current.GeneralizedCostPesos - epsilon ||
                    otherMetrics.AccessBurdenSeconds < current.AccessBurdenSeconds - epsilon ||
                    otherMetrics.WalkingMeters < current.WalkingMeters - epsilon ||
                    otherMetrics.TransferCount < current.TransferCount;

                return noWorse && strictlyBetter;
            });

            if (dominator is null)
            {
                kept.Add(candidate);
                continue;
            }

            _logger.LogDebug(
                "Routing candidate rejected: Pareto dominated. routes={Routes} time={Time:F0}s fare={Fare:F2} cost={Cost:F2} access={Access:F0}s walk={Walk:F0}m transfers={Transfers}",
                RouteSequence(candidate),
                current.TimeSeconds,
                current.FarePesos,
                current.GeneralizedCostPesos,
                current.AccessBurdenSeconds,
                current.WalkingMeters,
                current.TransferCount);
        }

        return kept;
    }

    private static ConfirmedParetoMetrics BuildConfirmedParetoMetrics(
        ConfirmedJourneyCandidate candidate)
    {
        var plan = candidate.Plan;
        return new ConfirmedParetoMetrics(
            plan.TotalTimeSeconds,
            plan.TotalFarePesos,
            plan.GeneralizedCostPesos,
            plan.Legs
                .Where(leg => leg.Mode != AccessMode.Jeepney)
                .Sum(leg => leg.DurationSeconds),
            plan.Legs
                .Where(leg => leg.Mode == AccessMode.Walk)
                .Sum(leg => leg.DistanceMeters),
            plan.TransferCount);
    }

    private void LogConfirmedPruningDelta(
        IReadOnlyCollection<ConfirmedJourneyCandidate> before,
        IReadOnlyCollection<ConfirmedJourneyCandidate> after,
        string reason)
    {
        if (!_logger.IsEnabled(LogLevel.Debug) || before.Count == after.Count)
            return;

        var survivors = new HashSet<ConfirmedJourneyCandidate>(after);
        foreach (var candidate in before.Where(candidate => !survivors.Contains(candidate)))
        {
            _logger.LogDebug(
                "Routing candidate rejected: {Reason}; routes={Routes} board={BoardProgress:F0}m access={AccessDistance:F0}m time={Time:F0}s fare={Fare:F2} cost={Cost:F2}",
                reason,
                RouteSequence(candidate),
                GetBoardProgressMeters(candidate.Candidate.Legs[0]),
                ConfirmedOriginAccessDistanceMeters(candidate),
                candidate.Plan.TotalTimeSeconds,
                candidate.Plan.TotalFarePesos,
                candidate.Plan.GeneralizedCostPesos);
        }
    }

    private static string RouteSequence(ConfirmedJourneyCandidate candidate) =>
        string.Join('>', candidate.Candidate.Legs.Select(leg => leg.RouteId));

    /// <summary>
    /// Rejects physically disconnected leg chains before objective selection.
    /// Geometry validation is optional because road geometry is enriched only
    /// after the routing plan itself has been selected.
    /// </summary>
    private bool ValidatePlanContinuity(
        JeepneyTripPlan plan,
        bool requireGeometry,
        string stage)
    {
        if (plan.Legs.Count == 0)
        {
            _logger.LogWarning("Routing plan rejected at {Stage}: plan has no physical legs", stage);
            return false;
        }

        for (var index = 0; index < plan.Legs.Count - 1; index++)
        {
            var current = plan.Legs[index];
            var next = plan.Legs[index + 1];
            var gap = ApproximateDistanceMeters(
                current.DestinationLatitude,
                current.DestinationLongitude,
                next.OriginLatitude,
                next.OriginLongitude);

            if (gap <= _options.JourneyLegContinuityToleranceMeters)
                continue;

            _logger.LogWarning(
                "Routing plan rejected at {Stage}: disconnected legs {CurrentIndex}->{NextIndex}, gap={Gap:F1}m, currentMode={CurrentMode}, nextMode={NextMode}",
                stage,
                index,
                index + 1,
                gap,
                current.Mode,
                next.Mode);
            return false;
        }

        if (!requireGeometry)
            return true;

        foreach (var leg in plan.Legs)
        {
            if (leg.Geometry.Count < 2)
            {
                _logger.LogWarning(
                    "Routing geometry validation failed at {Stage}: {Mode} leg has fewer than two points",
                    stage,
                    leg.Mode);
                return false;
            }

            var first = leg.Geometry[0];
            var last = leg.Geometry[^1];
            var startGap = ApproximateDistanceMeters(
                leg.OriginLatitude,
                leg.OriginLongitude,
                first.Latitude,
                first.Longitude);
            var endGap = ApproximateDistanceMeters(
                leg.DestinationLatitude,
                leg.DestinationLongitude,
                last.Latitude,
                last.Longitude);

            if (startGap > _options.JourneyLegContinuityToleranceMeters ||
                endGap > _options.JourneyLegContinuityToleranceMeters)
            {
                _logger.LogWarning(
                    "Routing geometry validation failed at {Stage}: {Mode} leg endpoint gap start={StartGap:F1}m end={EndGap:F1}m",
                    stage,
                    leg.Mode,
                    startGap,
                    endGap);
                return false;
            }
        }

        return true;
    }

    private void NormalizeAndValidatePlanGeometry(JeepneyTripPlan plan)
    {
        foreach (var leg in plan.Legs)
            AnchorGeometryToPhysicalLeg(leg);

        if (!ValidatePlanContinuity(plan, requireGeometry: true, stage: "geometry-enrichment"))
        {
            _logger.LogWarning(
                "Selected routing plan has invalid enriched geometry after endpoint normalization; routes={Routes}",
                string.Join('>', plan.Legs
                    .Where(leg => leg.Mode == AccessMode.Jeepney)
                    .Select(leg => leg.RouteId)));
        }
    }

    private void AnchorGeometryToPhysicalLeg(JeepneyTripLeg leg)
    {
        var origin = new RouteGeometryPoint(leg.OriginLatitude, leg.OriginLongitude);
        var destination = new RouteGeometryPoint(
            leg.DestinationLatitude,
            leg.DestinationLongitude);

        if (leg.Geometry.Count == 0)
        {
            leg.Geometry = [origin, destination];
            return;
        }

        var first = leg.Geometry[0];
        var startGap = ApproximateDistanceMeters(
            origin.Latitude,
            origin.Longitude,
            first.Latitude,
            first.Longitude);
        if (startGap <= _options.JourneyLegContinuityToleranceMeters)
            leg.Geometry[0] = origin;
        else
            leg.Geometry.Insert(0, origin);

        if (leg.Geometry.Count == 1)
        {
            leg.Geometry.Add(destination);
            return;
        }

        var last = leg.Geometry[^1];
        var endGap = ApproximateDistanceMeters(
            destination.Latitude,
            destination.Longitude,
            last.Latitude,
            last.Longitude);
        if (endGap <= _options.JourneyLegContinuityToleranceMeters)
            leg.Geometry[^1] = destination;
        else
            leg.Geometry.Add(destination);
    }

    private void LogSelectedPlanDiagnostics(IEnumerable<JeepneyTripPlan> plans)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        foreach (var plan in plans)
        {
            var jeepneyLegs = plan.Legs
                .Where(leg => leg.Mode == AccessMode.Jeepney)
                .ToList();
            var boards = string.Join(';', jeepneyLegs.Select(leg =>
                $"{leg.RouteId}@{leg.BoardLatitude:F6},{leg.BoardLongitude:F6}"));

            _logger.LogDebug(
                "Routing selected plan recommendation={Recommendation} routes={Routes} boards={Boards} time={Time:F0}s fare={Fare:F2} cost={Cost:F2} transfers={Transfers} accessBurden={Access:F0}s",
                plan.RecommendationType,
                string.Join('>', jeepneyLegs.Select(leg => leg.RouteId)),
                boards,
                plan.TotalTimeSeconds,
                plan.TotalFarePesos,
                plan.GeneralizedCostPesos,
                plan.TransferCount,
                plan.Legs.Where(leg => leg.Mode != AccessMode.Jeepney)
                    .Sum(leg => leg.DurationSeconds));
        }
    }

    private sealed record ConfirmedParetoMetrics(
        double TimeSeconds,
        double FarePesos,
        double GeneralizedCostPesos,
        double AccessBurdenSeconds,
        double WalkingMeters,
        int TransferCount);
}