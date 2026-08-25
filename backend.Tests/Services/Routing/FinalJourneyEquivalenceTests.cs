using backend.Models.Routing;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

public sealed class FinalJourneyEquivalenceTests
{
    private const double OriginLatitude = 15.1140403;
    private const double OriginLongitude = 120.5831296;
    private const double DestinationLatitude = 15.144311680416919;
    private const double DestinationLongitude = 120.595954648114059;

    [Fact]
    public async Task PlanTripsAsync_NearbyBoundedPrefixBoardsCollapseAfterConfirmation()
    {
        var service = ProductionNetworkFixture.CreateService();

        // Non-vacuity: bounded prefix search deliberately retains multiple
        // distinct, nearby occurrences on the first route. They must remain
        // distinct until their complete journeys have been confirmed.
        var prefix = await service.InspectBoardingSelectionAsync(
            "JEEP-SAMPLE-02",
            downstreamSampleIndex: 31,
            OriginLatitude,
            OriginLongitude,
            DestinationLatitude,
            DestinationLongitude);

        var nearbyBoards = prefix.TransferPrefix
            .Where(board => board.ProgressMeters is >= 1_950 and <= 2_100)
            .ToList();

        Assert.True(
            nearbyBoards.Count >= 2,
            $"Expected multiple nearby prefix states, got {string.Join(", ",
                prefix.TransferPrefix.Select(board =>
                    $"{board.ProgressMeters:F1}/{board.Mode}/{board.TodaId}"))}");

        var plans = await service.PlanTripsAsync(
            OriginLatitude,
            OriginLongitude,
            DestinationLatitude,
            DestinationLongitude);

        var sameChoice = plans.Where(plan =>
            plan.OriginAccess.Mode == AccessMode.Trike &&
            plan.OriginAccess.TrikePointId == "TRIKE-SAMPLE-01" &&
            plan.Legs
                .Where(leg => leg.Mode == AccessMode.Jeepney)
                .Select(leg => leg.RouteId)
                .SequenceEqual([
                    "JEEP-SAMPLE-02",
                    ProductionNetworkFixture.LinkCorridor
                ])).ToList();

        var visible = Assert.Single(sameChoice);
        Assert.Equal(1, visible.TransferCount);
        var firstTransit = visible.Legs.First(leg =>
            leg.Mode == AccessMode.Jeepney);
        Assert.Equal(15.119332, firstTransit.BoardLatitude, precision: 6);
        Assert.Equal(120.570012, firstTransit.BoardLongitude, precision: 6);
    }

    [Fact]
    public void FinalEquivalence_PreservesDistinctBoardingRegionAndLoopOccurrence()
    {
        var baseline = Snapshot(
            boardLatitude: 15.0000,
            boardProgressMeters: 2_000);

        // Same physical point, later traversal of a loop. Authoritative
        // progress, rather than coordinate equality, keeps it distinct.
        var laterLoopOccurrence = Snapshot(
            boardLatitude: 15.0000,
            boardProgressMeters: 9_600);

        // Similar progress but a physically different boarding area.
        var differentBoardingRegion = Snapshot(
            boardLatitude: 15.0020,
            boardProgressMeters: 2_050);

        Assert.False(RoutingService.AreFinalJourneysNearEquivalent(
            baseline,
            laterLoopOccurrence));
        Assert.False(RoutingService.AreFinalJourneysNearEquivalent(
            baseline,
            differentBoardingRegion));
    }

    private static FinalJourneyEquivalenceSnapshot Snapshot(
        double boardLatitude,
        double boardProgressMeters)
    {
        const double boardLongitude = 120.5000;
        const double alightLatitude = 15.0100;
        const double alightLongitude = 120.5100;

        return new FinalJourneyEquivalenceSnapshot(
            [new FinalTransitOccurrenceSnapshot(
                "LOOP-B",
                boardLatitude,
                boardLongitude,
                boardProgressMeters,
                alightLatitude,
                alightLongitude,
                boardProgressMeters + 1_500,
                ConfirmedRideDistanceMeters: 1_500)],
            [new FinalConfirmedLegSnapshot(
                AccessMode.Jeepney,
                "LOOP-B",
                TrikePointId: null,
                boardLatitude,
                boardLongitude,
                alightLatitude,
                alightLongitude,
                DistanceMeters: 1_500,
                DurationSeconds: 410,
                FarePesos: 13)],
            AccessMode.Walk,
            OriginTrikePointId: null,
            AccessMode.Walk,
            DestinationTrikePointId: null,
            TransferWalkDistancesMeters: [],
            TotalFarePesos: 13,
            TotalTimeSeconds: 410,
            GeneralizedCostPesos: 26.67,
            TransferCount: 0);
    }
}
