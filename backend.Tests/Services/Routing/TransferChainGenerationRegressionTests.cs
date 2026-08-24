using backend.Models.Routing;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Regression coverage for transfer-candidate generation fairness.
///
/// The production symptom was a useful three-jeepney journey that the planner
/// simply never built: the origin's route had fifty-five interchange edges,
/// the depth-first search spent that route's entire candidate budget inside
/// the subtree of the FIRST edge, and the remaining fifty-four -- including
/// the one the useful chain needed -- were never expanded. Which edge comes
/// first is an accident of route ordering in the database, so the planner's
/// answer was effectively decided by row order.
///
/// See <see cref="TransferChainTopologyFixture"/> for the network: sixteen
/// routes and twenty-five terminals, thirteen of the routes offering
/// plausible one-transfer alternatives out of the same neighbourhood, and the
/// useful transfer deliberately NOT the geometrically closest pair (a ~300 m
/// walk against the decoys' ~40 m crossings).
/// </summary>
public sealed class TransferChainGenerationRegressionTests
{
    private static readonly string[] ExpectedChain =
    [
        TransferChainTopologyFixture.LocalWest,
        TransferChainTopologyFixture.LinkNortheast,
        TransferChainTopologyFixture.FinalNorth
    ];

    // -----------------------------------------------------------------
    // The useful two-transfer chain must be built, confirmed, and still be
    // eligible when objectives are chosen. It is genuinely the right answer
    // here -- three flat jeepney fares and no tricycle -- so it should also
    // win, but the assertion that matters is that it EXISTS.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_BuildsTheUsefulTwoTransferChain()
    {
        var plans = await PlanAsync();

        Assert.NotEmpty(plans);

        var chain = Assert.Single(
            plans,
            plan => JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));

        Assert.Equal(2, chain.TransferCount);
        Assert.All(JeepneyLegs(chain), leg => Assert.Equal(13, leg.FarePesos));
        Assert.DoesNotContain(chain.Legs, leg => leg.Mode == AccessMode.Trike);
        Assert.Equal(39, chain.TotalFarePesos);
        Assert.Equal(plans.Min(plan => plan.TotalFarePesos), chain.TotalFarePesos);
        Assert.Contains("cheapest", chain.RecommendationType.Split(','));
    }

    // -----------------------------------------------------------------
    // The same network, with the chain's routes moved to the front of the
    // route list. Interchange edges are filed in route order, so this is the
    // only thing that differs -- and it must not change the answer. This is
    // the direct regression against edge-order starvation: before the fix the
    // chain appeared only when its edges happened to be enumerated first.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_ChainDoesNotDependOnInterchangeEnumerationOrder()
    {
        var chainRoutesLast = await PlanAsync(chainRoutesFirst: false);
        var chainRoutesFirst = await PlanAsync(chainRoutesFirst: true);

        Assert.Contains(chainRoutesFirst, plan =>
            JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));
        Assert.Contains(chainRoutesLast, plan =>
            JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));
    }

    // -----------------------------------------------------------------
    // Bounded boarding-prefix diversity means one physical first-route
    // interchange can legitimately appear once per retained origin boarding
    // occurrence. Those access variants must not consume the next level before
    // every distinct downstream interchange occurrence gets considered. The
    // useful FINAL-NORTH edge is deliberately filed after twelve viable
    // LINK-NORTHEAST fan-out edges.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_MultipleBoardingOccurrencesDoNotStarveLateSecondTransfer()
    {
        var chainRoutesLast = await PlanAsync(
            chainRoutesFirst: false,
            crowdedSecondTransfer: true);
        var chainRoutesFirst = await PlanAsync(
            chainRoutesFirst: true,
            crowdedSecondTransfer: true);

        Assert.Contains(chainRoutesFirst, plan =>
            JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));
        Assert.Contains(chainRoutesLast, plan =>
            JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));
    }

    // -----------------------------------------------------------------
    // Candidate confirmation has its own bounded budget after transfer
    // generation. A provisional direct walk can be cheapest for every
    // boarding bucket even though a tricycle feeder is the useful confirmed
    // alternative. Preserve both the physical route occurrence and a bounded
    // set of access profiles; otherwise A > B > C exists in raw search but its
    // origin-tricycle form is never submitted to Valhalla.
    //
    // Route list order is reversed in the second run as a counterexample to
    // accidental repository-order selection.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_ConfirmationBudgetPreservesOriginTrikeAccessProfile()
    {
        var feederOrigin = (Latitude: 15.0430, Longitude: 120.4920);
        var trikePoints = TransferChainTopologyFixture.BuildTrikePoints();
        trikePoints.Add(ProductionTopologyFixture.BuildToda(
            500,
            "TODA-FEEDER-ORIGIN",
            feederOrigin.Latitude,
            feederOrigin.Longitude));

        foreach (var chainRoutesFirst in new[] { false, true })
        {
            var service = TransferChainTopologyFixture.CreateService(
                options: CloneWithConfirmationBudget(
                    TransferChainTopologyFixture.DefaultOptions(),
                    20),
                chainRoutesFirst: chainRoutesFirst,
                crowdedSecondTransfer: true,
                trikePoints: trikePoints);

            var plans = await service.PlanTripsAsync(
                feederOrigin.Latitude,
                feederOrigin.Longitude,
                TransferChainTopologyFixture.Destination.Latitude,
                TransferChainTopologyFixture.Destination.Longitude);

            Assert.Contains(plans, plan =>
                JeepneyRouteIds(plan).SequenceEqual(ExpectedChain) &&
                plan.OriginAccess.Mode == AccessMode.Trike &&
                plan.OriginAccess.TrikePointId == "TODA-FEEDER-ORIGIN");

            // Access diversity supplements rather than replaces physical
            // diversity: the ordinary walk-boarded chain remains eligible.
            Assert.Contains(plans, plan =>
                JeepneyRouteIds(plan).SequenceEqual(ExpectedChain) &&
                plan.OriginAccess.Mode == AccessMode.Walk);
        }
    }

    // -----------------------------------------------------------------
    // A loop can visit the same physical transfer area more than once. The
    // fairness bucket must use route occurrences, not coordinates alone, so an
    // outbound and return-pass transfer cannot collapse before direction and
    // complete-journey cost are evaluated.
    // -----------------------------------------------------------------
    [Fact]
    public void TransferFrontierBucket_DistinguishesRepeatedRouteOccurrences()
    {
        var outbound = new RoutingService.TransferExpansionBucket(
            "B-LOOP", 12, "C-NORTH", 4);
        var returnPass = new RoutingService.TransferExpansionBucket(
            "B-LOOP", 37, "C-NORTH", 19);
        var sameOutboundOccurrence = new RoutingService.TransferExpansionBucket(
            "B-LOOP", 12, "C-NORTH", 4);

        Assert.NotEqual(outbound, returnPass);
        Assert.Equal(outbound, sameOutboundOccurrence);
    }

    // -----------------------------------------------------------------
    // Depth fairness in both directions. Shallow journeys must not starve the
    // deep one (the bug), and reserving room for the deep one must not have
    // broken shallow generation either: with one transfer allowed, this
    // network still produces a one-transfer journey out of the same origin.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_ShallowAndDeepJourneysBothGetGenerated()
    {
        var oneTransfer = await PlanAsync(maxTransfers: 1);
        Assert.NotEmpty(oneTransfer);
        Assert.Contains(oneTransfer, plan =>
            plan.Legs.Count(leg => leg.Mode == AccessMode.Jeepney) == 2);

        var twoTransfers = await PlanAsync(maxTransfers: 2);
        Assert.Contains(twoTransfers, plan =>
            JeepneyRouteIds(plan).SequenceEqual(ExpectedChain));
    }

    // -----------------------------------------------------------------
    // The chain replaced a journey whose tricycle covered 5.4 km -- the whole
    // of the first corridor -- because no journey riding that corridor existed
    // to compare it against. With the chain generated, feeder shadowing has
    // its reference back and that journey is gone.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_NoOversizedOriginTricycleOnceTheChainExists()
    {
        var plans = await PlanAsync();

        Assert.All(plans, plan => Assert.True(
            (plan.OriginAccess.TrikeRideDistanceMeters ?? 0) < 2_000,
            $"A {plan.OriginAccess.TrikeRideDistanceMeters ?? 0:F0}m origin tricycle " +
            $"is replacing the first corridor: {Describe(plan)}"));
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static async Task<List<JeepneyTripPlan>> PlanAsync(
        bool chainRoutesFirst = false,
        int maxTransfers = 2,
        bool crowdedSecondTransfer = false)
    {
        var options = TransferChainTopologyFixture.DefaultOptions();
        var service = TransferChainTopologyFixture.CreateService(
            maxTransfers == 2
                ? options
                : CloneWithMaxTransfers(options, maxTransfers),
            chainRoutesFirst,
            crowdedSecondTransfer);

        return await service.PlanTripsAsync(
            TransferChainTopologyFixture.Origin.Latitude,
            TransferChainTopologyFixture.Origin.Longitude,
            TransferChainTopologyFixture.Destination.Latitude,
            TransferChainTopologyFixture.Destination.Longitude);
    }

    private static RoutingOptions CloneWithMaxTransfers(
        RoutingOptions options,
        int maxTransfers) => new()
        {
            MaxTransfers = maxTransfers,
            MaxCandidatesToConfirm = options.MaxCandidatesToConfirm,
            MaxInterchangesPerRoutePair = options.MaxInterchangesPerRoutePair,
            MaxTransferWalkMeters = options.MaxTransferWalkMeters,
            DefaultSampleIntervalMeters = options.DefaultSampleIntervalMeters,
            MaxRouteSamples = options.MaxRouteSamples,
            MaxWalkAccessDistanceMeters = options.MaxWalkAccessDistanceMeters,
            MaxWalkToTrikePointMeters = options.MaxWalkToTrikePointMeters,
            MaxNearbyTrikeCandidates = options.MaxNearbyTrikeCandidates,
            MaxTotalWalkingMetersPerJourney = options.MaxTotalWalkingMetersPerJourney,
            MaxWalkOnlyTripDistanceMeters = options.MaxWalkOnlyTripDistanceMeters,
            MaxWalkTrikeTripDistanceMeters = options.MaxWalkTrikeTripDistanceMeters,
            MaxStaticRouteSegmentJumpMeters = options.MaxStaticRouteSegmentJumpMeters,
            MaxTripOptions = options.MaxTripOptions
        };

    private static RoutingOptions CloneWithConfirmationBudget(
        RoutingOptions options,
        int maxCandidatesToConfirm) => new()
        {
            MaxTransfers = options.MaxTransfers,
            MaxCandidatesToConfirm = maxCandidatesToConfirm,
            MaxInterchangesPerRoutePair = options.MaxInterchangesPerRoutePair,
            MaxTransferWalkMeters = options.MaxTransferWalkMeters,
            DefaultSampleIntervalMeters = options.DefaultSampleIntervalMeters,
            MaxRouteSamples = options.MaxRouteSamples,
            MaxWalkAccessDistanceMeters = options.MaxWalkAccessDistanceMeters,
            MaxWalkToTrikePointMeters = options.MaxWalkToTrikePointMeters,
            MaxNearbyTrikeCandidates = options.MaxNearbyTrikeCandidates,
            MaxTotalWalkingMetersPerJourney = options.MaxTotalWalkingMetersPerJourney,
            MaxWalkOnlyTripDistanceMeters = options.MaxWalkOnlyTripDistanceMeters,
            MaxWalkTrikeTripDistanceMeters = options.MaxWalkTrikeTripDistanceMeters,
            MaxStaticRouteSegmentJumpMeters = options.MaxStaticRouteSegmentJumpMeters,
            MaxTripOptions = options.MaxTripOptions,
            BoardingDiversityBucketMeters = options.BoardingDiversityBucketMeters
        };

    private static List<JeepneyTripLeg> JeepneyLegs(JeepneyTripPlan plan) =>
        plan.Legs.Where(leg => leg.Mode == AccessMode.Jeepney).ToList();

    private static string[] JeepneyRouteIds(JeepneyTripPlan plan) =>
        JeepneyLegs(plan).Select(leg => leg.RouteId!).ToArray();

    private static string Describe(JeepneyTripPlan plan) =>
        string.Join(" > ", plan.Legs.Select(leg => leg.Mode switch
        {
            AccessMode.Jeepney => $"JEEP {leg.RouteId} {leg.DistanceMeters:F0}m",
            AccessMode.Trike => $"TRIKE {leg.TrikePointId} {leg.DistanceMeters:F0}m",
            _ => $"WALK {leg.DistanceMeters:F0}m"
        }));
}
