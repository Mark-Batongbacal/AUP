using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Destination-completion-edge regressions: every boarded/current transit
/// state can finish through its own forward destination access before taking
/// another transfer. Counterexamples keep transfers when an apparent
/// destination occurrence is behind the board or unreachable on the road
/// network.
/// </summary>
public sealed class DestinationCompletionEdgeRegressionTests
{
    private const string Through = "B-THROUGH-DESTINATION";
    private const string Return = "C-RETURN-TO-DESTINATION";
    private const string Feeder = "A-FEEDER-TO-THROUGH";
    private const string Toda = "TODA-ORIGIN";

    private static readonly (double Latitude, double Longitude) Origin =
        (15.0000, 120.5000);
    private static readonly (double Latitude, double Longitude) OriginToda =
        (15.0005, 120.5000);
    private static readonly (double Latitude, double Longitude) PrefixBoard =
        (15.0000, 120.5100);
    private static readonly (double Latitude, double Longitude) LaterBoard =
        (15.0000, 120.5070);
    private static readonly (double Latitude, double Longitude) Destination =
        (15.0000, 120.5400);
    private static readonly (double Latitude, double Longitude) Transfer =
        (15.0060, 120.5460);

    [Fact]
    public async Task PlanTripsAsync_CompletesOnEarlierLegBeforeTransferReturnsToDestination()
    {
        var routes = BuildLoopWitnessRoutes();
        var service = ProductionTopologyFixture.CreateService(
            PrefixOptions(maxBoardingVariantsPerRoute: 2),
            routes: routes,
            trikePoints:
            [
                ProductionTopologyFixture.BuildToda(
                    1, Toda, OriginToda.Latitude, OriginToda.Longitude)
            ]);

        var generatedByTransferSearch =
            await service.InspectTransferDestinationCompletionsAsync(
                Origin.Latitude,
                Origin.Longitude,
                Destination.Latitude,
                Destination.Longitude);
        var rootCompletion = generatedByTransferSearch
            .Where(candidate =>
                candidate.RouteIds.SequenceEqual([Through]) &&
                DistanceMeters(
                    candidate.TransitOccurrences[0].AlightLatitude,
                    candidate.TransitOccurrences[0].AlightLongitude,
                    Destination.Latitude,
                    Destination.Longitude) < 100)
            .OrderBy(candidate =>
                candidate.TransitOccurrences[0].AlightProgressMeters)
            .FirstOrDefault();
        Assert.NotNull(rootCompletion);
        Assert.True(
            rootCompletion.TransitOccurrences[0].AlightProgressMeters >
            rootCompletion.TransitOccurrences[0].BoardProgressMeters,
            "Transfer search must emit the boarded route's own forward " +
            "destination edge before taking its outgoing transfer.");

        var repeatedDestinationOccurrences = generatedByTransferSearch
            .Where(candidate => candidate.RouteIds.SequenceEqual([Through]))
            .Select(candidate => candidate.TransitOccurrences[0])
            .Where(occurrence => DistanceMeters(
                occurrence.AlightLatitude,
                occurrence.AlightLongitude,
                Destination.Latitude,
                Destination.Longitude) < 100)
            .Select(occurrence => occurrence.AlightProgressMeters)
            .DistinctBy(progress => Math.Round(progress, 0))
            .OrderBy(progress => progress)
            .ToList();
        Assert.True(repeatedDestinationOccurrences.Count >= 2,
            "A loop's repeated visit to the same destination region must " +
            "remain separate in authoritative route progress.");
        Assert.True(repeatedDestinationOccurrences[^1] -
                    repeatedDestinationOccurrences[0] > 1_000);

        var plans = await service.PlanTripsAsync(
            Origin.Latitude,
            Origin.Longitude,
            Destination.Latitude,
            Destination.Longitude);

        Assert.NotEmpty(plans);

        // The exact boarding representatives can change as boarding-region
        // discovery improves. The semantic invariant is that transfer search
        // can finish on B's first forward destination occurrence instead of
        // forcing B -> C.
        var matchingCompletions = plans.Where(plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through]) &&
            DistanceMeters(JeepneyLegs(plan)[0].DestinationLatitude,
                JeepneyLegs(plan)[0].DestinationLongitude,
                Destination.Latitude,
                Destination.Longitude) < 100).ToList();
        Assert.NotEmpty(matchingCompletions);
        Assert.All(matchingCompletions, completion =>
        {
            Assert.Equal(0, completion.TransferCount);
            Assert.Equal(AccessMode.Trike, completion.OriginAccess.Mode);
        });
        Assert.DoesNotContain(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through, Return]));

    }

    [Fact]
    public async Task PlanTripsAsync_IntermediateRouteCanCompleteBeforeSecondTransfer()
    {
        var firstTransfer = (Latitude: 15.0000, Longitude: 120.5100);
        var secondTransfer = (Latitude: 15.0060, Longitude: 120.5460);
        var routes = new List<TransportRoute>
        {
            Route(1, Feeder,
            [
                Origin,
                firstTransfer,
                (15.0000, 120.5150)
            ]),
            Route(2, Through,
            [
                (firstTransfer.Latitude + 0.00005,
                    firstTransfer.Longitude + 0.00005),
                Destination,
                secondTransfer
            ]),
            Route(3, Return,
            [
                (secondTransfer.Latitude + 0.00005,
                    secondTransfer.Longitude + 0.00005),
                (15.0100, 120.5500),
                Destination,
                (14.9990, 120.5420)
            ])
        };
        var service = ProductionTopologyFixture.CreateService(
            PrefixOptions(maxBoardingVariantsPerRoute: 6, maxTransfers: 2),
            routes: routes,
            trikePoints: []);

        var generatedByTransferSearch =
            await service.InspectTransferDestinationCompletionsAsync(
                Origin.Latitude,
                Origin.Longitude,
                Destination.Latitude,
                Destination.Longitude);
        Assert.Contains(generatedByTransferSearch, candidate =>
            candidate.RouteIds.SequenceEqual([Feeder, Through]) &&
            candidate.TransitOccurrences[^1].AlightProgressMeters >
            candidate.TransitOccurrences[^1].BoardProgressMeters);

        var plans = await service.PlanTripsAsync(
            Origin.Latitude,
            Origin.Longitude,
            Destination.Latitude,
            Destination.Longitude);

        Assert.Contains(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Feeder, Through]));
        Assert.DoesNotContain(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Feeder, Through, Return]));
    }

    [Fact]
    public async Task PlanTripsAsync_KeepsTransferWhenDestinationOccurrenceIsBehindBoard()
    {
        var wrongOccurrence = Destination;
        var board = (Latitude: 15.0000, Longitude: 120.5000);
        var transfer = (Latitude: 15.0060, Longitude: 120.5100);
        var routes = new List<TransportRoute>
        {
            Route(1, Through,
            [
                wrongOccurrence,
                (15.0060, 120.5150),
                board,
                transfer
            ]),
            Route(2, Return,
            [
                transfer,
                (15.0100, 120.5220),
                wrongOccurrence,
                (14.9990, 120.5420)
            ])
        };
        var service = ProductionTopologyFixture.CreateService(
            PrefixOptions(maxBoardingVariantsPerRoute: 6),
            routes: routes,
            trikePoints: []);

        var plans = await service.PlanTripsAsync(
            board.Latitude,
            board.Longitude,
            Destination.Latitude,
            Destination.Longitude);

        Assert.NotEmpty(plans);
        var transferPlan = Assert.Single(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through, Return]));
        Assert.True(DistanceMeters(
                JeepneyLegs(transferPlan)[0].OriginLatitude,
                JeepneyLegs(transferPlan)[0].OriginLongitude,
                board.Latitude,
                board.Longitude) < 100);

        // B contains the destination coordinate globally, but only before this
        // passenger's board progress. Coordinate equality must not manufacture
        // a backward/loop completion.
        Assert.DoesNotContain(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through]));
    }

    [Fact]
    public async Task PlanTripsAsync_KeepsTransferWhenNearDestinationCannotBeReachedOnFoot()
    {
        var destination = (Latitude: 15.0000, Longitude: 120.5200);
        var board = (Latitude: 15.0001, Longitude: 120.5000);
        var inaccessiblePass = (Latitude: 15.0001, Longitude: 120.5200);
        var transfer = (Latitude: 15.0060, Longitude: 120.5260);
        var blockedQueries = 0;

        double? PedestrianOverride(ValhallaLocation source, ValhallaLocation target)
        {
            var targetsDestination = DistanceMeters(
                target.Lat, target.Lon,
                destination.Latitude, destination.Longitude) < 50;
            var startsOnInaccessibleRoad =
                source.Lat > destination.Latitude + 0.00005 &&
                source.Lat < transfer.Latitude - 0.0002 &&
                source.Lon > destination.Longitude - 0.001 &&
                source.Lon < transfer.Longitude + 0.001;
            if (!targetsDestination || !startsOnInaccessibleRoad)
                return null;

            blockedQueries++;
            return double.PositiveInfinity;
        }

        var routes = new List<TransportRoute>
        {
            Route(1, Through,
            [
                board,
                inaccessiblePass,
                transfer
            ]),
            Route(2, Return,
            [
                transfer,
                (15.0100, 120.5290),
                destination,
                (14.9990, 120.5220)
            ])
        };
        var valhalla = new RoadNetworkValhallaService(
            pedestrianOverride: PedestrianOverride);
        var service = ProductionTopologyFixture.CreateService(
            PrefixOptions(maxBoardingVariantsPerRoute: 6),
            valhalla,
            routes,
            trikePoints: []);

        var plans = await service.PlanTripsAsync(
            board.Latitude,
            board.Longitude,
            destination.Latitude,
            destination.Longitude);

        Assert.True(blockedQueries > 0,
            "The candidate near B's pass must be rejected by network confirmation.");
        Assert.Contains(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through, Return]));
        Assert.DoesNotContain(plans, plan =>
            JeepneyRouteIds(plan).SequenceEqual([Through]));
    }

    private static List<TransportRoute> BuildLoopWitnessRoutes() =>
    [
        Route(1, Through,
        [
            (15.0140, 120.4880),
            (15.0140, 120.5120),
            PrefixBoard,
            Destination,
            Transfer,
            (15.0180, 120.5350),
            (15.0180, 120.5150),
            LaterBoard,
            (15.0220, 120.5600),
            Destination,
            (14.9970, 120.5450)
        ]),
        Route(2, Return,
        [
            (Transfer.Latitude + 0.00005, Transfer.Longitude + 0.00005),
            (14.9940, 120.5520),
            (14.9960, 120.5430),
            Destination,
            (14.9990, 120.5380)
        ])
    ];

    private static RoutingOptions PrefixOptions(
        int maxBoardingVariantsPerRoute,
        int maxTransfers = 1) => new()
    {
        DefaultSampleIntervalMeters = 150,
        MaxRouteSamples = 100,
        MaxTransfers = maxTransfers,
        MaxTripOptions = 10,
        MaxCandidatesToConfirm = 300,
        MaxInterchangesPerRoutePair = 4,
        MaxTransferWalkMeters = 200,
        MaxBoardingVariantsPerRoute = maxBoardingVariantsPerRoute,
        MaxWalkAccessDistanceMeters = 200,
        MaxWalkToTrikePointMeters = 200,
        MaxNearbyTrikeCandidates = 2,
        MaxTotalWalkingMetersPerJourney = 2_500,
        MaxWalkOnlyTripDistanceMeters = 100,
        MaxWalkTrikeTripDistanceMeters = 100,
        MaxStaticRouteSegmentJumpMeters = 15_000,
        BoardingDiversityBucketMeters = 500
    };

    private static TransportRoute Route(
        int id,
        string code,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints) =>
        ProductionTopologyFixture.BuildDenseRoute(id, code, code, waypoints);

    private static List<JeepneyTripLeg> JeepneyLegs(JeepneyTripPlan plan) =>
        plan.Legs.Where(leg => leg.Mode == AccessMode.Jeepney).ToList();

    private static string[] JeepneyRouteIds(JeepneyTripPlan plan) =>
        JeepneyLegs(plan).Select(leg => leg.RouteId!).ToArray();

    private static double DistanceMeters(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude) =>
        ProductionTopologyFixture.Haversine(
            (fromLatitude, fromLongitude),
            (toLatitude, toLongitude));
}
