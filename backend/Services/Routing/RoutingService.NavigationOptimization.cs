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
        List<JourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences = null)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var keyedCandidates = candidates
            .Select(candidate => new KeyedJourneyCandidate(
                candidate,
                GetJourneyCandidateKey(candidate)))
            .ToList();
        return SelectCandidatesToConfirmWithDiversity(
            keyedCandidates,
            preferences);
    }

    internal List<JourneyCandidate>
        SelectCandidatesToConfirmWithDiversityD1Reference(
            List<JourneyCandidate> candidates,
            JourneyPlanningPreferences? preferences = null)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var keyedCandidates = candidates
            .Select(candidate => new KeyedJourneyCandidate(
                candidate,
                GetJourneyCandidateKey(candidate)))
            .ToList();
        return SelectCandidatesToConfirmWithDiversityD1(
            keyedCandidates,
            preferences);
    }

    private List<JourneyCandidate> SelectCandidatesToConfirmWithDiversity(
        List<KeyedJourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences)
    {
        return SelectCandidatesToConfirmWithDiversityD2(candidates, preferences);
    }

    private List<JourneyCandidate> SelectCandidatesToConfirmWithDiversityD2(
        List<KeyedJourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates.Select(candidate => candidate.Candidate).ToList();

        var hasSoftPreference = HasSoftPlanningPreference(preferences);
        var metadata = new List<CandidateSelectionMetadata>(candidates.Count);
        var physicalBucketStates = new Dictionary<
            string,
            PhysicalBucketSelectionState>(StringComparer.Ordinal);
        var journeyKeys = new HashSet<string>(StringComparer.Ordinal);
        var hasDuplicateJourneyKey = false;
        for (var ordinal = 0; ordinal < candidates.Count; ordinal++)
        {
            var item = BuildCandidateSelectionMetadata(
                candidates[ordinal],
                preferences,
                ordinal);
            metadata.Add(item);
            if (physicalBucketStates.TryGetValue(
                    item.BoardingDiversityKey,
                    out var physicalBucketState))
            {
                physicalBucketState.Add(item, hasSoftPreference);
            }
            else
            {
                physicalBucketStates.Add(
                    item.BoardingDiversityKey,
                    new PhysicalBucketSelectionState(item));
            }
            hasDuplicateJourneyKey |= !journeyKeys.Add(item.JourneyKey);
        }

        // The production caller supplies candidates after journey-key dedupe.
        // Preserve the D1 behavior for any test or future caller that violates
        // that invariant rather than relying on bounded-frontier capacities
        // that assume one occurrence of each journey key.
        if (hasDuplicateJourneyKey)
        {
            return SelectCandidatesToConfirmWithDiversityD1(
                candidates,
                preferences);
        }

        var selected = new List<JourneyCandidate>(MaxCandidatesToConfirm);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var physicalDiversityQuota = Math.Max(1, MaxCandidatesToConfirm / 4);
        var objectiveQuota = Math.Max(
            1,
            (MaxCandidatesToConfirm - physicalDiversityQuota) / 4);
        var planningComparer = Comparer<CandidateSelectionMetadata>.Create(
            (left, right) => ComparePlanningCandidates(
                left,
                right,
                hasSoftPreference));
        var physicalComparer = Comparer<CandidateSelectionMetadata>.Create(
            (left, right) => ComparePhysicalDiversityCandidates(
                left,
                right,
                hasSoftPreference));
        var fareComparer = Comparer<CandidateSelectionMetadata>.Create(
            CompareFareCandidates);
        var timeComparer = Comparer<CandidateSelectionMetadata>.Create(
            CompareTimeCandidates);

        var orderedPhysicalBuckets = physicalBucketStates
            .OrderBy(pair =>
                pair.Value.BestCandidate.Candidate.TotalGeneralizedCostPesos)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        var physicalSelectionsRemaining = physicalDiversityQuota;
        while (physicalSelectionsRemaining > 0)
        {
            var selectedInRound = false;
            foreach (var pair in orderedPhysicalBuckets)
            {
                if (pair.Value.RequiredCandidateCount >= pair.Value.CandidateCount)
                    continue;

                pair.Value.RequiredCandidateCount++;
                physicalSelectionsRemaining--;
                selectedInRound = true;
                if (physicalSelectionsRemaining == 0)
                    break;
            }

            if (!selectedInRound)
                break;
        }

        var physicalFrontiers = new Dictionary<
            string,
            BoundedFrontier<CandidateSelectionMetadata>>(
                Math.Min(physicalBucketStates.Count, physicalDiversityQuota),
                StringComparer.Ordinal);
        var planningFrontier = new BoundedFrontier<CandidateSelectionMetadata>(
            MaxCandidatesToConfirm,
            planningComparer);
        var fareFrontier = new BoundedFrontier<CandidateSelectionMetadata>(
            Math.Min(
                MaxCandidatesToConfirm,
                physicalDiversityQuota + 2 * objectiveQuota),
            fareComparer);
        var timeFrontier = new BoundedFrontier<CandidateSelectionMetadata>(
            Math.Min(
                MaxCandidatesToConfirm,
                physicalDiversityQuota + 3 * objectiveQuota),
            timeComparer);
        var accessProfiles = new Dictionary<
            AccessProfileSelectionKey,
            AccessProfileSelectionAccumulator>();

        // All ranking frontiers and access-profile representatives are built
        // in one traversal. The capacities include the maximum number of
        // candidates that earlier quota passes can place in the seen set.
        foreach (var item in metadata)
        {
            var requiredPhysicalCandidates = physicalBucketStates[
                item.BoardingDiversityKey].RequiredCandidateCount;
            if (requiredPhysicalCandidates > 0)
            {
                if (!physicalFrontiers.TryGetValue(
                        item.BoardingDiversityKey,
                        out var physicalFrontier))
                {
                    physicalFrontier =
                        new BoundedFrontier<CandidateSelectionMetadata>(
                            requiredPhysicalCandidates,
                            physicalComparer);
                    physicalFrontiers.Add(
                        item.BoardingDiversityKey,
                        physicalFrontier);
                }

                physicalFrontier.Add(item);
            }

            planningFrontier.Add(item);
            fareFrontier.Add(item);
            timeFrontier.Add(item);

            var accessProfileKey = new AccessProfileSelectionKey(
                item.AccessModePairKey,
                item.AccessProfileRegionDiversityKey);
            if (accessProfiles.TryGetValue(
                    accessProfileKey,
                    out var accessProfile))
            {
                accessProfile.Add(item);
            }
            else
            {
                accessProfiles.Add(
                    accessProfileKey,
                    new AccessProfileSelectionAccumulator(item));
            }
        }

        var planningCandidates = planningFrontier.ToSortedList();
        var fareCandidates = fareFrontier.ToSortedList();
        var timeCandidates = timeFrontier.ToSortedList();

        AddPhysicalDiversity(physicalDiversityQuota);
        Add(planningCandidates, objectiveQuota);
        Add(fareCandidates, objectiveQuota);
        Add(timeCandidates, objectiveQuota);
        AddAccessProfileDiversity(objectiveQuota);

        if (selected.Count < MaxCandidatesToConfirm)
        {
            Add(
                planningCandidates,
                MaxCandidatesToConfirm - selected.Count);
        }

        _logger.LogDebug(
            "Routing candidate diversity selected {SelectedCount} of {CandidateCount} candidates for confirmation",
            selected.Count,
            candidates.Count);

        return selected;

        void Add(IEnumerable<CandidateSelectionMetadata> source, int limit)
        {
            var added = 0;
            foreach (var item in source)
            {
                if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                    break;

                if (!seen.Add(item.JourneyKey))
                    continue;

                selected.Add(item.Candidate);
                added++;
            }
        }

        void AddPhysicalDiversity(int limit)
        {
            var queues = physicalFrontiers
                .Select(pair => (
                    Key: pair.Key,
                    Queue: new Queue<CandidateSelectionMetadata>(
                        pair.Value.ToSortedList())))
                .OrderBy(pair =>
                    pair.Queue.Peek().Candidate.TotalGeneralizedCostPesos)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToList();

            var added = 0;
            while (added < limit &&
                   selected.Count < MaxCandidatesToConfirm &&
                   queues.Any(pair => pair.Queue.Count > 0))
            {
                foreach (var pair in queues)
                {
                    if (pair.Queue.Count == 0)
                        continue;

                    var item = pair.Queue.Dequeue();
                    seen.Add(item.JourneyKey);
                    selected.Add(item.Candidate);
                    added++;

                    if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                        break;
                }
            }
        }

        void AddAccessProfileDiversity(int limit)
        {
            if (limit <= 0 || selected.Count >= MaxCandidatesToConfirm)
                return;

            var profileComparer = Comparer<AccessProfileSelectionSummary>.Create(
                CompareAccessProfileSummaries);
            var modePairFrontiers = new Dictionary<
                string,
                BoundedFrontier<AccessProfileSelectionSummary>>(
                    StringComparer.Ordinal);

            foreach (var (key, accumulator) in accessProfiles)
            {
                var summary = accumulator.ToSummary(key);
                if (seen.Contains(summary.Representative.JourneyKey))
                    continue;

                if (!modePairFrontiers.TryGetValue(
                        key.AccessModePairKey,
                        out var frontier))
                {
                    frontier = new BoundedFrontier<AccessProfileSelectionSummary>(
                        limit,
                        profileComparer);
                    modePairFrontiers.Add(key.AccessModePairKey, frontier);
                }

                frontier.Add(summary);
            }

            var queues = modePairFrontiers
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new Queue<AccessProfileSelectionSummary>(
                    pair.Value.ToSortedList()))
                .ToList();

            var added = 0;
            while (added < limit &&
                   selected.Count < MaxCandidatesToConfirm &&
                   queues.Any(queue => queue.Count > 0))
            {
                foreach (var queue in queues)
                {
                    if (queue.Count == 0)
                        continue;

                    var item = queue.Dequeue().Representative;
                    seen.Add(item.JourneyKey);
                    selected.Add(item.Candidate);
                    added++;

                    if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                        break;
                }
            }
        }
    }

    private List<JourneyCandidate> SelectCandidatesToConfirmWithDiversityD1(
        List<KeyedJourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates.Select(candidate => candidate.Candidate).ToList();

        var metadata = candidates
            .Select((candidate, ordinal) => BuildCandidateSelectionMetadata(
                candidate,
                preferences,
                ordinal))
            .ToList();
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
        AddDiverse(physicalDiversityQuota);
        Add(OrderByPlanningPreference(metadata, preferences), objectiveQuota);
        Add(metadata.OrderBy(candidate => candidate.FarePesos)
            .ThenBy(candidate => candidate.Candidate.TotalGeneralizedCostPesos),
            objectiveQuota);
        Add(metadata.OrderBy(candidate => candidate.TimeSeconds)
            .ThenBy(candidate => candidate.Candidate.TotalGeneralizedCostPesos),
            objectiveQuota);
        AddAccessProfileDiverse(objectiveQuota);

        if (selected.Count < MaxCandidatesToConfirm)
        {
            Add(
                OrderByPlanningPreference(metadata, preferences),
                MaxCandidatesToConfirm - selected.Count);
        }

        _logger.LogDebug(
            "Routing candidate diversity selected {SelectedCount} of {CandidateCount} candidates for confirmation",
            selected.Count,
            candidates.Count);

        return selected;

        void Add(IEnumerable<CandidateSelectionMetadata> source, int limit)
        {
            var added = 0;
            foreach (var item in source)
            {
                if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                    break;

                if (!seen.Add(item.JourneyKey))
                    continue;

                selected.Add(item.Candidate);
                added++;
            }
        }

        void AddDiverse(int limit)
        {
            var buckets = metadata
                .GroupBy(candidate => candidate.BoardingDiversityKey, StringComparer.Ordinal)
                .Select(group => new Queue<CandidateSelectionMetadata>(
                    OrderByPlanningPreference(group, preferences)
                        .ThenBy(candidate => candidate.TimeSeconds)
                        .ThenBy(candidate => candidate.JourneyKey, StringComparer.Ordinal)))
                .OrderBy(queue => queue.Peek().Candidate.TotalGeneralizedCostPesos)
                .ThenBy(
                    queue => queue.Peek().BoardingDiversityKey,
                    StringComparer.Ordinal)
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
                        var item = queue.Dequeue();
                        if (!seen.Add(item.JourneyKey))
                            continue;

                        selected.Add(item.Candidate);
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
            var modePairQueues = metadata
                .GroupBy(candidate => candidate.AccessModePairKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new Queue<CandidateSelectionMetadata>(group
                    .GroupBy(
                        candidate => candidate.AccessProfileRegionDiversityKey,
                        StringComparer.Ordinal)
                    .Select(profile => new
                    {
                        Representative = profile
                            .OrderBy(candidate => candidate.OriginAccessDistanceMeters)
                            .ThenBy(candidate =>
                                candidate.Candidate.OriginAccess.TotalTimeSeconds)
                            .ThenBy(candidate =>
                                candidate.Candidate.TotalGeneralizedCostPesos)
                            .ThenBy(candidate => candidate.TimeSeconds)
                            .ThenBy(candidate => candidate.JourneyKey, StringComparer.Ordinal)
                            .First(),
                        ProvisionalProfileCost = profile.Min(candidate =>
                            candidate.Candidate.TotalGeneralizedCostPesos)
                    })
                    .OrderBy(profile => profile.ProvisionalProfileCost)
                    .ThenBy(profile =>
                        profile.Representative.Candidate.TotalGeneralizedCostPesos)
                    .ThenBy(profile => profile.Representative.TimeSeconds)
                    .ThenBy(
                        profile => profile.Representative
                            .AccessProfileRegionDiversityKey,
                        StringComparer.Ordinal)
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
                        var item = queue.Dequeue();
                        if (!seen.Add(item.JourneyKey))
                            continue;

                        selected.Add(item.Candidate);
                        added++;
                        break;
                    }

                    if (added >= limit || selected.Count >= MaxCandidatesToConfirm)
                        break;
                }
            }
        }
    }

    // Test-only parity oracle retained from the pre-D1 selector. Production
    // planning uses the metadata-cached overload above.
    internal List<JourneyCandidate>
        SelectCandidatesToConfirmWithDiversityReference(
            List<JourneyCandidate> candidates,
            JourneyPlanningPreferences? preferences = null)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var selected = new List<JourneyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var physicalDiversityQuota = Math.Max(1, MaxCandidatesToConfirm / 4);
        var objectiveQuota = Math.Max(
            1,
            (MaxCandidatesToConfirm - physicalDiversityQuota) / 4);

        AddDiverse(physicalDiversityQuota, GetBoardingDiversityKey);
        Add(OrderByPlanningPreference(candidates, preferences), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        Add(candidates.OrderBy(EstimateCandidateTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos), objectiveQuota);
        AddAccessProfileDiverse(objectiveQuota);

        if (selected.Count < MaxCandidatesToConfirm)
        {
            Add(
                OrderByPlanningPreference(candidates, preferences),
                MaxCandidatesToConfirm - selected.Count);
        }

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
                .Select(group => new Queue<JourneyCandidate>(
                    OrderByPlanningPreference(group, preferences)
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

    private IOrderedEnumerable<CandidateSelectionMetadata>
        OrderByPlanningPreference(
            IEnumerable<CandidateSelectionMetadata> candidates,
            JourneyPlanningPreferences? preferences)
    {
        if (!HasSoftPlanningPreference(preferences))
        {
            return candidates.OrderBy(candidate =>
                candidate.Candidate.TotalGeneralizedCostPesos);
        }

        return candidates
            .OrderBy(candidate => candidate.PlanningScore)
            .ThenBy(candidate => candidate.Candidate.TotalGeneralizedCostPesos)
            .ThenBy(candidate => candidate.JourneyKey, StringComparer.Ordinal);
    }

    private CandidateSelectionMetadata BuildCandidateSelectionMetadata(
        KeyedJourneyCandidate keyedCandidate,
        JourneyPlanningPreferences? preferences,
        int originalOrdinal)
    {
        var candidate = keyedCandidate.Candidate;
        var diversity = BuildCandidateDiversityMetadata(candidate);
        var farePesos = EstimateCandidateFarePesos(candidate);
        var timeSeconds = EstimateCandidateTimeSeconds(candidate);
        var planningScore = HasSoftPlanningPreference(preferences)
            ? PlanningCandidateScoreFromEstimates(
                candidate.TotalGeneralizedCostPesos,
                preferences,
                timeSeconds,
                farePesos,
                EstimateCandidateWalkingMeters(candidate))
            : candidate.TotalGeneralizedCostPesos;

        return new CandidateSelectionMetadata(
            candidate,
            keyedCandidate.JourneyKey,
            diversity.BoardingKey,
            GetAccessModePairKey(candidate),
            diversity.AccessProfileRegionKey,
            planningScore,
            farePesos,
            timeSeconds,
            EstimateCandidateOriginAccessDistanceMeters(candidate),
            originalOrdinal);
    }

    private CandidateDiversityMetadata BuildCandidateDiversityMetadata(
        JourneyCandidate candidate)
    {
        var bucketSize = _options.BoardingDiversityBucketMeters;
        var boardingParts = new string[candidate.Legs.Count];
        var occurrenceParts = new string[candidate.Legs.Count];

        for (var index = 0; index < candidate.Legs.Count; index++)
        {
            var leg = candidate.Legs[index];
            var boardBucket = (long)Math.Floor(
                GetBoardProgressMeters(leg) / bucketSize);
            var alightBucket = (long)Math.Floor(
                GetAlightProgressMeters(leg) / bucketSize);
            boardingParts[index] = $"{leg.RouteId}@{boardBucket}";
            occurrenceParts[index] =
                $"{leg.RouteId}@{boardBucket}-{alightBucket}";
        }

        return new CandidateDiversityMetadata(
            string.Join('>', boardingParts),
            $"{GetAccessProfileDiversityKey(candidate)}|" +
            string.Join('>', occurrenceParts));
    }

    private double PlanningCandidateScoreFromEstimates(
        double generalizedCostPesos,
        JourneyPlanningPreferences? preferences,
        double timeSeconds,
        double farePesos,
        double walkingMeters)
    {
        if (preferences is null)
            return generalizedCostPesos;

        var score = preferences.OptimizationPreference switch
        {
            JourneyOptimizationPreference.Fastest =>
                timeSeconds + generalizedCostPesos / 100,
            JourneyOptimizationPreference.Cheapest =>
                farePesos * 1_000 + generalizedCostPesos,
            _ => generalizedCostPesos
        };

        return preferences.WalkingPreference switch
        {
            JourneyWalkingPreference.Less => score + walkingMeters / 100,
            JourneyWalkingPreference.More => score -
                WalkingFatiguePesosPerKilometer * walkingMeters / 2_000,
            _ => score
        };
    }

    private IOrderedEnumerable<JourneyCandidate> OrderByPlanningPreference(
        IEnumerable<JourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences)
    {
        // Preserve the pre-preference stable ordering exactly for ordinary
        // requests and hard-constraint-only requests. Adding even a harmless
        // deterministic key here can change which equal-cost candidate uses a
        // bounded confirmation slot.
        if (!HasSoftPlanningPreference(preferences))
        {
            return candidates.OrderBy(candidate =>
                candidate.TotalGeneralizedCostPesos);
        }

        return candidates
            .OrderBy(candidate => PlanningCandidateScore(candidate, preferences))
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .ThenBy(GetJourneyCandidateKey, StringComparer.Ordinal);
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

    private static int ComparePlanningCandidateKeys(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right,
        bool hasSoftPreference)
    {
        if (hasSoftPreference)
        {
            var result = Comparer<double>.Default.Compare(
                left.PlanningScore,
                right.PlanningScore);
            if (result != 0)
                return result;

            result = Comparer<double>.Default.Compare(
                left.Candidate.TotalGeneralizedCostPesos,
                right.Candidate.TotalGeneralizedCostPesos);
            if (result != 0)
                return result;

            result = StringComparer.Ordinal.Compare(
                left.JourneyKey,
                right.JourneyKey);
            if (result != 0)
                return result;

            return 0;
        }

        return Comparer<double>.Default.Compare(
            left.Candidate.TotalGeneralizedCostPesos,
            right.Candidate.TotalGeneralizedCostPesos);
    }

    private static int ComparePlanningCandidates(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right,
        bool hasSoftPreference)
    {
        var result = ComparePlanningCandidateKeys(
            left,
            right,
            hasSoftPreference);
        return result != 0
            ? result
            : left.OriginalOrdinal.CompareTo(right.OriginalOrdinal);
    }

    private static int ComparePhysicalDiversityCandidates(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right,
        bool hasSoftPreference)
    {
        var result = ComparePlanningCandidateKeys(
            left,
            right,
            hasSoftPreference);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.TimeSeconds,
            right.TimeSeconds);
        if (result != 0)
            return result;

        result = StringComparer.Ordinal.Compare(left.JourneyKey, right.JourneyKey);
        return result != 0
            ? result
            : left.OriginalOrdinal.CompareTo(right.OriginalOrdinal);
    }

    private static int CompareFareCandidates(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right)
    {
        var result = Comparer<double>.Default.Compare(
            left.FarePesos,
            right.FarePesos);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Candidate.TotalGeneralizedCostPesos,
            right.Candidate.TotalGeneralizedCostPesos);
        return result != 0
            ? result
            : left.OriginalOrdinal.CompareTo(right.OriginalOrdinal);
    }

    private static int CompareTimeCandidates(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right)
    {
        var result = Comparer<double>.Default.Compare(
            left.TimeSeconds,
            right.TimeSeconds);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Candidate.TotalGeneralizedCostPesos,
            right.Candidate.TotalGeneralizedCostPesos);
        return result != 0
            ? result
            : left.OriginalOrdinal.CompareTo(right.OriginalOrdinal);
    }

    private static int CompareAccessProfileRepresentatives(
        CandidateSelectionMetadata left,
        CandidateSelectionMetadata right)
    {
        var result = Comparer<double>.Default.Compare(
            left.OriginAccessDistanceMeters,
            right.OriginAccessDistanceMeters);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Candidate.OriginAccess.TotalTimeSeconds,
            right.Candidate.OriginAccess.TotalTimeSeconds);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Candidate.TotalGeneralizedCostPesos,
            right.Candidate.TotalGeneralizedCostPesos);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.TimeSeconds,
            right.TimeSeconds);
        if (result != 0)
            return result;

        result = StringComparer.Ordinal.Compare(left.JourneyKey, right.JourneyKey);
        return result != 0
            ? result
            : left.OriginalOrdinal.CompareTo(right.OriginalOrdinal);
    }

    private static int CompareAccessProfileSummaries(
        AccessProfileSelectionSummary left,
        AccessProfileSelectionSummary right)
    {
        var result = Comparer<double>.Default.Compare(
            left.ProvisionalProfileCost,
            right.ProvisionalProfileCost);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Representative.Candidate.TotalGeneralizedCostPesos,
            right.Representative.Candidate.TotalGeneralizedCostPesos);
        if (result != 0)
            return result;

        result = Comparer<double>.Default.Compare(
            left.Representative.TimeSeconds,
            right.Representative.TimeSeconds);
        if (result != 0)
            return result;

        result = StringComparer.Ordinal.Compare(
            left.AccessProfileRegionDiversityKey,
            right.AccessProfileRegionDiversityKey);
        return result != 0
            ? result
            : left.FirstOrdinal.CompareTo(right.FirstOrdinal);
    }

    private sealed class AccessProfileSelectionAccumulator
    {
        private CandidateSelectionMetadata _representative;
        private double _provisionalProfileCost;

        public AccessProfileSelectionAccumulator(
            CandidateSelectionMetadata candidate)
        {
            _representative = candidate;
            _provisionalProfileCost =
                candidate.Candidate.TotalGeneralizedCostPesos;
            FirstOrdinal = candidate.OriginalOrdinal;
        }

        public int FirstOrdinal { get; }

        public void Add(CandidateSelectionMetadata candidate)
        {
            if (CompareAccessProfileRepresentatives(
                    candidate,
                    _representative) < 0)
            {
                _representative = candidate;
            }

            var provisionalCost = candidate.Candidate.TotalGeneralizedCostPesos;
            if (double.IsNaN(provisionalCost) ||
                provisionalCost < _provisionalProfileCost)
            {
                _provisionalProfileCost = provisionalCost;
            }
        }

        public AccessProfileSelectionSummary ToSummary(
            AccessProfileSelectionKey key) =>
            new(
                _representative,
                _provisionalProfileCost,
                key.AccessProfileRegionDiversityKey,
                FirstOrdinal);
    }

    private sealed class PhysicalBucketSelectionState
    {
        public PhysicalBucketSelectionState(
            CandidateSelectionMetadata firstCandidate)
        {
            BestCandidate = firstCandidate;
            CandidateCount = 1;
        }

        public CandidateSelectionMetadata BestCandidate { get; private set; }
        public int CandidateCount { get; private set; }
        public int RequiredCandidateCount { get; set; }

        public void Add(
            CandidateSelectionMetadata candidate,
            bool hasSoftPreference)
        {
            CandidateCount++;
            if (ComparePhysicalDiversityCandidates(
                    candidate,
                    BestCandidate,
                    hasSoftPreference) < 0)
            {
                BestCandidate = candidate;
            }
        }
    }

    private sealed class BoundedFrontier<T>
    {
        private readonly int _capacity;
        private readonly IComparer<T> _comparer;
        private readonly PriorityQueue<T, T> _queue;

        public BoundedFrontier(int capacity, IComparer<T> comparer)
        {
            _capacity = Math.Max(1, capacity);
            _comparer = comparer;
            _queue = new PriorityQueue<T, T>(
                new ReverseComparer<T>(comparer));
        }

        public void Add(T item)
        {
            if (_queue.Count < _capacity)
            {
                _queue.Enqueue(item, item);
                return;
            }

            if (_comparer.Compare(item, _queue.Peek()) >= 0)
                return;

            _queue.Dequeue();
            _queue.Enqueue(item, item);
        }

        public List<T> ToSortedList()
        {
            var result = _queue.UnorderedItems
                .Select(item => item.Element)
                .ToList();
            result.Sort(_comparer);
            return result;
        }
    }

    private sealed class ReverseComparer<T>(IComparer<T> inner) : IComparer<T>
    {
        public int Compare(T? left, T? right) => inner.Compare(right!, left!);
    }

    private sealed record KeyedJourneyCandidate(
        JourneyCandidate Candidate,
        string JourneyKey);

    private readonly record struct AccessProfileSelectionKey(
        string AccessModePairKey,
        string AccessProfileRegionDiversityKey);

    private sealed record AccessProfileSelectionSummary(
        CandidateSelectionMetadata Representative,
        double ProvisionalProfileCost,
        string AccessProfileRegionDiversityKey,
        int FirstOrdinal);

    private sealed record CandidateSelectionMetadata(
        JourneyCandidate Candidate,
        string JourneyKey,
        string BoardingDiversityKey,
        string AccessModePairKey,
        string AccessProfileRegionDiversityKey,
        double PlanningScore,
        double FarePesos,
        double TimeSeconds,
        double OriginAccessDistanceMeters,
        int OriginalOrdinal);

    private sealed record CandidateDiversityMetadata(
        string BoardingKey,
        string AccessProfileRegionKey);

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
