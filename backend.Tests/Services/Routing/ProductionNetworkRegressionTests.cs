using backend.Models.Routing;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Regressions that run against the real network rather than a drawn one.
/// See <see cref="ProductionNetworkFixture"/> for provenance.
///
/// These exist because the synthetic-corridor suites stayed green through two
/// production routing failures. Real geometry is what caught them.
/// </summary>
public sealed class ProductionNetworkRegressionTests
{
    // Trips that the deployed API answers, used here as fixed reference
    // journeys through the middle of the network.
    private const double SampleAOriginLat = 15.12254950605129;
    private const double SampleAOriginLon = 120.5997480979361;
    private const double SampleBOriginLat = 15.109698583445889;
    private const double SampleBOriginLon = 120.58240903543013;
    private const double SharedDestinationLat = 15.139582098206548;
    private const double SharedDestinationLon = 120.60108373338038;

    // Calzadang Bayu Elementary School -> Withe's Enterprises, the trip from
    // the production report.
    private const double ReportedOriginLat = 15.10268;
    private const double ReportedOriginLon = 120.580438;
    private const double ReportedDestinationLat = 15.150765;
    private const double ReportedDestinationLon = 120.592846;

    [Fact]
    public async Task SmCpoint_DirectBoardingOccurrenceAlsoSurvivesTransferPrefix()
    {
        var service = ProductionNetworkFixture.CreateService();
        var snapshot = await service.InspectBoardingSelectionAsync(
            ProductionNetworkFixture.LinkCorridor,
            downstreamSampleIndex: 18,
            SampleBOriginLat,
            SampleBOriginLon,
            SharedDestinationLat,
            SharedDestinationLon);

        // This physically distinct early occurrence is selected by the direct
        // route's progress diversity. The former scalar prefix had already
        // replaced it with the provisionally cheaper board at ~4.63 km.
        var early = Assert.Single(snapshot.Direct,
            state => Math.Abs(state.ProgressMeters - 2_474.4) < 5);
        var formerScalar = Assert.IsType<BoardingStateSnapshot>(
            snapshot.PreviousScalarPrefix);

        Assert.Equal(4_630.8, formerScalar.ProgressMeters, precision: 0);
        Assert.NotEqual(early.OccurrenceKey, formerScalar.OccurrenceKey);
        Assert.Contains(snapshot.TransferPrefix,
            state => state.OccurrenceKey == early.OccurrenceKey);
        Assert.Equal(8, snapshot.TransferPrefix.Count);
        Assert.NotNull(early.ConfirmedTrikeMeters);
    }

    [Fact]
    public async Task SmCpoint_UsefulJeepneyTransferStillWorksWithMultiplePrefixStates()
    {
        var plans = await PlanAsync(
            SampleBOriginLat, SampleBOriginLon,
            SharedDestinationLat, SharedDestinationLon);

        Assert.Contains(plans, plan => JeepneyLegs(plan)
            .Select(leg => leg.RouteId)
            .SequenceEqual(["JEEP-SAMPLE-02", ProductionNetworkFixture.LinkCorridor]));
    }

    // -----------------------------------------------------------------
    // The reported symptom: whenever SM-CPOINT-HOLY-HIWAY was the useful
    // corridor the planner avoided it, staying on another jeepney or walking
    // instead. It was not being scored badly -- transfer-candidate generation
    // spent each route's budget on whichever interchange happened to be
    // enumerated first, so the journeys through it were never built. From this
    // origin the corridor disappeared entirely and the planner substituted a
    // longer ride on MARISOL.
    //
    // Only this origin is asserted here. From the other sample origin the
    // corridor did surface even before the fix (inside a worse journey), so it
    // cannot witness the regression -- it is covered by the tests below.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_BuildsJourneysThroughTheLinkCorridor()
    {
        var plans = await PlanAsync(
            SampleBOriginLat, SampleBOriginLon,
            SharedDestinationLat, SharedDestinationLon);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => JeepneyLegs(plan)
            .Any(leg => leg.RouteId == ProductionNetworkFixture.LinkCorridor));
    }

    // -----------------------------------------------------------------
    // Feeder shadowing, checked against real road geometry rather than a
    // straight line. The shape this rules out is a tricycle covering several
    // kilometres of a corridor a jeepney was already running, which the
    // deployed build still returns (a 4.3 km tricycle onto a 585 m jeepney
    // ride won "fastest" there). Journeys with no jeepney at all are exempt:
    // a direct tricycle is an honest answer, not a feeder overstepping.
    //
    // This guards the feeder-shadow rules rather than witnessing the transfer
    // generation fix -- it holds with either generation strategy.
    // -----------------------------------------------------------------
    [Theory]
    [InlineData(SampleAOriginLat, SampleAOriginLon)]
    [InlineData(SampleBOriginLat, SampleBOriginLon)]
    public async Task PlanTripsAsync_KeepsTricyclesInAFeederRole(
        double originLat,
        double originLon)
    {
        var plans = await PlanAsync(
            originLat, originLon, SharedDestinationLat, SharedDestinationLon);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            if (JeepneyLegs(plan).Count == 0)
                return;

            var originTrike = plan.OriginAccess.TrikeRideDistanceMeters ?? 0;
            Assert.True(
                originTrike < 4_000,
                $"A {originTrike:F0}m origin tricycle is covering the corridor: " +
                $"{Describe(plan)}");
        });
    }

    // -----------------------------------------------------------------
    // The trip from the production report returns nothing, and the reason is
    // coverage rather than search: from that origin the nearest route is about
    // 2.2 km away by road against an 1,800 m access limit, and the nearest
    // terminal about 1.5 km against a 1,200 m limit. Lift the terminal limit
    // and the same planner answers immediately -- including through the link
    // corridor.
    //
    // This is pinned so that a future "no route found" report from this area
    // can be told apart from a routing regression at a glance.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_ReportedTripIsLimitedByAccessRangeNotBySearch()
    {
        var atDeployedLimits = await PlanAsync(
            ReportedOriginLat, ReportedOriginLon,
            ReportedDestinationLat, ReportedDestinationLon);

        Assert.Empty(atDeployedLimits);

        var withTerminalInRange = await PlanAsync(
            ReportedOriginLat, ReportedOriginLon,
            ReportedDestinationLat, ReportedDestinationLon,
            ProductionNetworkFixture.DeployedOptionsWith(
                maxWalkToTrikePointMeters: 1_500));

        Assert.NotEmpty(withTerminalInRange);
        Assert.Contains(withTerminalInRange, plan => JeepneyLegs(plan).Count > 0);
        Assert.Contains(withTerminalInRange, plan => JeepneyLegs(plan)
            .Any(leg => leg.RouteId == ProductionNetworkFixture.LinkCorridor));
    }

    // -----------------------------------------------------------------
    // A jeepney ridden only to reach another jeepney the passenger could
    // already board where they started.
    //
    // This is a GUARD, not a witness. The shape is real -- the deployed API
    // returns it, for instance
    //
    //     walk 1152 m -> trike 3101 m -> CPOINT-HENSONVILLE-HOLY 520 m
    //                 -> walk 16 m -> VILLA-PAMPANG(SM-TELEBASTAGAN) 2440 m
    //
    // where the second route passes the first boarding point at zero metres,
    // at progress 2866 m, and is ridden from 2542 m to 4972 m.
    //
    // But it is not reproducible here. Tests substitute a straight-line
    // stand-in for Valhalla, and whether the redundant journey escapes Pareto
    // pruning turns on a few metres of access distance, which the stand-in
    // gets wrong. Checked against the real Valhalla instance: this trip does
    // not produce the redundant prefix either way. The rule's logic is
    // witnessed by RedundantTransitPrefixRegressionTests instead, on a
    // deterministic fixture where the margin is built in.
    // -----------------------------------------------------------------
    [Fact]
    public async Task PlanTripsAsync_DoesNotRideOneJeepneyJustToReachAnother()
    {
        var plans = await PlanAsync(15.12, 120.595, 15.135, 120.58);

        Assert.NotEmpty(plans);

        // The journey that simply boards the through route must be there.
        Assert.Contains(plans, plan =>
            JeepneyLegs(plan).Count == 1 &&
            JeepneyLegs(plan)[0].RouteId == "VILLA-PAMPANG(SM-TELEBASTAGAN)");

        // The redundant prefix must not.
        Assert.DoesNotContain(plans, plan => JeepneyLegs(plan)
            .Select(leg => leg.RouteId)
            .SequenceEqual(["VILLA-PAMPANG(SUPER-8)", "VILLA-PAMPANG(SM-TELEBASTAGAN)"]));
    }

    // -----------------------------------------------------------------
    // Whatever else changes, a returned plan has to be a journey: physically
    // connected legs, forward progress on every jeepney, and totals that match
    // the legs they are made of.
    // -----------------------------------------------------------------
    [Theory]
    [InlineData(SampleAOriginLat, SampleAOriginLon)]
    [InlineData(SampleBOriginLat, SampleBOriginLon)]
    public async Task PlanTripsAsync_ReturnsStructurallyValidJourneys(
        double originLat,
        double originLon)
    {
        var plans = await PlanAsync(
            originLat, originLon, SharedDestinationLat, SharedDestinationLon);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            Assert.NotEmpty(plan.Legs);
            Assert.All(plan.Legs, leg => Assert.True(
                leg.DistanceMeters > 0,
                $"Zero-length {leg.Mode} leg in {Describe(plan)}"));

            Assert.Equal(plan.Legs.Sum(leg => leg.DistanceMeters > 0 ? leg.FarePesos : 0),
                plan.TotalFarePesos, 6);
            Assert.Equal(plan.Legs.Sum(leg => leg.DurationSeconds),
                plan.TotalTimeSeconds, 6);

            for (var index = 0; index < plan.Legs.Count - 1; index++)
            {
                var gap = Haversine(
                    plan.Legs[index].DestinationLatitude,
                    plan.Legs[index].DestinationLongitude,
                    plan.Legs[index + 1].OriginLatitude,
                    plan.Legs[index + 1].OriginLongitude);
                Assert.True(gap <= 25, $"Disconnected legs ({gap:F0}m) in {Describe(plan)}");
            }
        });
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static async Task<List<JeepneyTripPlan>> PlanAsync(
        double originLat,
        double originLon,
        double destinationLat,
        double destinationLon,
        RoutingOptions? options = null)
    {
        var service = ProductionNetworkFixture.CreateService(options);
        return await service.PlanTripsAsync(
            originLat, originLon, destinationLat, destinationLon);
    }

    private static List<JeepneyTripLeg> JeepneyLegs(JeepneyTripPlan plan) =>
        plan.Legs.Where(leg => leg.Mode == AccessMode.Jeepney).ToList();

    private static string Describe(JeepneyTripPlan plan) =>
        string.Join(" > ", plan.Legs.Select(leg => leg.Mode switch
        {
            AccessMode.Jeepney => $"JEEP {leg.RouteId} {leg.DistanceMeters:F0}m",
            AccessMode.Trike => $"TRIKE {leg.DistanceMeters:F0}m",
            _ => $"WALK {leg.DistanceMeters:F0}m"
        })) + $" [{plan.RecommendationType}]";

    private static double Haversine(
        double fromLat, double fromLon, double toLat, double toLon)
    {
        const double earthRadiusMeters = 6_371_000;
        var deltaLat = (toLat - fromLat) * Math.PI / 180;
        var deltaLon = (toLon - fromLon) * Math.PI / 180;
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(fromLat * Math.PI / 180) * Math.Cos(toLat * Math.PI / 180) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
