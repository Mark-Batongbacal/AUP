using System.Diagnostics;
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
        CancellationToken cancellationToken = default) =>
        await PlanTripsAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences: null,
            cancellationToken);

    public async Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken = default)
    {
        using var planTelemetry = _telemetry.BeginRoutingPlan(
            "RoutingService",
            cancellationToken);
        using var passTelemetry = _telemetry.BeginRoutingPass(
            MaxTransfers,
            cancellationToken);
        _telemetry.SetRoutingValue(
            "max_candidates_to_confirm",
            MaxCandidatesToConfirm);
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

        _telemetry.SetRoutingValue(
            "routes_considered_before_spatial_filter",
            _routes.Count);

        var planningPreferences = NormalizePlanningPreferences(preferences);
        var maxWalkAccessDistanceMeters =
            GetWalkAccessDistanceLimit(planningPreferences);
        var spatialDiscoveryStarted = Stopwatch.GetTimestamp();
        var spatialAccessRadiusMeters =
            GetConservativeSpatialAccessRadiusMeters(
                maxWalkAccessDistanceMeters);
        var originRouteIds = _spatialRouteIndex.FindNearbyRoutes(
            originLatitude,
            originLongitude,
            spatialAccessRadiusMeters).ToHashSet(StringComparer.Ordinal);
        var destinationRouteIds = _spatialRouteIndex.FindNearbyRoutes(
            destinationLatitude,
            destinationLongitude,
            spatialAccessRadiusMeters).ToHashSet(StringComparer.Ordinal);

        // Existing origin feeder semantics allow any route anchor to be
        // reached from a TODA the passenger can walk to. There is no feeder
        // ride-distance cap, so narrowing this set would be a false negative.
        // Destination tricycle access is different: the route anchor itself
        // must be near a TODA, which is static and captured by the snapshot.
        if (FindNearbyTrikePoints(originLatitude, originLongitude).Count > 0)
        {
            foreach (var route in _routes)
                originRouteIds.Add(route.RouteId);
        }
        if (MaxNearbyTrikeCandidates > 0)
            destinationRouteIds.UnionWith(_routesWithTodaAccess);

        // A live onboard occurrence remains authoritative even if reported
        // GPS drift puts the passenger just outside the conservative query.
        if (planningPreferences?.OnboardTransit is { } onboardContext &&
            _routeSamples.ContainsKey(onboardContext.RouteId))
        {
            originRouteIds.Add(onboardContext.RouteId);
        }

        var originRoutes = new List<StaticJeepneyRoute>(_routes.Count);
        var routesForAccessDiscovery =
            new List<StaticJeepneyRoute>(_routes.Count);
        var directRoutes = new List<StaticJeepneyRoute>(_routes.Count);
        var destinationRouteCount = 0;
        foreach (var route in _routes)
        {
            var isOriginRoute = originRouteIds.Contains(route.RouteId);
            var isDestinationRoute = destinationRouteIds.Contains(route.RouteId);
            if (isOriginRoute)
                originRoutes.Add(route);
            if (isDestinationRoute)
                destinationRouteCount++;
            if (isOriginRoute || isDestinationRoute)
                routesForAccessDiscovery.Add(route);
            if (isOriginRoute && isDestinationRoute)
                directRoutes.Add(route);
        }
        _telemetry.SetRoutingValue(
            "spatial_access_radius_meters",
            spatialAccessRadiusMeters);
        _telemetry.SetRoutingValue(
            "origin_routes_after_spatial_filter",
            originRoutes.Count);
        _telemetry.SetRoutingValue(
            "destination_routes_after_spatial_filter",
            destinationRouteCount);
        _telemetry.SetRoutingValue(
            "direct_routes_after_spatial_filter",
            directRoutes.Count);
        _telemetry.SetRoutingValue(
            "routes_considered_after_spatial_filter",
            originRoutes.Count);
        _telemetry.SetRoutingValue(
            "routes_in_access_discovery_union",
            routesForAccessDiscovery.Count);
        _telemetry.ObserveRouting(
            "spatial_route_discovery_ms",
            Stopwatch.GetElapsedTime(spatialDiscoveryStarted).TotalMilliseconds);
        var candidateGenerationStarted = Stopwatch.GetTimestamp();
        var accessDiscoveryStarted = Stopwatch.GetTimestamp();

        var boardAccessPrefixByRoute =
            new Dictionary<string, IReadOnlyList<AccessCandidate>[]>();

        var destinationAccessByRoute =
            new Dictionary<string, IReadOnlyList<AccessCandidate>>(
                StringComparer.Ordinal);
        var directConnectionsByRoute =
            new Dictionary<string, List<RouteConnectionCandidate>>(StringComparer.Ordinal);
        List<AccessPathDestinationCompletionEdge> accessPathCompletionEdges;
        List<DirectAccessDestinationCompletionEdge> directCompletionEdges;
        var accessDiscoveryDiagnostics = new AccessDiscoveryDiagnostics();
        _accessDiscoveryDiagnostics = accessDiscoveryDiagnostics;

        try
        {
            using (_telemetry.BeginRoutingStage("access_discovery"))
            {
                foreach (var route in routesForAccessDiscovery)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var routeId = route.RouteId;
                    var samples = _routeSamples[routeId];
                    var isOriginRoute = originRouteIds.Contains(routeId);
                    var isDestinationRoute = destinationRouteIds.Contains(routeId);
                    var routeDiagnosticCountsBefore =
                        accessDiscoveryDiagnostics.Counts();
                    var boardDiscoveryMilliseconds = 0.0;
                    var directConnectionMilliseconds = 0.0;
                    var prefixMilliseconds = 0.0;
                    var destinationAccessMilliseconds = 0.0;
                    var boardAlternatives = 0;
                    var destinationAlternatives = 0;
                    var directConnections = new List<RouteConnectionCandidate>();
                    BoardAccessDiscovery? boardDiscovery = null;

                    if (isOriginRoute)
                    {
                        var boardDiscoveryStarted = Stopwatch.GetTimestamp();
                        boardDiscovery = await DiscoverBoardAccessOptionsAsync(
                            routeId,
                            samples,
                            originLatitude,
                            originLongitude,
                            cancellationToken,
                            maxWalkAccessDistanceMeters);
                        boardDiscoveryMilliseconds = Stopwatch.GetElapsedTime(
                            boardDiscoveryStarted).TotalMilliseconds;
                        _telemetry.ObserveRouting(
                            "board_access_discovery_by_route_ms",
                            boardDiscoveryMilliseconds);
                        if (planningPreferences?.OnboardTransit is { } onboard &&
                            string.Equals(
                                onboard.RouteId,
                                routeId,
                                StringComparison.Ordinal))
                        {
                            var anchor = GetRouteAnchorAtProgress(
                                routeId,
                                onboard.CurrentRouteProgressMeters);
                            boardDiscovery = boardDiscovery with
                            {
                                Onboard = WalkAccess(
                                    (anchor.Latitude, anchor.Longitude),
                                    0,
                                    GetNearestSampleIndex(
                                        samples,
                                        (anchor.Latitude, anchor.Longitude)),
                                    anchor) with
                                {
                                    IsNetworkWalkConfirmed = true,
                                    IsAlreadyOnboard = true
                                }
                            };
                        }

                        if (isDestinationRoute)
                        {
                            var directConnectionStarted = Stopwatch.GetTimestamp();
                            directConnections = FindBestConnections(
                                route,
                                originLatitude,
                                originLongitude,
                                destinationLatitude,
                                destinationLongitude,
                                boardDiscovery,
                                maxWalkAccessDistanceMeters,
                                planningPreferences?.OnboardTransit);
                            directConnectionMilliseconds = Stopwatch.GetElapsedTime(
                                directConnectionStarted).TotalMilliseconds;
                            _telemetry.ObserveRouting(
                                "direct_connection_discovery_ms",
                                directConnectionMilliseconds);
                            _telemetry.IncrementRouting(
                                "direct_connections_generated",
                                directConnections.Count);
                            directConnectionsByRoute[routeId] = directConnections;
                        }

                        // Origin candidates seed transfer search. Intermediate
                        // routes remain reachable through the complete
                        // snapshot interchange graph.
                        var prefixStarted = Stopwatch.GetTimestamp();
                        var accessPrefix = ComputePrefixAccessOptions(
                            routeId,
                            ConstrainTransitAccessOptions(
                                boardDiscovery.Projected,
                                maxWalkAccessDistanceMeters),
                            directConnections.Select(candidate =>
                                candidate.BoardAccess));
                        boardAccessPrefixByRoute[routeId] = ApplyOnboardAccessContext(
                            routeId,
                            accessPrefix,
                            boardDiscovery.Onboard,
                            planningPreferences?.OnboardTransit);
                        prefixMilliseconds = Stopwatch.GetElapsedTime(
                            prefixStarted).TotalMilliseconds;
                        _telemetry.ObserveRouting(
                            "prefix_access_computation_ms",
                            prefixMilliseconds);

                        boardAlternatives =
                            boardDiscovery.Projected.Sum(candidate =>
                                candidate.AllAlternatives.Count) +
                            boardDiscovery.SearchAnchors.Sum(candidate =>
                                candidate.AllAlternatives.Count) +
                            (boardDiscovery.Exact?.AllAlternatives.Count ?? 0);
                        _telemetry.IncrementRouting(
                            "board_access_alternatives",
                            boardAlternatives);
                    }

                    if (isDestinationRoute)
                    {
                        var destinationAccessStarted = Stopwatch.GetTimestamp();
                        var alightOptions = ComputeAlightAccessOptions(
                            routeId,
                            samples,
                            destinationLatitude,
                            destinationLongitude);
                        var constrainedAlightOptions =
                            ConstrainTransitAccessOptions(
                                alightOptions,
                                maxWalkAccessDistanceMeters);
                        destinationAccessByRoute[routeId] =
                            DistinctAccessOccurrences(
                                constrainedAlightOptions
                                    .Where(access => access is not null)
                                    .Select(access => access!)
                                    .Concat(directConnections.Select(candidate =>
                                        candidate.AlightAccess)));
                        destinationAccessMilliseconds = Stopwatch.GetElapsedTime(
                            destinationAccessStarted).TotalMilliseconds;
                        destinationAlternatives = alightOptions.Sum(candidate =>
                            candidate.AllAlternatives.Count);
                        _telemetry.IncrementRouting(
                            "destination_access_alternatives",
                            destinationAlternatives);
                    }

                    _telemetry.ObserveRouting(
                        "access_alternatives_per_route",
                        boardAlternatives + destinationAlternatives);
                    var routeDiagnosticCounts =
                        accessDiscoveryDiagnostics.Counts() -
                        routeDiagnosticCountsBefore;
                    _telemetry.RecordRoutingAccessDiscoveryRoute(
                        routeId,
                        samples.Count,
                        boardDiscoveryMilliseconds,
                        directConnectionMilliseconds,
                        prefixMilliseconds,
                        destinationAccessMilliseconds,
                        routeDiagnosticCounts.TodaCandidatesConsidered,
                        routeDiagnosticCounts.TodaCandidatesSurvivingFilters,
                        routeDiagnosticCounts.TodaCandidatesSelected,
                        boardAlternatives,
                        destinationAlternatives,
                        directConnections.Count);
                }

                // Destination-completion edges are discovered from origin/access
                // states before any transit candidate is confirmed. They run in
                // parallel with transit confirmation and never depend on a bad suffix
                // surviving pruning merely to reveal that the trip could have ended.
                var accessPathDiscoveryStarted = Stopwatch.GetTimestamp();
                accessPathCompletionEdges = BuildAccessPathDestinationCompletionEdges(
                    boardAccessPrefixByRoute,
                    directConnectionsByRoute,
                    destinationLatitude,
                    destinationLongitude);
                _telemetry.ObserveRouting(
                    "access_path_completion_discovery_ms",
                    Stopwatch.GetElapsedTime(
                        accessPathDiscoveryStarted).TotalMilliseconds);
                _telemetry.IncrementRouting(
                    "access_path_completion_edges_generated",
                    accessPathCompletionEdges.Count);

                var directCompletionStarted = Stopwatch.GetTimestamp();
                directCompletionEdges = BuildDirectAccessDestinationCompletionEdges(
                    originLatitude,
                    originLongitude,
                    destinationLatitude,
                    destinationLongitude);
                _telemetry.ObserveRouting(
                    "direct_connection_completion_discovery_ms",
                    Stopwatch.GetElapsedTime(
                        directCompletionStarted).TotalMilliseconds);
                _telemetry.IncrementRouting(
                    "direct_completion_edges_generated",
                    directCompletionEdges.Count);
            }
        }
        finally
        {
            _accessDiscoveryDiagnostics = null;
            accessDiscoveryDiagnostics.Flush(_telemetry);
        }
        _telemetry.ObserveRouting(
            "access_discovery_ms",
            Stopwatch.GetElapsedTime(accessDiscoveryStarted).TotalMilliseconds);

        var transferCandidateGenerationStarted = Stopwatch.GetTimestamp();
        var candidates = new List<JourneyCandidate>();

        // 0 transfers. Keep several boarding variants for each route instead
        // of collapsing to FirstOrDefault before Valhalla can confirm access.
        foreach (var route in directRoutes)
        {
            if (!directConnectionsByRoute.TryGetValue(
                    route.RouteId,
                    out var routeDirectConnections))
                continue;

            foreach (var direct in routeDirectConnections)
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

        var transferCandidates = FindTransferCandidates(
            boardAccessPrefixByRoute,
            destinationAccessByRoute,
            originRoutes,
            cancellationToken).ToList();
        candidates.AddRange(transferCandidates);
        _telemetry.IncrementRouting("candidates_generated", candidates.Count);
        _telemetry.IncrementRouting(
            "candidates_entering_expensive_expansion",
            candidates.Count);
        _telemetry.ObserveRouting(
            "transfer_candidate_generation_ms",
            Stopwatch.GetElapsedTime(
                transferCandidateGenerationStarted).TotalMilliseconds);

        // Access generation stores walking and tricycle choices together on
        // one route anchor. Expand them before ranking; otherwise confirmation
        // accepts only the first valid choice and useful multimodal variants
        // (for example trike -> jeepney -> trike) disappear.
        var hardConstraintTicks = 0L;
        var candidatesRejectedByHardConstraints = 0L;
        var accessExpansionStarted = Stopwatch.GetTimestamp();
        var expandedCandidates = candidates
            .SelectMany(ExpandAccessAlternatives)
            // Full-route progress is authoritative. This catches wrong-way or
            // zero-progress legs even when sparse sample indices look valid.
            .Where(MeetsMeasuredTransitHardConstraints)
            .ToList();
        var accessExpansionAndFilterTicks =
            Stopwatch.GetTimestamp() - accessExpansionStarted;
        _telemetry.ObserveRouting(
            "access_expansion_ms",
            StopwatchTicksToMilliseconds(Math.Max(
                0,
                accessExpansionAndFilterTicks - hardConstraintTicks)));
        _telemetry.IncrementRouting(
            "candidates_after_access_expansion",
            expandedCandidates.Count);
        _telemetry.IncrementRouting("candidates_expanded", expandedCandidates.Count);
        _telemetry.IncrementRouting(
            "candidates_rejected_by_hard_constraints",
            candidatesRejectedByHardConstraints);

        var candidateKeyGenerationTicks = 0L;
        var candidateDedupeStarted = Stopwatch.GetTimestamp();
        var distinctCandidates = expandedCandidates
            .GroupBy(GetMeasuredJourneyCandidateKey, StringComparer.Ordinal)
            .Select(group => new KeyedJourneyCandidate(
                HasSoftPlanningPreference(planningPreferences)
                    ? group
                        .OrderBy(candidate => PlanningCandidateScore(
                            candidate, planningPreferences))
                        .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
                        .First()
                    : group
                        .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                        .First(),
                group.Key))
            .ToList();
        var candidateDedupeAndKeyTicks =
            Stopwatch.GetTimestamp() - candidateDedupeStarted;
        _telemetry.ObserveRouting(
            "candidate_key_generation_ms",
            StopwatchTicksToMilliseconds(candidateKeyGenerationTicks));
        _telemetry.ObserveRouting(
            "candidate_dedupe_ms",
            StopwatchTicksToMilliseconds(Math.Max(
                0,
                candidateDedupeAndKeyTicks - candidateKeyGenerationTicks)));
        _telemetry.IncrementRouting(
            "candidates_after_dedupe",
            distinctCandidates.Count);

        // Phase 2 reserves part of the confirmation budget for distinct route
        // and boarding regions so a dense cluster of similar candidates cannot
        // crowd out useful alternatives before authoritative validation.
        var diversitySelectionAllocatedBytesBefore =
            GC.GetAllocatedBytesForCurrentThread();
        var diversitySelectionStarted = Stopwatch.GetTimestamp();
        var ranked = SelectCandidatesToConfirmWithDiversity(
            distinctCandidates, planningPreferences);
        var diversitySelectionAllocatedBytes = Math.Max(
            0,
            GC.GetAllocatedBytesForCurrentThread() -
            diversitySelectionAllocatedBytesBefore);
        _telemetry.ObserveRouting(
            "diversity_selection_ms",
            Stopwatch.GetElapsedTime(diversitySelectionStarted).TotalMilliseconds);
        _telemetry.ObserveRouting(
            "diversity_selection_allocated_bytes",
            diversitySelectionAllocatedBytes);
        var eligibleDirectCompletionEdges = directCompletionEdges
            .Where(MeetsMeasuredDirectHardConstraints)
            .ToList();
        var eligibleAccessPathCompletionEdges = accessPathCompletionEdges
            .Where(MeetsMeasuredAccessPathHardConstraints)
            .ToList();
        _telemetry.ObserveRouting(
            "hard_constraint_filter_ms",
            StopwatchTicksToMilliseconds(hardConstraintTicks));
        _telemetry.IncrementRouting(
            "transit_candidates_selected_for_confirmation",
            ranked.Count);
        _telemetry.IncrementRouting(
            "direct_candidates_selected_for_confirmation",
            eligibleDirectCompletionEdges.Count);
        _telemetry.IncrementRouting(
            "access_path_candidates_selected_for_confirmation",
            eligibleAccessPathCompletionEdges.Count);
        _telemetry.IncrementRouting(
            "candidates_selected_for_confirmation",
            ranked.Count +
            eligibleDirectCompletionEdges.Count +
            eligibleAccessPathCompletionEdges.Count);
        _telemetry.ObserveRouting(
            "candidate_generation_ms",
            Stopwatch.GetElapsedTime(candidateGenerationStarted).TotalMilliseconds);

        // Preserve transit sources while confirming all terminal-edge kinds
        // through one boundary. Transit-specific semantic pruning still has
        // its exact route occurrence; access-only completions join afterward.
        var completionEdges = ranked
            .Cast<DestinationCompletionEdge>()
            .Concat(eligibleDirectCompletionEdges)
            .Concat(eligibleAccessPathCompletionEdges)
            .ToList();
        var confirmationStarted = Stopwatch.GetTimestamp();
        DestinationCompletionConfirmationResult completionResult;
        using (_telemetry.BeginRoutingStage("confirmation"))
        {
            completionResult = await ConfirmDestinationCompletionEdgesAsync(
                completionEdges,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken,
                maxWalkAccessDistanceMeters);
        }
        _telemetry.ObserveRouting(
            "confirmation_ms",
            Stopwatch.GetElapsedTime(confirmationStarted).TotalMilliseconds);
        _telemetry.IncrementRouting(
            "transit_candidates_confirmed",
            completionResult.Transit.Count);
        _telemetry.IncrementRouting(
            "access_only_candidates_confirmed",
            completionResult.AccessOnly.Count);

        bool MeetsMeasuredTransitHardConstraints(JourneyCandidate candidate)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var accepted = candidate.Legs.All(HasForwardRouteProgress) &&
                    MeetsProvisionalHardConstraints(
                        candidate,
                        planningPreferences);
                if (!accepted)
                    candidatesRejectedByHardConstraints++;
                return accepted;
            }
            finally
            {
                hardConstraintTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        bool MeetsMeasuredDirectHardConstraints(
            DirectAccessDestinationCompletionEdge edge)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                return MeetsProvisionalHardConstraints(
                    edge,
                    planningPreferences);
            }
            finally
            {
                hardConstraintTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        bool MeetsMeasuredAccessPathHardConstraints(
            AccessPathDestinationCompletionEdge edge)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                return MeetsProvisionalHardConstraints(
                    edge,
                    planningPreferences);
            }
            finally
            {
                hardConstraintTicks += Stopwatch.GetTimestamp() - started;
            }
        }

        string GetMeasuredJourneyCandidateKey(JourneyCandidate candidate)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                return GetJourneyCandidateKey(candidate);
            }
            finally
            {
                candidateKeyGenerationTicks +=
                    Stopwatch.GetTimestamp() - started;
            }
        }

        static double StopwatchTicksToMilliseconds(long ticks) =>
            ticks * 1_000.0 / Stopwatch.Frequency;

        var pruningStarted = Stopwatch.GetTimestamp();
        // Pairwise pruning must only use journeys that are eligible to reach
        // the user-facing result set. A provisionally short walk can exceed
        // the configured transit-access cap once Valhalla confirms the road
        // path; allowing that journey to remain as a pruning reference can
        // shadow a valid tricycle-access alternative which the facade would
        // otherwise keep, before the over-limit walk is itself discarded.
        // This applies the existing cap to authoritative distances and does
        // not alter direct walk/tricycle completion limits.
        var confirmedWithSource = completionResult.Transit
            .Where(result =>
                IsTransitAccessWithinLimit(
                    result.Plan.OriginAccess,
                    maxWalkAccessDistanceMeters) &&
                IsTransitAccessWithinLimit(
                    result.Plan.DestinationAccess,
                    maxWalkAccessDistanceMeters) &&
                MeetsConfirmedHardConstraints(result.Plan, planningPreferences))
            .ToList();

        // Feeder shadowing, on all three boundaries where a feeder can stop
        // connecting to transit and start replacing it. Each stage logs its
        // own rejections at Debug level (see LogFeederShadowRejection).
        var originPruned = PruneConfirmedFeederShadowing(confirmedWithSource);
        var transferPruned = PruneConfirmedTransferBoardingShadowing(originPruned);
        var destinationPruned = PruneConfirmedDestinationFeederShadowing(transferPruned);

        // A jeepney used only to reach another jeepney the passenger could
        // already board where they started is not a transfer, it is a wasted
        // fare. Decided on full-route progress, so a loop's return leg at the
        // same coordinates does not qualify.
        var prefixPruned = PruneRedundantTransitPrefix(destinationPruned);

        var paretoPruned = PruneDominatedConfirmedCandidates(prefixPruned);
        _telemetry.IncrementRouting(
            "confirmed_candidates_rejected_dominated",
            prefixPruned.Count - paretoPruned.Count);
        var finalEquivalentPruned =
            DeduplicateFinalNearEquivalentJourneys(paretoPruned);
        _telemetry.IncrementRouting(
            "confirmed_candidates_rejected_duplicate_equivalent",
            paretoPruned.Count - finalEquivalentPruned.Count);
        var confirmed = finalEquivalentPruned
            .Select(result => result.Plan)
            .ToList();

        var distinctPlans = confirmed
            .Concat(completionResult.AccessOnly.Where(plan =>
                MeetsConfirmedHardConstraints(plan, planningPreferences)))
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

        // Direct/access-state completions join only after transit candidates
        // are confirmed. Give them the same conservative Pareto comparison so
        // a transit suffix cannot survive solely because the better complete
        // access journey came from a different generation path.
        var finalParetoPlans = PruneDominatedPlans(distinctPlans);

        // Structural sanity is the last gate before objectives, so Cheapest
        // and Fastest choose from sensible commutes rather than from the
        // cheapest arithmetic combination of individually legal legs.
        var sensiblePlans = PruneTokenTransitJourneys(finalParetoPlans);

        var finalRankingStarted = Stopwatch.GetTimestamp();
        var selectedPlans = SelectObjectivePlans(sensiblePlans, planningPreferences);
        _telemetry.ObserveRouting(
            "final_journey_ranking_ms",
            Stopwatch.GetElapsedTime(finalRankingStarted).TotalMilliseconds);
        _telemetry.ObserveRouting(
            "pruning_ms",
            Stopwatch.GetElapsedTime(pruningStarted).TotalMilliseconds);
        _telemetry.SetRoutingValue("selected_plan_count", selectedPlans.Count);
        LogSelectedPlanDiagnostics(selectedPlans);
        _telemetry.Event(selectedPlans.Count == 0 ? "NoRouteFound" : "TripPlanned",
            outcome: selectedPlans.Count.ToString());
        var outcome = selectedPlans.Count == 0 ? "no_route" : "success";
        passTelemetry.Complete(outcome);
        planTelemetry.Complete(outcome);
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

    private JourneyPlanningPreferences? NormalizePlanningPreferences(
        JourneyPlanningPreferences? preferences)
    {
        if (preferences is null)
            return null;

        double? maxWalking = preferences.MaxWalkingMeters is { } requestedWalking
            ? Math.Min(requestedWalking, MaxTotalWalkingMetersPerJourney)
            : null;
        var normalized = preferences with
        {
            MaxFarePesos = preferences.MaxFarePesos is { } fare
                ? Math.Max(0, fare)
                : null,
            MaxWalkingMeters = maxWalking is { } walking
                ? Math.Max(0, walking)
                : null,
            AvoidTransportModes = preferences.AvoidTransportModes is null
                ? []
                : preferences.AvoidTransportModes.ToHashSet()
        };

        return normalized.MaxFarePesos is null &&
               normalized.MaxWalkingMeters is null &&
               normalized.WalkingPreference == JourneyWalkingPreference.Normal &&
               normalized.OptimizationPreference is null &&
               normalized.AvoidTransportModes.Count == 0 &&
               normalized.OnboardTransit is null
            ? null
            : normalized;
    }

    private IReadOnlyList<AccessCandidate>[] ApplyOnboardAccessContext(
        string routeId,
        IReadOnlyList<AccessCandidate>[] prefix,
        AccessCandidate? onboardAccess,
        OnboardTransitPlanningContext? context)
    {
        if (context is null || onboardAccess is null ||
            !string.Equals(context.RouteId, routeId, StringComparison.Ordinal))
            return prefix;

        return prefix.Select((options, index) =>
        {
            var retained = options.Where(candidate =>
            {
                var progress = candidate.FullRouteAnchor?.DistanceFromRouteStartMeters;
                return progress is null ||
                       (!context.IsMateriallyBehind(progress.Value) &&
                        !context.IsCurrentOccurrence(progress.Value));
            }).ToList();
            if (_routeSearchAnchors[routeId][index].DistanceFromRouteStartMeters >=
                context.CurrentRouteProgressMeters - context.ProgressToleranceMeters)
            {
                retained.Insert(0, onboardAccess);
            }
            return (IReadOnlyList<AccessCandidate>)retained;
        }).ToArray();
    }

    internal double GetWalkAccessDistanceLimit(
        JourneyPlanningPreferences? preferences)
    {
        var preferenceLimit = preferences?.WalkingPreference switch
        {
            JourneyWalkingPreference.Less => LessWalkingPreferenceAccessMeters,
            JourneyWalkingPreference.More => MoreWalkingPreferenceAccessMeters,
            _ => NormalWalkingPreferenceAccessMeters
        };
        var effectiveLimit = Math.Min(
            preferenceLimit,
            Math.Min(
                MaxWalkAccessDistanceMeters,
                MaxTotalWalkingMetersPerJourney));

        if (preferences?.MaxWalkingMeters is { } explicitWalkingLimit)
            effectiveLimit = Math.Min(effectiveLimit, explicitWalkingLimit);

        return Math.Max(0, effectiveLimit);
    }

    /// <summary>
    /// Walking access is the only radial transit-access rule. Tricycle feeder
    /// reachability is added separately from actual TODA topology because the
    /// current feeder behavior has no ride-distance cap; treating the
    /// access-only trip limit as one would remove valid transit journeys.
    /// </summary>
    internal double GetConservativeSpatialAccessRadiusMeters(
        double walkAccessDistanceMeters) =>
        walkAccessDistanceMeters;

    private bool MeetsProvisionalHardConstraints(
        JourneyCandidate candidate,
        JourneyPlanningPreferences? preferences)
    {
        if (preferences is null)
            return true;

        if (preferences.MaxFarePesos is { } maxFare &&
            EstimateCandidateFarePesos(candidate) > (double)maxFare)
            return false;

        if (preferences.MaxWalkingMeters is { } maxWalking &&
            EstimateCandidateWalkingMeters(candidate) > maxWalking)
            return false;

        return !UsesAvoidedMode(candidate, preferences.AvoidTransportModes);
    }

    private bool MeetsProvisionalHardConstraints(
        DirectAccessDestinationCompletionEdge edge,
        JourneyPlanningPreferences? preferences) =>
        MeetsProvisionalHardConstraints(edge.Access, preferences);

    private bool MeetsProvisionalHardConstraints(
        AccessPathDestinationCompletionEdge edge,
        JourneyPlanningPreferences? preferences) =>
        MeetsProvisionalHardConstraints(edge.AccessToBoard, preferences);

    private static bool MeetsProvisionalHardConstraints(
        AccessCandidate access,
        JourneyPlanningPreferences? preferences)
    {
        if (preferences is null)
            return true;

        if (preferences.MaxWalkingMeters is { } maxWalking &&
            access.WalkDistanceMeters > maxWalking)
            return false;

        if (preferences.MaxFarePesos is { } maxFare &&
            access.FarePesos > (double)maxFare)
            return false;

        return !UsesAvoidedMode(access, preferences.AvoidTransportModes);
    }

    private static bool MeetsConfirmedHardConstraints(
        JeepneyTripPlan plan,
        JourneyPlanningPreferences? preferences)
    {
        if (preferences is null)
            return true;

        if (preferences.MaxFarePesos is { } maxFare &&
            plan.TotalFarePesos > (double)maxFare)
            return false;

        if (preferences.MaxWalkingMeters is { } maxWalking &&
            plan.Legs.Where(leg => leg.Mode == AccessMode.Walk)
                .Sum(leg => leg.DistanceMeters) > maxWalking)
            return false;

        return !UsesAvoidedMode(plan, preferences.AvoidTransportModes);
    }

    private static bool UsesAvoidedMode(
        JourneyCandidate candidate,
        IReadOnlySet<AccessMode>? avoided) =>
        (avoided?.Contains(AccessMode.Jeepney) == true && candidate.Legs.Count > 0) ||
        (avoided?.Contains(AccessMode.Trike) == true &&
         (candidate.OriginAccess.Mode == AccessMode.Trike ||
          candidate.DestinationAccess.Mode == AccessMode.Trike)) ||
        (avoided?.Contains(AccessMode.Walk) == true &&
         (candidate.OriginAccess.WalkDistanceMeters > 0 ||
          candidate.DestinationAccess.WalkDistanceMeters > 0 ||
          candidate.TransferWalkSegments.Count > 0));

    private static bool UsesAvoidedMode(
        AccessCandidate access,
        IReadOnlySet<AccessMode>? avoided) =>
        (avoided?.Contains(AccessMode.Trike) == true && access.Mode == AccessMode.Trike) ||
        (avoided?.Contains(AccessMode.Walk) == true && access.WalkDistanceMeters > 0);

    private static bool UsesAvoidedMode(
        JeepneyTripPlan plan,
        IReadOnlySet<AccessMode>? avoided) =>
        avoided is not null && plan.Legs.Any(leg => avoided.Contains(leg.Mode));

    private static double EstimateCandidateWalkingMeters(JourneyCandidate candidate) =>
        candidate.OriginAccess.WalkDistanceMeters +
        candidate.DestinationAccess.WalkDistanceMeters +
        candidate.TransferWalkSegments.Sum(segment => segment.StraightLineMeters);

    private double PlanningCandidateScore(
        JourneyCandidate candidate,
        JourneyPlanningPreferences? preferences)
    {
        if (preferences is null)
            return candidate.TotalGeneralizedCostPesos;

        var score = candidate.TotalGeneralizedCostPesos;
        score = preferences.OptimizationPreference switch
        {
            JourneyOptimizationPreference.Fastest =>
                EstimateCandidateTimeSeconds(candidate) + score / 100,
            JourneyOptimizationPreference.Cheapest =>
                EstimateCandidateFarePesos(candidate) * 1_000 + score,
            _ => score
        };

        var walkingMeters = EstimateCandidateWalkingMeters(candidate);
        return preferences.WalkingPreference switch
        {
            JourneyWalkingPreference.Less => score + walkingMeters / 100,
            // Relax the normal fatigue term, without rewarding additional
            // walking in its own right.
            JourneyWalkingPreference.More => score -
                WalkingFatiguePesosPerKilometer * walkingMeters / 2_000,
            _ => score
        };
    }

    private static bool HasSoftPlanningPreference(
        JourneyPlanningPreferences? preferences) =>
        preferences is
        {
            OptimizationPreference: not null
        } ||
        preferences is
        {
            WalkingPreference: not JourneyWalkingPreference.Normal
        };

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

    internal List<JeepneyTripPlan> SelectObjectivePlans(
        List<JeepneyTripPlan> plans,
        JourneyPlanningPreferences? preferences = null)
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

        if (preferences is { } requested &&
            (requested.OptimizationPreference is not null ||
             requested.WalkingPreference != JourneyWalkingPreference.Normal))
        {
            // An explicit preference gets the lead recommendation and controls
            // alternative preservation. The generic cheapest/fastest/efficient
            // trio is intentionally not re-applied afterward, because that
            // would erase the passenger-specific ordering we preserved during
            // confirmation.
            var primary = plans
                .OrderBy(plan => PlanningPlanScore(plan, requested))
                .ThenBy(plan => plan.GeneralizedCostPesos)
                .First();
            AddObjective(primary, requested.OptimizationPreference?.ToString()
                .ToLowerInvariant() ?? "efficient");

            foreach (var alternative in plans
                         .Except(selected)
                         .OrderBy(plan => PlanningPlanScore(plan, requested))
                         .ThenBy(plan => plan.GeneralizedCostPesos)
                         .ThenBy(plan => plan.TotalTimeSeconds))
            {
                if (selected.Count >= MaxTripOptions)
                    break;
                selected.Add(alternative);
            }

            return selected;
        }

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

    private static double PlanningPlanScore(
        JeepneyTripPlan plan,
        JourneyPlanningPreferences preferences)
    {
        var score = plan.GeneralizedCostPesos;
        score = preferences.OptimizationPreference switch
        {
            JourneyOptimizationPreference.Fastest => plan.TotalTimeSeconds + score / 100,
            JourneyOptimizationPreference.Cheapest => plan.TotalFarePesos * 1_000 + score,
            _ => score
        };

        var walking = plan.Legs
            .Where(leg => leg.Mode == AccessMode.Walk)
            .Sum(leg => leg.DistanceMeters);
        return preferences.WalkingPreference switch
        {
            JourneyWalkingPreference.Less => score + walking / 100,
            JourneyWalkingPreference.More => score - walking / 2_000,
            _ => score
        };
    }

    private static string GetJourneyCandidateKey(JourneyCandidate candidate) =>
        string.Join('|', candidate.Legs.Select(leg => string.Join(':',
            leg.RouteId,
            leg.BoardIndex ?? -1,
            leg.AlightIndex ?? -1,
            Math.Round(leg.Board.Latitude, 6),
            Math.Round(leg.Board.Longitude, 6),
            Math.Round(leg.Alight.Latitude, 6),
            Math.Round(leg.Alight.Longitude, 6),
            Math.Round(leg.BoardFullRouteAnchor?.DistanceFromRouteStartMeters ?? -1, 1),
            Math.Round(leg.AlightFullRouteAnchor?.DistanceFromRouteStartMeters ?? -1, 1)))) +
        $"|{candidate.OriginAccess.Mode}:{candidate.OriginAccess.TrikePoint?.Id}" +
        $"|{candidate.DestinationAccess.Mode}:{candidate.DestinationAccess.TrikePoint?.Id}";

    internal static string GetJourneyCandidateSelectionKey(
        JourneyCandidate candidate) =>
        GetJourneyCandidateKey(candidate);

    // Leg distance is part of the identity because a route may pass the same
    // physical point twice. Two rides can share board and alight coordinates
    // yet be completely different journeys -- one riding the short way, the
    // other all the way around the loop -- and they are told apart by how far
    // the vehicle actually travels between them.
    private static string GetPlanKey(JeepneyTripPlan plan) =>
        string.Join('|', plan.Legs.Select(leg => string.Join(':',
            leg.Mode,
            leg.RouteId ?? string.Empty,
            Math.Round(leg.OriginLatitude, 6),
            Math.Round(leg.OriginLongitude, 6),
            Math.Round(leg.DestinationLatitude, 6),
            Math.Round(leg.DestinationLongitude, 6),
            Math.Round(leg.DistanceMeters, 1),
            leg.TrikePointId ?? string.Empty)));

    private List<DirectAccessDestinationCompletionEdge>
        BuildDirectAccessDestinationCompletionEdges(
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude)
    {
        var straightLineDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude);

        var candidates = new List<DirectAccessDestinationCompletionEdge>();

        if (straightLineDistance <= MaxWalkOnlyTripDistanceMeters)
        {
            candidates.Add(new DirectAccessDestinationCompletionEdge(
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
                candidates.Add(new DirectAccessDestinationCompletionEdge(
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

        return candidates;
    }

    private async Task<List<JeepneyTripPlan>>
        ConfirmDirectAccessDestinationCompletionsAsync(
            IReadOnlyList<DirectAccessDestinationCompletionEdge> candidates,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken)
    {
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

    private static int GetNearestSampleIndex(
        IReadOnlyList<(double Latitude, double Longitude)> samples,
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
        IReadOnlyList<(double Latitude, double Longitude)> samples,
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
