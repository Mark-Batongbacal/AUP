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
    internal List<JourneyCandidate> SelectCandidatesToConfirmWithDiversity(
        List<JourneyCandidate> candidates)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var selected = new List<JourneyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var physicalDiversityQuota = Math.Max(1, MaxCandidatesToConfirm / 4);
        var objectiveQuota = Math.Max(
            1,
            (MaxCandidatesToConfirm - physicalDiversityQuota) / 4);

        // Physical route occurrences and access profiles solve different
        // pre-confirmation failure modes. A cheap straight-line walk can be
        // the best provisional candidate in every boarding bucket, yet fail
        // once Valhalla measures the real network walk. Preserve the complete
        // physical-region reservation, and use the former low-origin-access
        // objective slice for bounded origin/destination mode + TODA
        // fallbacks. The total confirmation budget is unchanged.
        AddDiverse(physicalDiversityQuota, GetBoardingDiversityKey);
        Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        AddAccessProfileDiverse(objectiveQuota);

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

        void AddDiverse(
            int limit,
            Func<JourneyCandidate, string> diversityKey)
        {
            var buckets = candidates
                .GroupBy(diversityKey, StringComparer.Ordinal)
                .Select(group => new Queue<JourneyCandidate>(group
                    .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                    .ThenBy(EstimateCandidateTimeSeconds)
                    .ThenBy(GetJourneyCandidateKey, StringComparer.Ordinal)))
                .OrderBy(queue => queue.Peek().TotalGeneralizedCostPesos)
                .ThenBy(queue => diversityKey(queue.Peek()), StringComparer.Ordinal)
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

        void AddAccessProfileDiverse(int limit)
        {
            if (limit <= 0)
                return;

            // First divide by the coarse mode pair so walk/walk cannot consume
            // the complete access-fallback reservation. Within each pair, one
            // representative per route sequence + concrete TODA + physical
            // occurrence keeps the low-origin-access objective this quota
            // replaces. The occurrence component is essential: boarding and
            // transfer regions that confirm differently cannot be collapsed
            // merely because their modes and route IDs match. Choose the
            // shortest access within each joint bucket, then let bucket
            // representatives compete by complete provisional journey cost.
            // Round-robin selection remains deterministic and bounded.
            var modePairQueues = candidates
                .GroupBy(GetAccessModePairKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Queue<JourneyCandidate>(group
                    .GroupBy(GetAccessProfileRegionDiversityKey, StringComparer.Ordinal)
                    .Select(profile => new
                    {
                        Representative = profile
                            .OrderBy(EstimateCandidateOriginAccessDistanceMeters)
                            .ThenBy(candidate => candidate.OriginAccess.TotalTimeSeconds)
                            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
                            .ThenBy(EstimateCandidateTimeSeconds)
                            .ThenBy(GetJourneyCandidateKey, StringComparer.Ordinal)
                            .First(),
                        ProvisionalProfileCost = profile.Min(candidate =>
                            candidate.TotalGeneralizedCostPesos)
                    })
                    .OrderBy(profile => profile.ProvisionalProfileCost)
                    .ThenBy(profile => profile.Representative.TotalGeneralizedCostPesos)
                    .ThenBy(profile => EstimateCandidateTimeSeconds(
                        profile.Representative))
                    .ThenBy(profile => GetAccessProfileRegionDiversityKey(
                        profile.Representative), StringComparer.Ordinal)
                    .Select(profile => profile.Representative)))
                .ToList();

            var added = 0;
            while (added < limit &&
                   selected.Count < MaxCandidatesToConfirm &&
                   modePairQueues.Any(queue => queue.Count > 0))
            {
                foreach (var queue in modePairQueues)
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

    private static string GetAccessModePairKey(JourneyCandidate candidate) =>
        $"{candidate.OriginAccess.Mode}>{candidate.DestinationAccess.Mode}";

    private static string GetAccessProfileDiversityKey(
        JourneyCandidate candidate) =>
        $"{string.Join('>', candidate.Legs.Select(leg => leg.RouteId))}|" +
        $"{GetAccessEndpointProfileKey(candidate.OriginAccess)}|" +
        GetAccessEndpointProfileKey(candidate.DestinationAccess);

    private string GetAccessProfileRegionDiversityKey(
        JourneyCandidate candidate) =>
        $"{GetAccessProfileDiversityKey(candidate)}|" +
        GetTransitOccurrenceDiversityKey(candidate);

    private string GetTransitOccurrenceDiversityKey(JourneyCandidate candidate)
    {
        var bucketSize = _options.BoardingDiversityBucketMeters;
        return string.Join('>', candidate.Legs.Select(leg =>
        {
            var boardBucket = (long)Math.Floor(
                GetBoardProgressMeters(leg) / bucketSize);
            var alightBucket = (long)Math.Floor(
                GetAlightProgressMeters(leg) / bucketSize);
            return $"{leg.RouteId}@{boardBucket}-{alightBucket}";
        }));
    }

    private static string GetAccessEndpointProfileKey(AccessCandidate access) =>
        access.Mode == AccessMode.Trike
            ? $"Trike:{access.TrikePoint?.Id ?? "unknown"}"
            : access.Mode.ToString();

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
