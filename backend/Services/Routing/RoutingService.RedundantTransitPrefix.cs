using backend.Models.Routing;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

/// <summary>
/// Redundant transit prefix pruning.
///
/// The feeder rules ask whether walking or a tricycle was doing a jeepney's
/// job. This asks something different: whether a jeepney was doing nothing but
/// carrying the passenger to another jeepney they could already have boarded
/// where they started.
///
/// The shape, seen on the real network:
///
///     walk 660 m -> VILLA-PAMPANG(SUPER-8) 159 m -> walk 8 m
///                -> VILLA-PAMPANG(SM-TELEBASTAGAN) 2 872 m -> walk 841 m
///
/// The second route passes 12 m from where the passenger boarded the first,
/// at a progress it can still be ridden from. The first ride buys nothing: it
/// costs a fare, a boarding wait, an alighting and a transfer to travel 159 m,
/// and the journey that simply boards the second route is cheaper AND faster.
/// Pareto pruning kept it anyway, because it walked two metres less.
///
/// Everything here is decided on authoritative full-route progress, never on
/// coordinates. A route that traverses the same road twice offers two
/// different boarding occurrences at identical coordinates, and only one of
/// them may lead where the passenger is going.
/// </summary>
public partial class RoutingService
{
    /// <summary>
    /// Removes a confirmed journey when another confirmed journey is the same
    /// journey with one or more leading jeepney legs simply dropped, and is no
    /// worse to travel.
    ///
    /// The reference has to be a real, confirmed journey, which is what makes
    /// this safe: its boarding is directionally valid, its access is within
    /// the configured limits, and Valhalla has agreed the passenger can reach
    /// it. If the only place the later route passes the original boarding area
    /// is the wrong occurrence -- the return leg of a loop, or the opposite
    /// direction -- then no such journey exists, nothing is compared, and the
    /// transfer stays. That case is a legitimate journey and it survives.
    /// </summary>
    private List<ConfirmedJourneyCandidate> PruneRedundantTransitPrefix(
        List<ConfirmedJourneyCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        var kept = new List<ConfirmedJourneyCandidate>();

        foreach (var candidate in candidates)
        {
            var reference = candidates.FirstOrDefault(other =>
                !ReferenceEquals(other, candidate) &&
                IsRedundantTransitPrefix(candidate, other));

            if (reference is null)
            {
                kept.Add(candidate);
                continue;
            }

            _logger.LogDebug(
                "Routing candidate rejected: redundant transit prefix; routes={Routes} " +
                "reference={ReferenceRoutes} droppedLegs={Dropped} " +
                "fare={Fare:F2}->{ReferenceFare:F2} time={Time:F0}s->{ReferenceTime:F0}s " +
                "cost={Cost:F2}->{ReferenceCost:F2}",
                RouteSequence(candidate),
                RouteSequence(reference),
                candidate.Candidate.Legs.Count - reference.Candidate.Legs.Count,
                candidate.Plan.TotalFarePesos,
                reference.Plan.TotalFarePesos,
                candidate.Plan.TotalTimeSeconds,
                reference.Plan.TotalTimeSeconds,
                candidate.Plan.GeneralizedCostPesos,
                reference.Plan.GeneralizedCostPesos);
        }

        return kept;
    }

    /// <summary>
    /// True when <paramref name="reference"/> is <paramref name="candidate"/>
    /// with leading jeepney legs removed, reaches the same downstream state,
    /// and gives the passenger no reason to have ridden them.
    /// </summary>
    private bool IsRedundantTransitPrefix(
        ConfirmedJourneyCandidate candidate,
        ConfirmedJourneyCandidate reference)
    {
        var candidateLegs = candidate.Candidate.Legs;
        var referenceLegs = reference.Candidate.Legs;
        var dropped = candidateLegs.Count - referenceLegs.Count;

        // The reference must be strictly shorter: at least one whole jeepney
        // leg, its fare, its boarding wait and its transfer are what this rule
        // is deciding about.
        if (dropped < 1 || referenceLegs.Count == 0)
            return false;

        // (1) exact tail: the shared leg runs the same route, and (4) every
        // leg after it is the same leg by route and full-route progress.
        var shared = candidateLegs[dropped];
        var referenceShared = referenceLegs[0];

        if (!string.Equals(shared.RouteId, referenceShared.RouteId, StringComparison.Ordinal))
            return false;

        for (var index = 1; index < referenceLegs.Count; index++)
        {
            if (!AreEquivalentLegs(referenceLegs[index], candidateLegs[dropped + index]))
                return false;
        }

        // (2) the reference's boarding occurrence carries the passenger the
        // right way. Confirmed candidates are already forward-only, so this is
        // a guard against a future regression rather than a live filter -- and
        // it is what makes a wrong-way or wrong-loop occurrence at the same
        // coordinates fail to qualify.
        var referenceBoard = GetBoardProgressMeters(referenceShared);
        var referenceAlight = GetAlightProgressMeters(referenceShared);
        if (referenceAlight <= referenceBoard)
            return false;

        // (3) the shared leg has to finish in the same place, so the two
        // journeys really do reach the same downstream state.
        if (!IsEquivalentProgress(referenceAlight, GetAlightProgressMeters(shared)))
            return false;

        // (6) + (7) the dropped legs bought the passenger nothing. Access is
        // priced in: generalized cost carries access time, fare and walking
        // fatigue, so a reference needing a slightly longer walk to reach the
        // same route fails this test exactly when that walk actually costs
        // more than the ride, boarding wait and fare it saves.
        const double epsilon = 0.001;
        return reference.Plan.TotalFarePesos <= candidate.Plan.TotalFarePesos + epsilon &&
               reference.Plan.TotalTimeSeconds <= candidate.Plan.TotalTimeSeconds + epsilon &&
               reference.Plan.GeneralizedCostPesos <=
                   candidate.Plan.GeneralizedCostPesos + epsilon;
    }
}
