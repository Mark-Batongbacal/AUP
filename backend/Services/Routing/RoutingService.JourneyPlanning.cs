using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    public async Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        using var routingMeasurement = _telemetry.Measure("RoutePlanning");
        var areaValidation = _tripAreaValidator.ValidateTrip(
            originLatitude, originLongitude,
            destinationLatitude, destinationLongitude);
        if (!areaValidation.IsValid)
        {
            throw new RoutingValidationException(
                areaValidation.ErrorCode!, areaValidation.Message!);
        }

        await EnsureInitializedAsync(cancellationToken);

        var boardAccessPrefixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        var alightAccessSuffixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        foreach (var (routeId, samples) in _routeSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var boardOptions =
                ComputeBoardAccessOptions(
                    routeId,
                    samples,
                    originLatitude,
                    originLongitude);

            boardAccessPrefixByRoute[routeId] =
                ComputePrefixMinAccess(boardOptions);

            var alightOptions =
                ComputeAlightAccessOptions(
                    routeId,
                    samples,
                    destinationLatitude,
                    destinationLongitude);

            alightAccessSuffixByRoute[routeId] =
                ComputeSuffixMinAccess(alightOptions);
        }

        var candidates = new List<JourneyCandidate>();

        // 0 transfers. Keep several boarding variants for each route instead
        // of collapsing to FirstOrDefault before Valhalla can confirm access.
        foreach (var route in _routes)
        {
            if (!_routeSamples.ContainsKey(route.RouteId))
                continue;

            foreach (var direct in FindBestConnections(
                         route,
                         originLatitude,
                         originLongitude,
                         destinationLatitude,
                         destinationLongitude))
            {
                var legs = new List<JourneyLegCandidate>
                {
                    new(
                        direct.RouteId,
                        direct.RouteName,
                        direct.BoardAccess.Anchor,
                        direct.AlightAccess.Anchor,
                        direct.BoardIndex,
                        direct.AlightIndex,
                        direct.BoardAccess.FullRouteAnchor,
                        direct.AlightAccess.FullRouteAnchor)
                };

                if (!HasForwardRouteProgress(legs[0]))
                    continue;

                candidates.Add(new JourneyCandidate(
                    legs,
                    direct.BoardAccess,
                    direct.AlightAccess,
                    [],
                    direct.BoardAccess.GeneralizedCostPesos +
                    direct.AlightAccess.GeneralizedCostPesos +
                    GeneralizedCostFromTimeAndFare(
                        EstimateJeepneyTravelTimeSeconds(legs),
                        JeepneyBaseFarePesos)));
            }
        }

        candidates.AddRange(FindTransferCandidates(
            boardAccessPrefixByRoute,
            alightAccessSuffixByRoute,
            cancellationToken));

        // Access generation stores walking and tricycle choices together on
        // one route anchor. Expand them before ranking; otherwise confirmation
        // accepts only the first valid choice and useful multimodal variants
        // (for example trike -> jeepney -> trike) disappear.
        var expandedCandidates = candidates
            .SelectMany(ExpandAccessAlternatives)
            // Full-route progress is authoritative. This catches wrong-way or
            // zero-progress legs even when sparse sample indices look valid.
            .Where(candidate => candidate.Legs.All(HasForwardRouteProgress))
            .ToList();

        var distinctCandidates = expandedCandidates
            .GroupBy(GetJourneyCandidateKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                .First())
            .ToList();

        // Phase 2 reserves part of the confirmation budget for distinct route
        // and boarding regions so a dense cluster of similar candidates cannot
        // crowd out useful alternatives before authoritative validation.
        var ranked = SelectCandidatesToConfirmWithDiversity(distinctCandidates);

        // Preserve the originating candidate while Valhalla confirms access.
        // That lets post-confirmation pruning use both authoritative network
        // access metrics and exact full-route board/alight progress.
        var confirmationTasks = ranked.Select(async candidate =>
        {
            var plans = await ConfirmJourneyCandidatesAsync(
                [candidate],
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken);

            return plans.FirstOrDefault() is { } plan
                ? new ConfirmedJourneyCandidate(candidate, plan)
                : null;
        });

        var confirmedWithSource = (await Task.WhenAll(confirmationTasks))
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();

        var originPruned = PruneConfirmedFeederShadowing(confirmedWithSource);
        LogConfirmedPruningDelta(
            confirmedWithSource,
            originPruned,
            "origin feeder shadowing");

        var transferPruned = PruneConfirmedTransferBoardingShadowing(originPruned);
        LogConfirmedPruningDelta(
            originPruned,
            transferPruned,
            "transfer feeder shadowing");

        var paretoPruned = PruneDominatedConfirmedCandidates(transferPruned);
        var confirmed = paretoPruned
            .Select(result => result.Plan)
            .ToList();

        var directPlans =
            await ConfirmDirectTripCandidatesAsync(
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken);

        var distinctPlans = confirmed
            .Concat(directPlans)
            .Where(plan => ValidatePlanContinuity(
                plan,
                requireGeometry: false,
                stage: "post-confirmation"))
            .GroupBy(GetPlanKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(plan => plan.GeneralizedCostPesos)
                .ThenBy(plan => plan.TotalTimeSeconds)
                .First())
            .ToList();

        var selectedPlans = SelectObjectivePlans(distinctPlans);
        LogSelectedPlanDiagnostics(selectedPlans);
        _telemetry.Event(selectedPlans.Count == 0 ? "NoRouteFound" : "TripPlanned",
            outcome: selectedPlans.Count.ToString());
        return selectedPlans;
    }

    private static IEnumerable<JourneyCandidate> ExpandAccessAlternatives(
        JourneyCandidate candidate)
    {
        foreach (var origin in candidate.OriginAccess.AllAlternatives)
        foreach (var destination in candidate.DestinationAccess.AllAlternatives)
        {
            var standaloneOrigin = origin with { Alternatives = null };
            var standaloneDestination = destination with { Alternatives = null };
            var adjustedCost = candidate.TotalGeneralizedCostPesos
                - candidate.OriginAccess.GeneralizedCostPesos
                - candidate.DestinationAccess.GeneralizedCostPesos
                + standaloneOrigin.GeneralizedCostPesos
                + standaloneDestination.GeneralizedCostPesos;

            yield return candidate with
            {
                OriginAccess = standaloneOrigin,
                DestinationAccess = standaloneDestination,
                ProvisionalJourneyCostPesos = adjustedCost
            };
        }
    }

    private bool HasForwardRouteProgress(JourneyLegCandidate leg)
    {
        if (!_routeSamples.TryGetValue(leg.RouteId, out var samples))
            return false;

        var boardIndex = leg.BoardIndex ?? GetNearestSampleIndex(samples, leg.Board);
        var alightIndex = leg.AlightIndex ?? GetNearestSampleIndex(samples, leg.Alight);
        var boardAnchor = leg.BoardFullRouteAnchor ??
            GetRouteAnchor(leg.RouteId, boardIndex, leg.Board);
        var alightAnchor = leg.AlightFullRouteAnchor ??
            GetRouteAnchor(leg.RouteId, alightIndex, leg.Alight);

        return alightAnchor.DistanceFromRouteStartMeters >
               boardAnchor.DistanceFromRouteStartMeters;
    }

    private List<JourneyCandidate> SelectCandidatesToConfirm(
        List<JourneyCandidate> candidates)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var selected = new List<JourneyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var perObjective = Math.Max(1, MaxCandidatesToConfirm / 4);

        Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos));
        Add(candidates.OrderBy(EstimateCandidateFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos));
        Add(candidates.OrderBy(EstimateCandidateTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos));
        // Keep low-burden boarding candidates in the confirmation pool so an
        // apparently cheap downstream board cannot crowd them out beforehand.
        Add(candidates.OrderBy(EstimateCandidateOriginAccessDistanceMeters)
            .ThenBy(candidate => candidate.OriginAccess.TotalTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos));

        if (selected.Count < MaxCandidatesToConfirm)
            Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos),
                MaxCandidatesToConfirm - selected.Count);

        return selected;

        void Add(IEnumerable<JourneyCandidate> source, int? limit = null)
        {
            var added = 0;
            foreach (var candidate in source)
            {
                if (added >= (limit ?? perObjective) ||
                    selected.Count >= MaxCandidatesToConfirm)
                    break;
                if (!seen.Add(GetJourneyCandidateKey(candidate)))
                    continue;
                selected.Add(candidate);
                added++;
            }
        }
    }

    private double EstimateCandidateTimeSeconds(JourneyCandidate candidate) =>
        candidate.OriginAccess.TotalTimeSeconds +
        candidate.DestinationAccess.TotalTimeSeconds +
        candidate.TransferWalkSegments.Sum(segment =>
            segment.StraightLineMeters / WalkingSpeedMetersPerSecond) +
        EstimateJeepneyTravelTimeSeconds(candidate.Legs);

    private double EstimateCandidateFarePesos(JourneyCandidate candidate) =>
        candidate.OriginAccess.FarePesos +
        candidate.DestinationAccess.FarePesos +
        candidate.Legs.Count * JeepneyBaseFarePesos;

    private static double EstimateCandidateOriginAccessDistanceMeters(
        JourneyCandidate candidate) =>
        candidate.OriginAccess.WalkDistanceMeters +
        (candidate.OriginAccess.TrikeRideDistanceMeters ?? 0);

    /// <summary>
    /// Removes a confirmed boarding variant only when it looks like the feeder
    /// is physically chasing the same jeepney downstream and the farther board
    /// does not provide a meaningful fastest/cheapest advantage. Comparison is
    /// performed after Valhalla confirmation, so walls, divided roads, and road
    /// network detours are reflected in the access metrics.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneConfirmedFeederShadowing(
        List<ConfirmedJourneyCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        var kept = new List<ConfirmedJourneyCandidate>();

        foreach (var group in candidates.GroupBy(
                     GetConfirmedBoardingComparisonKey,
                     StringComparer.Ordinal))
        {
            var variants = group.ToList();
            if (variants.Count <= 1)
            {
                kept.AddRange(variants);
                continue;
            }

            var reference = variants
                .OrderBy(ConfirmedOriginAccessDistanceMeters)
                .ThenBy(candidate => candidate.Plan.OriginAccess.TotalTimeSeconds)
                .ThenBy(candidate => candidate.Plan.GeneralizedCostPesos)
                .First();

            var referenceBoardProgress = GetBoardProgressMeters(
                reference.Candidate.Legs[0]);
            var referenceAccessDistance = ConfirmedOriginAccessDistanceMeters(reference);

            foreach (var variant in variants)
            {
                if (ReferenceEquals(variant, reference))
                {
                    kept.Add(variant);
                    continue;
                }

                var downstreamProgress =
                    GetBoardProgressMeters(variant.Candidate.Legs[0]) -
                    referenceBoardProgress;
                if (downstreamProgress < FeederShadowingMinProgressMeters)
                {
                    kept.Add(variant);
                    continue;
                }

                var extraAccessDistance =
                    ConfirmedOriginAccessDistanceMeters(variant) -
                    referenceAccessDistance;
                var followsSameCorridor =
                    extraAccessDistance > 0 &&
                    extraAccessDistance >=
                    downstreamProgress * FeederShadowingAccessDistanceRatio;

                if (!followsSameCorridor ||
                    HasMeaningfulFastestOrCheapestAdvantage(variant.Plan, reference.Plan))
                {
                    kept.Add(variant);
                }
            }
        }

        return kept;
    }

    private string GetConfirmedBoardingComparisonKey(
        ConfirmedJourneyCandidate candidate)
    {
        var bucketSize = Math.Max(1, DefaultSampleIntervalMeters * 2);
        var downstreamSignature = string.Join('>', candidate.Candidate.Legs.Select(leg =>
        {
            var alightProgress = GetAlightProgressMeters(leg);
            var bucket = (long)Math.Floor(alightProgress / bucketSize);
            return $"{leg.RouteId}@{bucket}";
        }));

        return string.Join('|',
            downstreamSignature,
            $"origin:{candidate.Plan.OriginAccess.Mode}:{candidate.Plan.OriginAccess.TrikePointId}",
            $"destination:{candidate.Plan.DestinationAccess.Mode}:{candidate.Plan.DestinationAccess.TrikePointId}");
    }

    private double GetBoardProgressMeters(JourneyLegCandidate leg)
    {
        if (leg.BoardFullRouteAnchor is { } anchor)
            return anchor.DistanceFromRouteStartMeters;

        var samples = _routeSamples[leg.RouteId];
        var index = leg.BoardIndex ?? GetNearestSampleIndex(samples, leg.Board);
        return GetRouteAnchor(leg.RouteId, index, leg.Board)
            .DistanceFromRouteStartMeters;
    }

    private double GetAlightProgressMeters(JourneyLegCandidate leg)
    {
        if (leg.AlightFullRouteAnchor is { } anchor)
            return anchor.DistanceFromRouteStartMeters;

        var samples = _routeSamples[leg.RouteId];
        var index = leg.AlightIndex ?? GetNearestSampleIndex(samples, leg.Alight);
        return GetRouteAnchor(leg.RouteId, index, leg.Alight)
            .DistanceFromRouteStartMeters;
    }

    private static double ConfirmedOriginAccessDistanceMeters(
        ConfirmedJourneyCandidate candidate) =>
        candidate.Plan.OriginAccess.WalkDistanceMeters +
        (candidate.Plan.OriginAccess.TrikeRideDistanceMeters ?? 0);

    private bool HasMeaningfulFastestOrCheapestAdvantage(
        JeepneyTripPlan candidate,
        JeepneyTripPlan reference)
    {
        var timeSavings = reference.TotalTimeSeconds - candidate.TotalTimeSeconds;
        var fareSavings = reference.TotalFarePesos - candidate.TotalFarePesos;

        return timeSavings >= FeederShadowingRequiredTimeSavingsSeconds ||
               fareSavings >= FeederShadowingRequiredFareSavingsPesos;
    }

    private List<JeepneyTripPlan> SelectObjectivePlans(
        List<JeepneyTripPlan> plans)
    {
        if (plans.Count == 0)
            return [];

        // Fastest and cheapest remain pure objectives. The feeder-shadowing
        // guard above only removes a farther board when its advantage is too
        // small to justify chasing the same jeepney downstream.
        var cheapest = plans
            .OrderBy(plan => plan.TotalFarePesos)
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .ThenBy(plan => plan.TotalTimeSeconds)
            .First();

        var fastest = plans
            .OrderBy(plan => plan.TotalTimeSeconds)
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .ThenBy(plan => plan.TotalFarePesos)
            .First();

        // Efficient balances total time, fare, and access burden. Access burden
        // intentionally includes tricycle and transfer time instead of looking
        // only at walking, so a long feeder leg cannot appear "comfortable"
        // merely because it contains little walking.
        var minTime = plans.Min(plan => plan.TotalTimeSeconds);
        var maxTime = plans.Max(plan => plan.TotalTimeSeconds);
        var minFare = plans.Min(plan => plan.TotalFarePesos);
        var maxFare = plans.Max(plan => plan.TotalFarePesos);
        var accessBurden = plans.ToDictionary(
            plan => plan,
            plan => plan.Legs
                .Where(leg => leg.Mode != AccessMode.Jeepney)
                .Sum(leg => leg.DurationSeconds));
        var minAccessBurden = accessBurden.Values.Min();
        var maxAccessBurden = accessBurden.Values.Max();

        static double Normalize(double value, double min, double max) =>
            max <= min ? 0 : (value - min) / (max - min);

        var efficient = plans
            .OrderBy(plan =>
                Normalize(plan.TotalTimeSeconds, minTime, maxTime) +
                Normalize(plan.TotalFarePesos, minFare, maxFare) +
                Normalize(accessBurden[plan], minAccessBurden, maxAccessBurden))
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .First();

        var selected = new List<JeepneyTripPlan>();
        AddObjective(efficient, "efficient");
        AddObjective(cheapest, "cheapest");
        AddObjective(fastest, "fastest");

        foreach (var alternative in plans
                     .Except(selected)
                     .OrderBy(plan => plan.GeneralizedCostPesos)
                     .ThenBy(plan => plan.TotalTimeSeconds))
        {
            if (selected.Count >= MaxTripOptions)
                break;
            selected.Add(alternative);
        }

        return selected;

        void AddObjective(JeepneyTripPlan plan, string objective)
        {
            if (selected.Contains(plan))
            {
                plan.RecommendationType += $",{objective}";
                return;
            }

            plan.RecommendationType = objective;
            selected.Add(plan);
        }
    }

    private static string GetJourneyCandidateKey(JourneyCandidate candidate) =>
        string.Join('|', candidate.Legs.Select(leg => string.Join(':',
            leg.RouteId,
            leg.BoardIndex ?? -1,
            leg.AlightIndex ?? -1,
            Math.Round(leg.Board.Latitude, 6),
            Math.Round(leg.Board.Longitude, 6),
            Math.Round(leg.Alight.Latitude, 6),
            Math.Round(leg.Alight.Longitude, 6)))) +
        $"|{candidate.OriginAccess.Mode}:{candidate.OriginAccess.TrikePoint?.Id}" +
        $"|{candidate.DestinationAccess.Mode}:{candidate.DestinationAccess.TrikePoint?.Id}";

    private static string GetPlanKey(JeepneyTripPlan plan) =>
        string.Join('|', plan.Legs.Select(leg => string.Join(':',
            leg.Mode,
            leg.RouteId ?? string.Empty,
            Math.Round(leg.OriginLatitude, 6),
            Math.Round(leg.OriginLongitude, 6),
            Math.Round(leg.DestinationLatitude, 6),
            Math.Round(leg.DestinationLongitude, 6),
            leg.TrikePointId ?? string.Empty)));

    private async Task<List<JeepneyTripPlan>>
        ConfirmDirectTripCandidatesAsync(
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken)
    {
        var straightLineDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude);

        var candidates = new List<DirectTripCandidate>();

        if (straightLineDistance <= MaxWalkOnlyTripDistanceMeters)
        {
            candidates.Add(new DirectTripCandidate(
                WalkAccess(
                    (destinationLatitude, destinationLongitude),
                    straightLineDistance),
                MaxWalkOnlyTripDistanceMeters));
        }

        if (straightLineDistance <= MaxWalkTrikeTripDistanceMeters)
        {
            foreach (var trikePoint in FindNearbyTrikePoints(
                         originLatitude,
                         originLongitude))
            {
                candidates.Add(new DirectTripCandidate(
                    TrikeAccess(
                        (destinationLatitude, destinationLongitude),
                        trikePoint,
                        ApproximateDistanceMeters(
                            originLatitude,
                            originLongitude,
                            trikePoint.Latitude,
                            trikePoint.Longitude),
                        ApproximateDistanceMeters(
                            trikePoint.Latitude,
                            trikePoint.Longitude,
                            destinationLatitude,
                            destinationLongitude)),
                    MaxWalkTrikeTripDistanceMeters));
            }
        }

        var tasks = candidates.Select(async candidate =>
        {
            var access = await ConfirmAccessAsync(
                candidate.Access,
                (originLatitude, originLongitude),
                (destinationLatitude, destinationLongitude),
                cancellationToken);

            if (access is null)
                return null;

            var actualDistance = access.WalkDistanceMeters +
                (access.TrikeRideDistanceMeters ?? 0);

            var maximumDistance = access.Mode == AccessMode.Walk
                ? MaxWalkOnlyTripDistanceMeters
                : candidate.MaximumDistanceMeters;

            if (actualDistance > maximumDistance)
                return null;

            var legs = BuildAccessLegs(
                access,
                (originLatitude, originLongitude),
                (destinationLatitude, destinationLongitude));

            if (!IsWithinTotalWalkingLimit(legs))
                return null;

            return CreateTripPlan(
                legs,
                access,
                EmptyAccessSegment());
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(plan => plan is not null)
            .Select(plan => plan!)
            .ToList();
    }

    private static JeepneyAccessSegment EmptyAccessSegment() =>
        new()
        {
            Mode = AccessMode.Walk,
            TotalTimeSeconds = 0,
            TotalFarePesos = 0,
            GeneralizedCostPesos = 0
        };

    /// <summary>
    /// A transfer is "useful" when it produces a good complete journey,
    /// not merely when the two route geometries are closest together.
    ///
    /// Route sample ordering is used to enforce direction: after boarding
    /// the second route, its alighting point must be ahead of the transfer.
    /// </summary>
    private JourneyCandidate? BuildOneTransferCandidate(
        StaticJeepneyRoute firstRoute,
        List<(double Latitude, double Longitude)> firstSamples,
        RouteInterchange interchange,
        AccessCandidate originAccess,
        (double[] Cost, AccessCandidate?[] Access) secondRouteSuffix)
    {
        var secondSamples =
            _routeSamples[interchange.OtherRouteId];

        if (interchange.OwnIndex <= 0 ||
            interchange.OtherIndex >= secondSamples.Count - 1)
        {
            return null;
        }

        var destinationAccess =
            secondRouteSuffix.Access[interchange.OtherIndex];

        if (destinationAccess is null)
            return null;

        var boardIndex =
            GetNearestSampleIndex(
                firstSamples,
                originAccess.Anchor);

        var alightIndex =
            GetNearestSampleIndex(
                secondSamples,
                destinationAccess.Anchor);

        // Cheap early rejection; HasForwardRouteProgress performs the final
        // authoritative full-geometry direction check before confirmation.
        if (alightIndex <= interchange.OtherIndex)
            return null;

        var firstRideMeters =
            RouteDistanceBetweenSamples(
                firstSamples,
                boardIndex,
                interchange.OwnIndex);

        var secondRideMeters =
            RouteDistanceBetweenSamples(
                secondSamples,
                interchange.OtherIndex,
                alightIndex);

        var rideTime =
            (firstRideMeters + secondRideMeters) /
            JeepneySpeedMetersPerSecond;

        var transferWalkTime =
            interchange.DistanceMeters /
            WalkingSpeedMetersPerSecond;

        var provisionalCost =
            originAccess.GeneralizedCostPesos +
            destinationAccess.GeneralizedCostPesos +
            GeneralizedCostFromWalking(
                transferWalkTime,
                interchange.DistanceMeters) +
            GeneralizedCostFromTimeAndFare(
                rideTime +
                2 * JeepneyBoardingWaitTimeSeconds,
                2 * JeepneyBaseFarePesos);

        return new JourneyCandidate(
            [
                new JourneyLegCandidate(
                    firstRoute.RouteId,
                    firstRoute.RouteName,
                    originAccess.Anchor,
                    firstSamples[interchange.OwnIndex],
                    originAccess.RouteSampleIndex,
                    interchange.OwnIndex,
                    originAccess.FullRouteAnchor,
                    GetRouteAnchor(
                        firstRoute.RouteId,
                        interchange.OwnIndex,
                        firstSamples[interchange.OwnIndex])),

                new JourneyLegCandidate(
                    interchange.OtherRouteId,
                    interchange.OtherRouteName,
                    secondSamples[interchange.OtherIndex],
                    destinationAccess.Anchor,
                    interchange.OtherIndex,
                    destinationAccess.RouteSampleIndex,
                    GetRouteAnchor(
                        interchange.OtherRouteId,
                        interchange.OtherIndex,
                        secondSamples[interchange.OtherIndex]),
                    destinationAccess.FullRouteAnchor)
            ],
            originAccess,
            destinationAccess,
            [
                new WalkSegmentCandidate(
                    firstSamples[interchange.OwnIndex],
                    secondSamples[interchange.OtherIndex],
                    interchange.DistanceMeters)
            ],
            provisionalCost);
    }

    private static int GetNearestSampleIndex(
        List<(double Latitude, double Longitude)> samples,
        (double Latitude, double Longitude) point)
    {
        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;

        for (var i = 0; i < samples.Count; i++)
        {
            var distance = ApproximateDistanceMeters(
                point.Latitude,
                point.Longitude,
                samples[i].Latitude,
                samples[i].Longitude);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static double RouteDistanceBetweenSamples(
        List<(double Latitude, double Longitude)> samples,
        int startIndex,
        int endIndex)
    {
        if (endIndex <= startIndex)
            return 0;

        var distance = 0.0;

        for (var i = startIndex; i < endIndex; i++)
        {
            distance += ApproximateDistanceMeters(
                samples[i].Latitude,
                samples[i].Longitude,
                samples[i + 1].Latitude,
                samples[i + 1].Longitude);
        }

        return distance;
    }

    private sealed record ConfirmedJourneyCandidate(
        JourneyCandidate Candidate,
        JeepneyTripPlan Plan);
}
