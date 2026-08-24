using backend.Models.Routing;

namespace backend.Services.Routing;

/// <summary>
/// The confirmed, user-visible facts used only for final journey
/// near-equivalence. Search identity remains route-occurrence exact.
/// </summary>
internal sealed record FinalJourneyEquivalenceSnapshot(
    IReadOnlyList<FinalTransitOccurrenceSnapshot> TransitOccurrences,
    IReadOnlyList<FinalConfirmedLegSnapshot> ConfirmedLegs,
    AccessMode OriginAccessMode,
    string? OriginTrikePointId,
    AccessMode DestinationAccessMode,
    string? DestinationTrikePointId,
    IReadOnlyList<double> TransferWalkDistancesMeters,
    double TotalFarePesos,
    double TotalTimeSeconds,
    double GeneralizedCostPesos,
    int TransferCount);

internal sealed record FinalTransitOccurrenceSnapshot(
    string RouteId,
    double BoardLatitude,
    double BoardLongitude,
    double BoardProgressMeters,
    double AlightLatitude,
    double AlightLongitude,
    double AlightProgressMeters,
    double ConfirmedRideDistanceMeters);

internal sealed record FinalConfirmedLegSnapshot(
    AccessMode Mode,
    string? RouteId,
    string? TrikePointId,
    double OriginLatitude,
    double OriginLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    double DistanceMeters,
    double DurationSeconds,
    double FarePesos);

public partial class RoutingService
{
    // These are final-presentation tolerances, not routing configuration.
    // They deliberately fit inside one physical boarding area while keeping
    // separate loop/directional occurrences distinct by authoritative
    // progress. The real duplicate spans 94m of route progress.
    private const double FinalRegionToleranceMeters = 120;
    private const double FinalOccurrenceProgressToleranceMeters = 120;
    private const double FinalConfirmedLegDistanceToleranceMeters = 120;
    private const double FinalConfirmedLegTimeToleranceSeconds = 30;
    private const double FinalTransferWalkToleranceMeters = 50;
    private const double FinalFareTolerancePesos = 1;
    private const double FinalTotalTimeToleranceSeconds = 45;
    private const double FinalGeneralizedCostTolerancePesos = 2;

    /// <summary>
    /// Collapses only confirmed journeys that represent the same practical
    /// passenger choice. Candidate/deeper-transfer identity has already done
    /// its job by this stage and is intentionally untouched.
    /// </summary>
    private List<ConfirmedJourneyCandidate> DeduplicateFinalNearEquivalentJourneys(
        List<ConfirmedJourneyCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        // Make the representative deterministic: the best confirmed journey
        // wins even if candidate enumeration order changes.
        var ordered = candidates
            .OrderBy(candidate => candidate.Plan.GeneralizedCostPesos)
            .ThenBy(candidate => candidate.Plan.TotalTimeSeconds)
            .ThenBy(candidate => candidate.Plan.TotalFarePesos)
            .ThenBy(candidate => GetJourneyCandidateKey(candidate.Candidate),
                StringComparer.Ordinal)
            .Select(candidate => (
                Candidate: candidate,
                Snapshot: BuildFinalJourneyEquivalenceSnapshot(candidate)))
            .ToList();

        var kept = new List<(
            ConfirmedJourneyCandidate Candidate,
            FinalJourneyEquivalenceSnapshot Snapshot)>();

        foreach (var current in ordered)
        {
            var equivalent = kept.FirstOrDefault(existing =>
                AreFinalJourneysNearEquivalent(
                    existing.Snapshot,
                    current.Snapshot));

            if (equivalent.Candidate is null)
            {
                kept.Add(current);
                continue;
            }

            _logger.LogDebug(
                "Routing candidate rejected: final near-equivalent journey. " +
                "kept={KeptKey} rejected={RejectedKey} fareDelta={FareDelta:F2} " +
                "timeDelta={TimeDelta:F1}s costDelta={CostDelta:F2}",
                GetJourneyCandidateKey(equivalent.Candidate.Candidate),
                GetJourneyCandidateKey(current.Candidate.Candidate),
                Math.Abs(equivalent.Candidate.Plan.TotalFarePesos -
                    current.Candidate.Plan.TotalFarePesos),
                Math.Abs(equivalent.Candidate.Plan.TotalTimeSeconds -
                    current.Candidate.Plan.TotalTimeSeconds),
                Math.Abs(equivalent.Candidate.Plan.GeneralizedCostPesos -
                    current.Candidate.Plan.GeneralizedCostPesos));
        }

        return kept.Select(item => item.Candidate).ToList();
    }

    private FinalJourneyEquivalenceSnapshot BuildFinalJourneyEquivalenceSnapshot(
        ConfirmedJourneyCandidate candidate)
    {
        var transitLegs = candidate.Plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .ToList();

        var occurrences = candidate.Candidate.Legs
            .Select((leg, index) => new FinalTransitOccurrenceSnapshot(
                leg.RouteId,
                leg.Board.Latitude,
                leg.Board.Longitude,
                GetBoardProgressMeters(leg),
                leg.Alight.Latitude,
                leg.Alight.Longitude,
                GetAlightProgressMeters(leg),
                transitLegs[index].DistanceMeters))
            .ToList();

        var confirmedLegs = candidate.Plan.Legs
            .Select(leg => new FinalConfirmedLegSnapshot(
                leg.Mode,
                leg.RouteId,
                leg.TrikePointId,
                leg.OriginLatitude,
                leg.OriginLongitude,
                leg.DestinationLatitude,
                leg.DestinationLongitude,
                leg.DistanceMeters,
                leg.DurationSeconds,
                leg.FarePesos))
            .ToList();

        return new FinalJourneyEquivalenceSnapshot(
            occurrences,
            confirmedLegs,
            candidate.Plan.OriginAccess.Mode,
            candidate.Plan.OriginAccess.TrikePointId,
            candidate.Plan.DestinationAccess.Mode,
            candidate.Plan.DestinationAccess.TrikePointId,
            candidate.Plan.TransferWalkDistancesMeters,
            candidate.Plan.TotalFarePesos,
            candidate.Plan.TotalTimeSeconds,
            candidate.Plan.GeneralizedCostPesos,
            candidate.Plan.TransferCount);
    }

    internal static bool AreFinalJourneysNearEquivalent(
        FinalJourneyEquivalenceSnapshot left,
        FinalJourneyEquivalenceSnapshot right)
    {
        if (left.TransferCount != right.TransferCount ||
            left.OriginAccessMode != right.OriginAccessMode ||
            left.DestinationAccessMode != right.DestinationAccessMode ||
            !string.Equals(left.OriginTrikePointId, right.OriginTrikePointId,
                StringComparison.Ordinal) ||
            !string.Equals(left.DestinationTrikePointId,
                right.DestinationTrikePointId, StringComparison.Ordinal) ||
            left.TransitOccurrences.Count != right.TransitOccurrences.Count ||
            left.ConfirmedLegs.Count != right.ConfirmedLegs.Count ||
            left.TransferWalkDistancesMeters.Count !=
                right.TransferWalkDistancesMeters.Count ||
            Math.Abs(left.TotalFarePesos - right.TotalFarePesos) >
                FinalFareTolerancePesos ||
            Math.Abs(left.TotalTimeSeconds - right.TotalTimeSeconds) >
                FinalTotalTimeToleranceSeconds ||
            Math.Abs(left.GeneralizedCostPesos - right.GeneralizedCostPesos) >
                FinalGeneralizedCostTolerancePesos)
        {
            return false;
        }

        for (var index = 0; index < left.TransitOccurrences.Count; index++)
        {
            var first = left.TransitOccurrences[index];
            var second = right.TransitOccurrences[index];

            if (!string.Equals(first.RouteId, second.RouteId,
                    StringComparison.Ordinal) ||
                PhysicalDistanceMeters(
                    first.BoardLatitude,
                    first.BoardLongitude,
                    second.BoardLatitude,
                    second.BoardLongitude) > FinalRegionToleranceMeters ||
                PhysicalDistanceMeters(
                    first.AlightLatitude,
                    first.AlightLongitude,
                    second.AlightLatitude,
                    second.AlightLongitude) > FinalRegionToleranceMeters ||
                Math.Abs(first.BoardProgressMeters - second.BoardProgressMeters) >
                    FinalOccurrenceProgressToleranceMeters ||
                Math.Abs(first.AlightProgressMeters - second.AlightProgressMeters) >
                    FinalOccurrenceProgressToleranceMeters ||
                Math.Abs(first.ConfirmedRideDistanceMeters -
                    second.ConfirmedRideDistanceMeters) >
                    FinalConfirmedLegDistanceToleranceMeters)
            {
                return false;
            }
        }

        for (var index = 0; index < left.ConfirmedLegs.Count; index++)
        {
            var first = left.ConfirmedLegs[index];
            var second = right.ConfirmedLegs[index];

            if (first.Mode != second.Mode ||
                !string.Equals(first.RouteId, second.RouteId,
                    StringComparison.Ordinal) ||
                !string.Equals(first.TrikePointId, second.TrikePointId,
                    StringComparison.Ordinal) ||
                PhysicalDistanceMeters(
                    first.OriginLatitude,
                    first.OriginLongitude,
                    second.OriginLatitude,
                    second.OriginLongitude) > FinalRegionToleranceMeters ||
                PhysicalDistanceMeters(
                    first.DestinationLatitude,
                    first.DestinationLongitude,
                    second.DestinationLatitude,
                    second.DestinationLongitude) > FinalRegionToleranceMeters ||
                Math.Abs(first.DistanceMeters - second.DistanceMeters) >
                    FinalConfirmedLegDistanceToleranceMeters ||
                Math.Abs(first.DurationSeconds - second.DurationSeconds) >
                    FinalConfirmedLegTimeToleranceSeconds ||
                Math.Abs(first.FarePesos - second.FarePesos) >
                    FinalFareTolerancePesos)
            {
                return false;
            }
        }

        for (var index = 0;
             index < left.TransferWalkDistancesMeters.Count;
             index++)
        {
            if (Math.Abs(left.TransferWalkDistancesMeters[index] -
                    right.TransferWalkDistancesMeters[index]) >
                FinalTransferWalkToleranceMeters)
            {
                return false;
            }
        }

        return true;
    }

    private static double PhysicalDistanceMeters(
        double firstLatitude,
        double firstLongitude,
        double secondLatitude,
        double secondLongitude) =>
        ApproximateDistanceMeters(
            firstLatitude,
            firstLongitude,
            secondLatitude,
            secondLongitude);
}
