using System.Diagnostics;
using backend.Models.Routing;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

/// <summary>
/// Removes an intermediate jeepney only when a separately generated and
/// confirmed journey proves that the surrounding transit occurrences can be
/// connected directly on foot and that doing so is preferable. This is a
/// structural dominance rule, not a minimum ride-distance or duration rule.
/// </summary>
public partial class RoutingService
{
    internal List<ConfirmedJourneyCandidate> PruneRedundantIntermediateTransitLegs(
        List<ConfirmedJourneyCandidate> candidates,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken = default)
    {
        long intermediateLegsExamined = 0;
        long bypassChecksAttempted = 0;
        long confirmedBypasses = 0;
        long redundantLegsDetected = 0;
        long candidatesPruned = 0;
        long retainedWithoutConfirmedBypass = 0;
        long retainedBecauseBypassInvalid = 0;
        long retainedForOccurrenceMismatch = 0;
        long retainedBecauseOriginalPreferred = 0;
        var pruningStarted = Stopwatch.GetTimestamp();

        try
        {
            if (candidates.Count <= 1)
                return candidates;

            var byRouteSequence = candidates
                .GroupBy(
                    candidate => ExactRouteSequenceKey(candidate.Candidate.Legs),
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.Ordinal);
            var kept = new List<ConfirmedJourneyCandidate>(candidates.Count);

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidateLegs = candidate.Candidate.Legs;
                var rejected = false;

                // An intermediate transit leg has transit on both sides. The
                // first and last legs are deliberately outside this rule.
                for (var intermediateIndex = 1;
                     intermediateIndex < candidateLegs.Count - 1;
                     intermediateIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    intermediateLegsExamined++;
                    bypassChecksAttempted++;

                    var bypassRouteSequence = ExactRouteSequenceKey(
                        candidateLegs,
                        intermediateIndex);
                    if (!byRouteSequence.TryGetValue(
                            bypassRouteSequence,
                            out var routeSequenceMatches))
                    {
                        retainedWithoutConfirmedBypass++;
                        continue;
                    }

                    var foundExactOccurrence = false;
                    var foundValidBypass = false;
                    ConfirmedJourneyCandidate? preferredBypass = null;
                    foreach (var reference in routeSequenceMatches)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!TryGetExactIntermediateBypassDistance(
                                candidate,
                                reference,
                                intermediateIndex,
                                out var bypassWalkMeters))
                        {
                            continue;
                        }

                        foundExactOccurrence = true;
                        if (bypassWalkMeters < 0 ||
                            bypassWalkMeters > MaxTransferWalkMeters)
                        {
                            continue;
                        }

                        foundValidBypass = true;
                        if (IsBypassPreferred(candidate.Plan, reference.Plan, preferences))
                        {
                            preferredBypass = reference;
                            break;
                        }
                    }

                    if (!foundExactOccurrence)
                    {
                        // A route-ID-only match is not evidence on loops,
                        // retraces or self-intersections. Keep the journey.
                        retainedForOccurrenceMismatch++;
                        continue;
                    }

                    if (!foundValidBypass)
                    {
                        retainedBecauseBypassInvalid++;
                        continue;
                    }

                    confirmedBypasses++;
                    if (preferredBypass is null)
                    {
                        retainedBecauseOriginalPreferred++;
                        continue;
                    }

                    redundantLegsDetected++;
                    candidatesPruned++;
                    rejected = true;
                    LogRedundantIntermediateTransitRejection(
                        candidate,
                        preferredBypass,
                        intermediateIndex,
                        preferences);
                    break;
                }

                if (!rejected)
                    kept.Add(candidate);
            }

            return kept;
        }
        finally
        {
            _telemetry.IncrementRouting(
                "intermediate_transit_legs_examined",
                intermediateLegsExamined);
            _telemetry.IncrementRouting(
                "intermediate_bypass_checks_attempted",
                bypassChecksAttempted);
            _telemetry.IncrementRouting(
                "intermediate_bypasses_within_walking_constraints",
                confirmedBypasses);
            _telemetry.IncrementRouting(
                "redundant_intermediate_legs_detected",
                redundantLegsDetected);
            _telemetry.IncrementRouting(
                "candidates_pruned_redundant_intermediate_leg",
                candidatesPruned);
            _telemetry.IncrementRouting(
                "intermediate_candidates_retained_no_confirmed_bypass",
                retainedWithoutConfirmedBypass);
            _telemetry.IncrementRouting(
                "intermediate_candidates_retained_bypass_invalid",
                retainedBecauseBypassInvalid);
            _telemetry.IncrementRouting(
                "intermediate_candidates_retained_occurrence_mismatch",
                retainedForOccurrenceMismatch);
            _telemetry.IncrementRouting(
                "intermediate_candidates_retained_original_preferred",
                retainedBecauseOriginalPreferred);

            // The rule consumes already-confirmed candidates and therefore
            // performs no matrix or route request of its own.
            _telemetry.IncrementRouting(
                "redundant_intermediate_additional_valhalla_calls",
                0);
            _telemetry.ObserveRouting(
                "redundant_intermediate_pruning_ms",
                Stopwatch.GetElapsedTime(pruningStarted).TotalMilliseconds);
        }
    }

    private bool TryGetExactIntermediateBypassDistance(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference,
        int intermediateIndex,
        out double bypassWalkMeters)
    {
        bypassWalkMeters = 0;
        var candidateLegs = candidate.Candidate.Legs;
        var referenceLegs = reference.Candidate.Legs;
        if (intermediateIndex <= 0 ||
            intermediateIndex >= candidateLegs.Count - 1 ||
            referenceLegs.Count != candidateLegs.Count - 1 ||
            candidate.Plan.TransferWalkDistancesMeters.Count <
                candidateLegs.Count - 1 ||
            reference.Plan.TransferWalkDistancesMeters.Count <
                referenceLegs.Count - 1)
        {
            return false;
        }

        if (!HasSameEndpointAccessIdentity(candidate, reference))
            return false;

        for (var referenceIndex = 0;
             referenceIndex < referenceLegs.Count;
             referenceIndex++)
        {
            var candidateIndex = referenceIndex < intermediateIndex
                ? referenceIndex
                : referenceIndex + 1;
            if (!AreSameExactRouteOccurrence(
                    candidateLegs[candidateIndex],
                    referenceLegs[referenceIndex]))
            {
                return false;
            }
        }

        var bypassTransferIndex = intermediateIndex - 1;
        if (bypassTransferIndex >=
            reference.Plan.TransferWalkDistancesMeters.Count)
        {
            return false;
        }

        bypassWalkMeters =
            reference.Plan.TransferWalkDistancesMeters[bypassTransferIndex];
        return true;
    }

    private bool AreSameExactRouteOccurrence(
        JourneyLegCandidate left,
        JourneyLegCandidate right) =>
        string.Equals(left.RouteId, right.RouteId, StringComparison.Ordinal) &&
        Math.Abs(GetBoardProgressMeters(left) - GetBoardProgressMeters(right)) <=
            RouteOccurrenceIdentityToleranceMeters &&
        Math.Abs(GetAlightProgressMeters(left) - GetAlightProgressMeters(right)) <=
            RouteOccurrenceIdentityToleranceMeters;

    private static bool HasSameEndpointAccessIdentity(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference) =>
        HasSameAccessIdentity(
            candidate.Candidate.OriginAccess,
            reference.Candidate.OriginAccess) &&
        HasSameAccessIdentity(
            candidate.Candidate.DestinationAccess,
            reference.Candidate.DestinationAccess);

    private static bool HasSameAccessIdentity(
        AccessCandidate left,
        AccessCandidate right) =>
        left.Mode == right.Mode &&
        left.IsAlreadyOnboard == right.IsAlreadyOnboard &&
        string.Equals(
            left.TrikePoint?.Id,
            right.TrikePoint?.Id,
            StringComparison.Ordinal);

    private static bool IsBypassPreferred(
        JeepneyTripPlan original,
        JeepneyTripPlan bypass,
        JourneyPlanningPreferences? preferences)
    {
        const double epsilon = 0.001;

        // A generic request preserves fastest, cheapest and efficient
        // objectives at once. Requiring no-worse time, fare and generalized
        // cost prevents a cheaper walk from deleting a genuinely faster
        // connector ride before those objectives are chosen.
        if (!HasSoftPlanningPreference(preferences))
        {
            return bypass.TotalTimeSeconds <= original.TotalTimeSeconds + epsilon &&
                   bypass.TotalFarePesos <= original.TotalFarePesos + epsilon &&
                   bypass.GeneralizedCostPesos <=
                       original.GeneralizedCostPesos + epsilon;
        }

        // Explicit fastest/cheapest/efficient and walking preferences use the
        // same score as final plan selection. Equality favors the bypass
        // because it reaches the identical future state with one fewer
        // boarding, fare event and transfer.
        return PlanningPlanScore(bypass, preferences!) <=
               PlanningPlanScore(original, preferences!) + epsilon;
    }

    private void LogRedundantIntermediateTransitRejection(
        ConfirmedJourneyCandidate rejected,
        ConfirmedJourneyCandidate bypass,
        int intermediateIndex,
        JourneyPlanningPreferences? preferences)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        var bypassTransferIndex = intermediateIndex - 1;
        var middlePlanLeg = rejected.Plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .ElementAt(intermediateIndex);
        var firstWalk = rejected.Plan.TransferWalkDistancesMeters[intermediateIndex - 1];
        var secondWalk = rejected.Plan.TransferWalkDistancesMeters[intermediateIndex];
        var bypassWalk = bypass.Plan.TransferWalkDistancesMeters[bypassTransferIndex];

        _logger.LogDebug(
            "Routing candidate rejected: redundant intermediate transit leg; " +
            "routes={Routes} bypassRoutes={BypassRoutes} middleRoute={MiddleRoute} " +
            "originalTransferWalks={FirstWalk:F0}m+{SecondWalk:F0}m " +
            "bypassWalk={BypassWalk:F0}m middleRide={Ride:F0}m/{RideTime:F0}s/" +
            "{RideFare:F2} preference={Preference} score={Score:F3}->{BypassScore:F3}",
            RouteSequence(rejected),
            RouteSequence(bypass),
            middlePlanLeg.RouteId,
            firstWalk,
            secondWalk,
            bypassWalk,
            middlePlanLeg.DistanceMeters,
            middlePlanLeg.DurationSeconds,
            middlePlanLeg.FarePesos,
            preferences?.OptimizationPreference?.ToString() ?? "multi-objective",
            preferences is null
                ? rejected.Plan.GeneralizedCostPesos
                : PlanningPlanScore(rejected.Plan, preferences),
            preferences is null
                ? bypass.Plan.GeneralizedCostPesos
                : PlanningPlanScore(bypass.Plan, preferences));
    }

    private static string ExactRouteSequenceKey(
        IReadOnlyList<JourneyLegCandidate> legs,
        int? omittedIndex = null)
    {
        var routeIds = new string[legs.Count - (omittedIndex is null ? 0 : 1)];
        var destination = 0;
        for (var index = 0; index < legs.Count; index++)
        {
            if (index == omittedIndex)
                continue;
            routeIds[destination++] = legs[index].RouteId;
        }

        return string.Join('\u001f', routeIds);
    }
}
