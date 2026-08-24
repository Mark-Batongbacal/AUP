using backend.Models.Routing;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

/// <summary>
/// Feeder-shadow pruning: walking and tricycles exist to get a passenger TO
/// transit and away from it at the far end. They must not quietly take over
/// the corridor itself.
///
/// Every rule here works the same way. Take a confirmed journey, find another
/// confirmed journey that is the SAME journey except that it rides transit
/// over ground the first one covered by feeder instead, and ask how much
/// transit the feeder replaced against how much extra feeder distance that
/// cost. When the extra feeder distance is a large fraction of the replaced
/// transit, the feeder was not providing access -- it was doing the
/// jeepney's job.
///
/// Comparison is always performed on confirmed (Valhalla) access distances
/// and authoritative full-route progress. Geometry decides where a boarding
/// point sits along a route; it never decides whether it is reachable.
/// </summary>
public partial class RoutingService
{
    /// <summary>
    /// ORIGIN side: "did the feeder replace transit the passenger could have
    /// ridden BEFORE boarding?"
    ///
    /// A reference journey qualifies when the candidate's remaining transit
    /// is a tail of the reference's transit -- the reference rides one or
    /// more earlier jeepney legs, and/or an earlier part of the same leg,
    /// and from the candidate's boarding point onwards the two journeys are
    /// the same. Whole replaced legs count, which is what the previous
    /// implementation could not express: a feeder that swallows an entire
    /// upstream jeepney leg changes the route sequence, and a comparison
    /// keyed on that sequence therefore never fired.
    ///
    /// A candidate with no such reference survives unconditionally: there is
    /// nothing it can be said to have replaced.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneConfirmedFeederShadowing(
        List<ConfirmedJourneyCandidate> candidates) =>
        PruneShadowedCandidates(
            candidates,
            "origin feeder shadowing",
            TryMeasureTransitReplacedBeforeBoarding,
            ConfirmedOriginAccessDistanceMeters);

    /// <summary>
    /// DESTINATION side: the mirror question -- "did the passenger abandon
    /// useful remaining transit and replace it with a large feeder AFTER
    /// alighting?"
    ///
    /// This is deliberately NOT "always stay on the jeepney longer". A
    /// reference journey has to actually exist and be confirmed, which means
    /// riding further has to be physically possible, directionally valid and
    /// reachable at the far end. And because the test is on the EXTRA feeder
    /// distance, an earlier alight survives whenever continuing would not
    /// have shortened the feeder: a destination that genuinely needs a
    /// tricycle, a corridor that turns away from the destination, or a later
    /// alighting point that is worse to leave from.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneConfirmedDestinationFeederShadowing(
        List<ConfirmedJourneyCandidate> candidates) =>
        PruneShadowedCandidates(
            candidates,
            "destination feeder shadowing",
            TryMeasureTransitReplacedAfterAlighting,
            ConfirmedDestinationAccessDistanceMeters);

    /// <summary>
    /// TRANSFER side: the same principle applied to boarding the next
    /// jeepney. A passenger should not walk substantially downstream along
    /// route B when an earlier confirmed transfer boarding on route B is
    /// already accessible from the same alighting point -- that is the
    /// transfer replacing route B, not connecting to it.
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
            var index = legIndex;
            kept = PruneShadowedCandidates(
                kept,
                $"transfer feeder shadowing at leg {index}",
                (ConfirmedJourneyCandidate candidate,
                    ConfirmedJourneyCandidate reference,
                    out double replaced) =>
                    TryMeasureTransitReplacedAtTransfer(
                        candidate, reference, index, out replaced),
                candidate => ConfirmedTransferDistanceMeters(candidate, index) ?? 0);
        }

        return kept;
    }

    private delegate bool TransitReplacementMeasure(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference,
        out double replacedTransitMeters);

    /// <summary>
    /// Shared pairwise driver. Every candidate is tested against every other
    /// confirmed candidate rather than against one representative of a
    /// string-keyed group: bucketed group keys split journeys a passenger
    /// would call identical whenever a progress value happened to land on
    /// the far side of a bucket edge, and those journeys then never met.
    ///
    /// The candidate with the smallest feeder distance can never be rejected
    /// (rejection requires strictly more feeder distance than its
    /// reference), so a stage can never empty the candidate set.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneShadowedCandidates(
        List<ConfirmedJourneyCandidate> candidates,
        string reason,
        TransitReplacementMeasure measure,
        Func<ConfirmedJourneyCandidate, double> getFeederDistanceMeters)
    {
        if (candidates.Count <= 1)
            return candidates;

        var kept = new List<ConfirmedJourneyCandidate>();

        foreach (var candidate in candidates)
        {
            var candidateFeeder = getFeederDistanceMeters(candidate);
            var shadowed = false;

            foreach (var reference in candidates)
            {
                if (ReferenceEquals(reference, candidate))
                    continue;

                if (!measure(candidate, reference, out var replacedTransit))
                    continue;

                var extraFeeder = candidateFeeder - getFeederDistanceMeters(reference);
                if (!IsFeederReplacingTransit(replacedTransit, extraFeeder))
                    continue;

                LogFeederShadowRejection(
                    reason,
                    candidate,
                    reference,
                    replacedTransit,
                    extraFeeder);
                shadowed = true;
                break;
            }

            if (!shadowed)
                kept.Add(candidate);
        }

        return kept;
    }

    /// <summary>
    /// A feeder is replacing transit -- not optimizing network access -- when
    /// the extra confirmed feeder distance it costs is a large fraction of
    /// the transit progress it removes. This is unconditional: a faster or
    /// cheaper total trip does not excuse it, because those "savings" exist
    /// precisely because the feeder substituted for the jeepney.
    /// </summary>
    private bool IsFeederReplacingTransit(
        double replacedTransitMeters,
        double extraFeederMeters) =>
        replacedTransitMeters >= FeederShadowingMinProgressMeters &&
        extraFeederMeters > 0 &&
        extraFeederMeters >= replacedTransitMeters * FeederShadowingAccessDistanceRatio;

    /// <summary>
    /// Measures the transit the candidate's origin feeder replaced, when the
    /// candidate's whole transit itinerary is a tail of the reference's.
    /// Returns false when the two journeys are not the same journey from the
    /// candidate's boarding point onwards, so unrelated itineraries are never
    /// compared.
    /// </summary>
    private bool TryMeasureTransitReplacedBeforeBoarding(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference,
        out double replacedTransitMeters)
    {
        replacedTransitMeters = 0;

        var candidateLegs = candidate.Candidate.Legs;
        var referenceLegs = reference.Candidate.Legs;
        var offset = referenceLegs.Count - candidateLegs.Count;

        if (offset < 0)
            return false;

        if (!string.Equals(
                candidateLegs[0].RouteId,
                referenceLegs[offset].RouteId,
                StringComparison.Ordinal))
        {
            return false;
        }

        var candidateBoard = GetBoardProgressMeters(candidateLegs[0]);
        var referenceBoard = GetBoardProgressMeters(referenceLegs[offset]);
        var referenceAlight = GetAlightProgressMeters(referenceLegs[offset]);

        // The candidate must board no earlier than the reference does, and
        // the reference's vehicle must actually pass the candidate's boarding
        // point -- otherwise the reference never offered that ride.
        if (candidateBoard < referenceBoard - ProgressEqualityToleranceMeters ||
            candidateBoard > referenceAlight)
        {
            return false;
        }

        // Everything AFTER the shared boarding leg must be the same journey.
        // The shared leg itself only has to be ridden through the candidate's
        // boarding point: where the reference happens to get off it says
        // nothing about whether transit could have carried the passenger TO
        // that boarding point, which is the only question here.
        for (var index = 1; index < candidateLegs.Count; index++)
        {
            if (!AreEquivalentLegs(candidateLegs[index], referenceLegs[offset + index]))
                return false;
        }

        var replaced = candidateBoard - referenceBoard;
        for (var index = 0; index < offset; index++)
            replaced += ConfirmedLegRideMeters(referenceLegs[index]);

        // Transfers the candidate never has to make are a real saving, so
        // they are removed from the transit it can be said to have replaced.
        // This only ever makes the rule harder to trigger.
        replaced -= SumTransferWalks(reference, 0, offset);

        replacedTransitMeters = Math.Max(0, replaced);
        return true;
    }

    /// <summary>
    /// Measures the remaining transit the candidate abandoned in favour of a
    /// destination feeder, when the candidate's itinerary is a head of the
    /// reference's and the reference stays on the same vehicle past the
    /// candidate's alighting point.
    /// </summary>
    private bool TryMeasureTransitReplacedAfterAlighting(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference,
        out double replacedTransitMeters)
    {
        replacedTransitMeters = 0;

        var candidateLegs = candidate.Candidate.Legs;
        var referenceLegs = reference.Candidate.Legs;

        if (referenceLegs.Count < candidateLegs.Count)
            return false;

        var last = candidateLegs.Count - 1;

        for (var index = 0; index < last; index++)
        {
            if (!AreEquivalentLegs(candidateLegs[index], referenceLegs[index]))
                return false;
        }

        if (!string.Equals(
                candidateLegs[last].RouteId,
                referenceLegs[last].RouteId,
                StringComparison.Ordinal))
        {
            return false;
        }

        // Everything BEFORE the shared leg must be the same journey. The
        // shared leg itself only has to be ridden through the candidate's
        // alighting point: where the reference boarded it says nothing about
        // whether staying on past that point was worth doing, which is the
        // only question here.
        var referenceBoard = GetBoardProgressMeters(referenceLegs[last]);
        var candidateAlight = GetAlightProgressMeters(candidateLegs[last]);
        var referenceAlight = GetAlightProgressMeters(referenceLegs[last]);

        // The candidate must leave the vehicle no later than the reference,
        // and at a point the reference was already riding through.
        if (candidateAlight > referenceAlight + ProgressEqualityToleranceMeters ||
            candidateAlight < referenceBoard)
        {
            return false;
        }

        var replaced = referenceAlight - candidateAlight;
        for (var index = last + 1; index < referenceLegs.Count; index++)
            replaced += ConfirmedLegRideMeters(referenceLegs[index]);

        replaced -= SumTransferWalks(
            reference,
            last,
            reference.Plan.TransferWalkDistancesMeters.Count);

        replacedTransitMeters = Math.Max(0, replaced);
        return true;
    }

    /// <summary>
    /// Measures the part of the next jeepney route a transfer replaced: the
    /// two journeys are the same everywhere except where they board leg
    /// <paramref name="legIndex"/>.
    /// </summary>
    private bool TryMeasureTransitReplacedAtTransfer(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference,
        int legIndex,
        out double replacedTransitMeters)
    {
        replacedTransitMeters = 0;

        var candidateLegs = candidate.Candidate.Legs;
        var referenceLegs = reference.Candidate.Legs;

        if (candidateLegs.Count != referenceLegs.Count ||
            legIndex >= candidateLegs.Count)
        {
            return false;
        }

        if (ConfirmedTransferDistanceMeters(candidate, legIndex) is null ||
            ConfirmedTransferDistanceMeters(reference, legIndex) is null)
        {
            return false;
        }

        for (var index = 0; index < candidateLegs.Count; index++)
        {
            if (index == legIndex)
                continue;

            if (!AreEquivalentLegs(candidateLegs[index], referenceLegs[index]))
                return false;
        }

        if (!string.Equals(
                candidateLegs[legIndex].RouteId,
                referenceLegs[legIndex].RouteId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsEquivalentProgress(
                GetAlightProgressMeters(candidateLegs[legIndex]),
                GetAlightProgressMeters(referenceLegs[legIndex])))
        {
            return false;
        }

        var replaced = GetBoardProgressMeters(candidateLegs[legIndex]) -
                       GetBoardProgressMeters(referenceLegs[legIndex]);

        if (replaced <= 0)
            return false;

        replacedTransitMeters = replaced;
        return true;
    }

    /// <summary>
    /// Two jeepney legs are the same leg for comparison purposes when they
    /// run the same route and their boarding and alighting positions agree
    /// within tolerance. A tolerance, rather than a shared bucket index, is
    /// what makes this robust: bucketing splits two positions a few metres
    /// apart whenever they straddle a bucket edge, and those journeys then
    /// never get compared at all.
    /// </summary>
    private bool AreEquivalentLegs(JourneyLegCandidate left, JourneyLegCandidate right) =>
        string.Equals(left.RouteId, right.RouteId, StringComparison.Ordinal) &&
        IsEquivalentProgress(
            GetBoardProgressMeters(left),
            GetBoardProgressMeters(right)) &&
        IsEquivalentProgress(
            GetAlightProgressMeters(left),
            GetAlightProgressMeters(right));

    private bool IsEquivalentProgress(double left, double right) =>
        Math.Abs(left - right) <= ProgressEqualityToleranceMeters;

    private double ConfirmedLegRideMeters(JourneyLegCandidate leg) =>
        Math.Max(
            0,
            GetAlightProgressMeters(leg) - GetBoardProgressMeters(leg));

    private static double SumTransferWalks(
        ConfirmedJourneyCandidate candidate,
        int fromIndex,
        int toExclusiveIndex)
    {
        var walks = candidate.Plan.TransferWalkDistancesMeters;
        var total = 0.0;

        for (var index = Math.Max(0, fromIndex);
             index < Math.Min(toExclusiveIndex, walks.Count);
             index++)
        {
            total += walks[index];
        }

        return total;
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

    private static double ConfirmedOriginAccessDistanceMeters(
        ConfirmedJourneyCandidate candidate) =>
        candidate.Plan.OriginAccess.WalkDistanceMeters +
        (candidate.Plan.OriginAccess.TrikeRideDistanceMeters ?? 0);

    private static double ConfirmedDestinationAccessDistanceMeters(
        ConfirmedJourneyCandidate candidate) =>
        candidate.Plan.DestinationAccess.WalkDistanceMeters +
        (candidate.Plan.DestinationAccess.TrikeRideDistanceMeters ?? 0);

    private void LogFeederShadowRejection(
        string reason,
        ConfirmedJourneyCandidate rejected,
        ConfirmedJourneyCandidate reference,
        double replacedTransitMeters,
        double extraFeederMeters)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        _logger.LogDebug(
            "Routing candidate rejected: {Reason}; routes={Routes} " +
            "referenceRoutes={ReferenceRoutes} " +
            "replacedTransit={Replaced:F0}m extraFeeder={Extra:F0}m " +
            "fare={Fare:F2} time={Time:F0}s",
            reason,
            RouteSequence(rejected),
            RouteSequence(reference),
            replacedTransitMeters,
            extraFeederMeters,
            rejected.Plan.TotalFarePesos,
            rejected.Plan.TotalTimeSeconds);
    }

    /// <summary>
    /// Rejects journeys whose jeepney leg is a token gesture: the feeder
    /// modes cover most of the ground and the transit contributes almost
    /// nothing, as in a 30 m jeepney hop followed by a 2 km tricycle.
    ///
    /// This is deliberately NOT the primary-mode rule used for choosing the
    /// default recommendation, which would be far too aggressive as a
    /// validity test. It fires only on a genuinely lopsided journey AND only
    /// when a sensible alternative survives, so it can never empty the
    /// results or force a jeepney onto a trip that does not want one. A
    /// journey with no jeepney leg at all is not token transit -- a direct
    /// tricycle is an honest answer, it just is not pretending to be a
    /// transit journey.
    /// </summary>
    private List<JeepneyTripPlan> PruneTokenTransitJourneys(List<JeepneyTripPlan> plans)
    {
        if (plans.Count <= 1)
            return plans;

        var sensible = plans.Where(plan => !IsTokenTransitJourney(plan)).ToList();

        if (sensible.Count == 0 || sensible.Count == plans.Count)
            return plans;

        foreach (var plan in plans.Except(sensible))
        {
            _logger.LogDebug(
                "Routing candidate rejected: token transit journey; routes={Routes} " +
                "jeepney={Jeepney:F0}m feeder={Feeder:F0}m fare={Fare:F2} time={Time:F0}s",
                string.Join('>', plan.Legs
                    .Where(leg => leg.Mode == AccessMode.Jeepney)
                    .Select(leg => leg.RouteId)),
                JeepneyDistanceMeters(plan),
                FeederDistanceMeters(plan),
                plan.TotalFarePesos,
                plan.TotalTimeSeconds);
        }

        return sensible;
    }

    private bool IsTokenTransitJourney(JeepneyTripPlan plan)
    {
        if (plan.Legs.All(leg => leg.Mode != AccessMode.Jeepney))
            return false;

        return JeepneyDistanceMeters(plan) * TokenTransitJeepneyMultiple <
               FeederDistanceMeters(plan);
    }

    private static double JeepneyDistanceMeters(JeepneyTripPlan plan) =>
        plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Sum(leg => leg.DistanceMeters);

    private static double FeederDistanceMeters(JeepneyTripPlan plan) =>
        plan.Legs
            .Where(leg => leg.Mode != AccessMode.Jeepney)
            .Sum(leg => leg.DistanceMeters);
}
