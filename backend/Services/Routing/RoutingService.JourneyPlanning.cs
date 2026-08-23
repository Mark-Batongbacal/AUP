using backend.Models.Routing;
using backend.Models.Valhalla;
using Microsoft.Extensions.Logging;

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
    /// Removes a confirmed boarding variant when it is a feeder (walk/trike)
    /// replacing a large share of the SAME downstream jeepney corridor rather
    /// than merely getting the passenger to transit. Comparison is performed
    /// after Valhalla confirmation, so walls, divided roads, and road network
    /// detours are reflected in the access metrics -- geometry only decides
    /// route progress, never accessibility or journey sensibility.
    ///
    /// Every candidate is compared against the best-accessible candidate that
    /// actually boards EARLIER on the same route (not a single fixed
    /// reference), so a group can contain several boarding regions without
    /// losing pairwise shadow detection. A candidate with no earlier
    /// accessible alternative in its group survives unconditionally: there is
    /// nothing to have replaced.
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

            foreach (var variant in variants)
            {
                var baseline = FindEarlierAccessibleBaseline(
                    variants,
                    variant,
                    candidate => GetBoardProgressMeters(candidate.Candidate.Legs[0]),
                    ConfirmedOriginAccessDistanceMeters);

                if (baseline is null)
                {
                    kept.Add(variant);
                    continue;
                }

                var variantAccessDistance = ConfirmedOriginAccessDistanceMeters(variant);
                var baselineAccessDistance = ConfirmedOriginAccessDistanceMeters(baseline);

                if (IsFeederReplacingTransit(
                        laterProgress: GetBoardProgressMeters(variant.Candidate.Legs[0]),
                        earlierProgress: GetBoardProgressMeters(baseline.Candidate.Legs[0]),
                        laterAccessDistance: variantAccessDistance,
                        earlierAccessDistance: baselineAccessDistance,
                        out var skippedProgress,
                        out var extraAccessDistance))
                {
                    LogFeederShadowRejection(
                        "origin feeder shadowing",
                        variant,
                        baseline,
                        variant.Candidate.Legs[0].RouteId,
                        legIndex: 0,
                        baselineAccessDistance,
                        variantAccessDistance,
                        skippedProgress,
                        extraAccessDistance);
                    continue;
                }

                kept.Add(variant);
            }
        }

        return kept;
    }

    /// <summary>
    /// Finds the strongest earlier-boarding baseline for a candidate within
    /// its downstream-itinerary group: among variants that actually board
    /// strictly earlier (by authoritative full-route progress), pick the one
    /// with the lowest confirmed access burden. "Strictly earlier" is
    /// required -- a candidate with the smallest access distance overall is
    /// not a valid baseline if it does not actually board earlier.
    /// </summary>
    private static ConfirmedJourneyCandidate? FindEarlierAccessibleBaseline(
        List<ConfirmedJourneyCandidate> variants,
        ConfirmedJourneyCandidate candidate,
        Func<ConfirmedJourneyCandidate, double> getProgressMeters,
        Func<ConfirmedJourneyCandidate, double> getAccessDistanceMeters)
    {
        var candidateProgress = getProgressMeters(candidate);

        return variants
            .Where(other =>
                !ReferenceEquals(other, candidate) &&
                getProgressMeters(other) < candidateProgress)
            .OrderBy(getAccessDistanceMeters)
            .ThenBy(other => other.Plan.GeneralizedCostPesos)
            .FirstOrDefault();
    }

    /// <summary>
    /// A later board is a feeder replacing transit -- not a legitimate
    /// network-access optimization -- when the extra confirmed access burden
    /// it costs is a large fraction of the jeepney progress it skips. This is
    /// unconditional: a faster or cheaper total trip does not excuse it,
    /// because the time/fare "savings" exist precisely because the feeder
    /// substituted for the jeepney ride.
    /// </summary>
    private bool IsFeederReplacingTransit(
        double laterProgress,
        double earlierProgress,
        double laterAccessDistance,
        double earlierAccessDistance,
        out double skippedProgress,
        out double extraAccessDistance)
    {
        skippedProgress = laterProgress - earlierProgress;
        extraAccessDistance = laterAccessDistance - earlierAccessDistance;

        return skippedProgress >= FeederShadowingMinProgressMeters &&
               extraAccessDistance > 0 &&
               extraAccessDistance >= skippedProgress * FeederShadowingAccessDistanceRatio;
    }

    private void LogFeederShadowRejection(
        string reason,
        ConfirmedJourneyCandidate rejected,
        ConfirmedJourneyCandidate baseline,
        string routeId,
        int legIndex,
        double earlierAccessDistanceMeters,
        double laterAccessDistanceMeters,
        double skippedProgressMeters,
        double extraAccessDistanceMeters)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        var earlierBoard = baseline.Candidate.Legs[legIndex].Board;
        var laterBoard = rejected.Candidate.Legs[legIndex].Board;

        _logger.LogDebug(
            "Routing candidate rejected: {Reason}; route={RouteId} " +
            "earlierBoard=({EarlierLat:F6},{EarlierLon:F6})@{EarlierProgress:F0}m " +
            "laterBoard=({LaterLat:F6},{LaterLon:F6})@{LaterProgress:F0}m " +
            "earlierFeeder={EarlierAccess:F0}m laterFeeder={LaterAccess:F0}m " +
            "skippedProgress={Skipped:F0}m extraFeeder={Extra:F0}m",
            reason,
            routeId,
            earlierBoard.Latitude,
            earlierBoard.Longitude,
            GetBoardProgressMeters(baseline.Candidate.Legs[legIndex]),
            laterBoard.Latitude,
            laterBoard.Longitude,
            GetBoardProgressMeters(rejected.Candidate.Legs[legIndex]),
            earlierAccessDistanceMeters,
            laterAccessDistanceMeters,
            skippedProgressMeters,
            extraAccessDistanceMeters);
    }

    /// <summary>
    /// Identifies the downstream transit itinerary a boarding candidate
    /// produces: the jeepney route sequence and approximate alighting
    /// regions. Origin access mode/TODA identity are deliberately excluded --
    /// those are exactly the alternatives feeder-shadow pruning must compare
    /// against each other, not partition into separate, single-member groups.
    /// Destination access is kept: it is part of what happens downstream of
    /// the first boarding, not an alternative way of reaching it.
    /// </summary>
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

    /// <summary>
    /// Decides whether a journey actually uses the jeepney as its PRIMARY
    /// corridor mode, rather than merely containing a jeepney leg.
    ///
    /// Everything reaching this point is already a valid journey: direction,
    /// continuity, walking limits, transfer structure and feeder shadowing
    /// have all been enforced upstream. What is left to establish is a
    /// transport-role question -- is the jeepney carrying this trip, or is it
    /// a token hop bolted onto a journey the feeder modes are really making?
    ///
    /// Two conditions, both required. The absolute distance rules out a
    /// jeepney segment so short the mode is pointless. The share rules out a
    /// journey whose ground is mostly covered by walking or tricycle, which
    /// is what a feeder mode overstepping its role looks like.
    /// </summary>
    private bool IsPracticalJeepneyJourney(JeepneyTripPlan plan)
    {
        var jeepneyDistance = plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Sum(leg => leg.DistanceMeters);

        if (jeepneyDistance < PrimaryJeepneyMinimumDistanceMeters)
            return false;

        var totalDistance = plan.Legs.Sum(leg => leg.DistanceMeters);

        return totalDistance > 0 &&
               jeepneyDistance / totalDistance >= PrimaryJeepneyMinimumJourneyShare;
    }

    private List<JeepneyTripPlan> SelectObjectivePlans(
        List<JeepneyTripPlan> plans)
    {
        if (plans.Count == 0)
            return [];

        // Fastest and cheapest remain pure objectives over every valid
        // journey: if a direct tricycle really is the quickest way to get
        // there, saying so is honest. Only the default recommendation is
        // role-aware (see below).
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

        // Tuki is a public-transport planner: the jeepney is the cheap
        // long-distance backbone, and walking/tricycles exist to connect the
        // gaps around it. Generalized cost alone cannot express that -- it
        // treats a 9km tricycle and a 9km jeepney ride as interchangeable
        // ways to cover ground, so a direct tricycle can capture the default
        // recommendation purely by dodging jeepney boarding wait.
        //
        // So when a practical jeepney journey exists, the default is chosen
        // from among those journeys. When none does (a short local hop, or a
        // corridor that would need an absurd detour), every mode competes as
        // an equal peer exactly as before -- the jeepney is preferred, never
        // forced.
        var jeepneyJourneys = plans.Where(IsPracticalJeepneyJourney).ToList();
        var defaultPlans = jeepneyJourneys.Count > 0 ? jeepneyJourneys : plans;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Routing default recommendation drawn from {Scope} ({Count} of {Total} plans)",
                jeepneyJourneys.Count > 0 ? "practical jeepney journeys" : "all valid journeys",
                defaultPlans.Count,
                plans.Count);
        }

        // Efficient balances total time, fare, and access burden. Access burden
        // intentionally includes tricycle and transfer time instead of looking
        // only at walking, so a long feeder leg cannot appear "comfortable"
        // merely because it contains little walking. Normalizing within the
        // chosen scope keeps the trade-off meaningful: comparing jeepney
        // journeys against each other, not against a mode that is not
        // supposed to be carrying the corridor.
        var minTime = defaultPlans.Min(plan => plan.TotalTimeSeconds);
        var maxTime = defaultPlans.Max(plan => plan.TotalTimeSeconds);
        var minFare = defaultPlans.Min(plan => plan.TotalFarePesos);
        var maxFare = defaultPlans.Max(plan => plan.TotalFarePesos);
        var accessBurden = defaultPlans.ToDictionary(
            plan => plan,
            plan => plan.Legs
                .Where(leg => leg.Mode != AccessMode.Jeepney)
                .Sum(leg => leg.DurationSeconds));
        var minAccessBurden = accessBurden.Values.Min();
        var maxAccessBurden = accessBurden.Values.Max();

        static double Normalize(double value, double min, double max) =>
            max <= min ? 0 : (value - min) / (max - min);

        var efficient = defaultPlans
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
