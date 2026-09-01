using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Regression coverage for the A -> B -> C quality rule. These tests build
/// already-confirmed candidates so the assertions exercise the exact pruning
/// boundary: routed transfer distances, route occurrences and preference
/// scoring are all known at this point.
/// </summary>
public sealed class RedundantIntermediateTransitRegressionTests
{
    [Fact]
    public async Task PlanTripsAsync_PrunesObservedThreeJeepStructureEndToEnd()
    {
        var service = CreateCorridorService(
            middleStartLongitude: 120.5200,
            middleEndLongitude: 120.5220);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5001,
            15.0000, 120.5399);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
            PlanRouteIds(plan).SequenceEqual(["A", "C"]));
        Assert.DoesNotContain(plans, plan =>
            PlanRouteIds(plan).SequenceEqual(["A", "B", "C"]));
    }

    [Fact]
    public async Task PlanTripsAsync_RetainsNecessaryThreeJeepStructureEndToEnd()
    {
        var service = CreateCorridorService(
            middleStartLongitude: 120.5200,
            middleEndLongitude: 120.5260);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5001,
            15.0000, 120.5399);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
            PlanRouteIds(plan).SequenceEqual(["A", "B", "C"]));
    }

    [Fact]
    public void ObservedBadJourney_PrefersConfirmedDirectWalkingBypass()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 1_600,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_200,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 160,
            middleRideSeconds: 325,
            bypassWalkMeters: 210);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            preferences: null);

        Assert.DoesNotContain(original, result);
        Assert.Contains(bypass, result);
        Assert.DoesNotContain(result, candidate =>
            RouteIds(candidate).SequenceEqual(["A", "B", "C"]));
    }

    [Fact]
    public void NecessaryShortConnector_RemainsWhenDirectWalkExceedsLimit()
    {
        var service = CreateService(maxTransferWalkMeters: 400);
        var (original, invalidBypass) = BuildScenario(
            originalTime: 1_000,
            originalFare: 39,
            originalCost: 70,
            bypassTime: 1_500,
            bypassFare: 26,
            bypassCost: 75,
            middleRideMeters: 600,
            middleRideSeconds: 390,
            bypassWalkMeters: 600);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, invalidBypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Cheapest));

        Assert.Contains(original, result);
    }

    [Fact]
    public void LongIntermediateRide_IsPrunedWhenSameConfirmedBypassDominates()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 2_200,
            originalFare: 39,
            originalCost: 105,
            bypassTime: 1_200,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 3_000,
            middleRideSeconds: 900,
            bypassWalkMeters: 210);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Efficient));

        Assert.DoesNotContain(original, result);
    }

    [Fact]
    public void Fastest_KeepsShortConnectorWithMeaningfulTimeAdvantage()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 800,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_100,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 300,
            middleRideSeconds: 75,
            bypassWalkMeters: 350);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Fastest));

        Assert.Contains(original, result);
    }

    [Fact]
    public void Cheapest_PrefersValidFareFreeBypassForMiddleLeg()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 800,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_100,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 300,
            middleRideSeconds: 75,
            bypassWalkMeters: 350);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Cheapest));

        Assert.DoesNotContain(original, result);
    }

    [Fact]
    public void Efficient_UsesExistingGeneralizedCostComparison()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 800,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_100,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 300,
            middleRideSeconds: 75,
            bypassWalkMeters: 350);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Efficient));

        Assert.DoesNotContain(original, result);
    }

    [Fact]
    public void GenericMultiObjectiveRequest_KeepsFasterConnector()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 800,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_100,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 300,
            middleRideSeconds: 75,
            bypassWalkMeters: 350);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            preferences: null);

        Assert.Contains(original, result);
    }

    [Fact]
    public void LessWalkingPreference_KeepsConnectorWhenExistingScorePrefersIt()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 800,
            originalFare: 39,
            originalCost: 60,
            bypassTime: 1_100,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 300,
            middleRideSeconds: 75,
            bypassWalkMeters: 350);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                WalkingPreference: JourneyWalkingPreference.Less));

        Assert.Contains(original, result);
    }

    [Fact]
    public void SameRoutesAtDifferentOccurrences_DoNotReuseBypass()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 1_600,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_200,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 160,
            middleRideSeconds: 325,
            bypassWalkMeters: 210,
            bypassNextBoardProgressOffset: 2_000,
            keepNextBoardCoordinate: true);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            preferences: null);

        Assert.Contains(original, result);
    }

    [Fact]
    public void SelfTransferAndRepeatedRouteOccurrences_RemainDistinct()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 1_600,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_200,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 160,
            middleRideSeconds: 325,
            bypassWalkMeters: 210,
            nextRouteId: "A",
            bypassNextBoardProgressOffset: 4_000,
            keepNextBoardCoordinate: true);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            preferences: null);

        Assert.Contains(original, result);
    }

    [Fact]
    public void DifferentTodaAccessIdentity_DoesNotSupplyBypassReference()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            originalTime: 1_600,
            originalFare: 39,
            originalCost: 80,
            bypassTime: 1_200,
            bypassFare: 26,
            bypassCost: 60,
            middleRideMeters: 160,
            middleRideSeconds: 325,
            bypassWalkMeters: 210,
            originalTodaId: "TODA-A",
            bypassTodaId: "TODA-B");

        var result = service.PruneRedundantIntermediateTransitLegs(
            [original, bypass],
            new JourneyPlanningPreferences(
                OptimizationPreference: JourneyOptimizationPreference.Cheapest));

        Assert.Contains(original, result);
    }

    [Fact]
    public void DirectAndOneTransferJourneys_AreOutsideTheRule()
    {
        var service = CreateService();
        var direct = BuildConfirmed(
            [BuildLeg("A", 0, 1_000, 15.000, 120.500, 15.000, 120.510)],
            [], 600, 13, 30);
        var transfer = BuildConfirmed(
            [
                BuildLeg("A", 0, 1_000, 15.000, 120.500, 15.000, 120.510),
                BuildLeg("C", 0, 1_000, 15.000, 120.512, 15.000, 120.530)
            ],
            [210], 1_200, 26, 60);

        var result = service.PruneRedundantIntermediateTransitLegs(
            [direct, transfer],
            preferences: null);

        Assert.Equal([direct, transfer], result);
    }

    [Fact]
    public void SurvivorOrdering_RemainsDeterministic()
    {
        var service = CreateService();
        var direct = BuildConfirmed(
            [BuildLeg("D", 0, 1_000, 15.010, 120.500, 15.010, 120.510)],
            [], 500, 13, 25);
        var (original, bypass) = BuildScenario(
            1_600, 39, 80,
            1_200, 26, 60,
            160, 325, 210);

        var first = service.PruneRedundantIntermediateTransitLegs(
            [direct, original, bypass],
            preferences: null);
        var second = service.PruneRedundantIntermediateTransitLegs(
            [direct, original, bypass],
            preferences: null);

        Assert.Equal([direct, bypass], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ConcurrentCalls_KeepRequestLocalComparisonState()
    {
        var service = CreateService();
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
        {
            var (original, bypass) = BuildScenario(
                1_600, 39, 80,
                1_200, 26, 60,
                160, 325, 210);
            var result = service.PruneRedundantIntermediateTransitLegs(
                [original, bypass],
                preferences: null);
            Assert.DoesNotContain(original, result);
            Assert.Contains(bypass, result);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Cancellation_StopsComparison()
    {
        var service = CreateService();
        var (original, bypass) = BuildScenario(
            1_600, 39, 80,
            1_200, 26, 60,
            160, 325, 210);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            service.PruneRedundantIntermediateTransitLegs(
                [original, bypass],
                preferences: null,
                cancellation.Token));
    }

    private static (
        RoutingService.ConfirmedJourneyCandidate Original,
        RoutingService.ConfirmedJourneyCandidate Bypass) BuildScenario(
        double originalTime,
        double originalFare,
        double originalCost,
        double bypassTime,
        double bypassFare,
        double bypassCost,
        double middleRideMeters,
        double middleRideSeconds,
        double bypassWalkMeters,
        string nextRouteId = "C",
        double bypassNextBoardProgressOffset = 0,
        bool keepNextBoardCoordinate = false,
        string? originalTodaId = null,
        string? bypassTodaId = null)
    {
        var first = BuildLeg(
            "A", 0, 2_000,
            15.000, 120.500,
            15.000, 120.510);
        var middle = BuildLeg(
            "B", 0, middleRideMeters,
            15.000, 120.5101,
            15.000, 120.5120);
        var next = BuildLeg(
            nextRouteId, 1_000, 5_000,
            15.000, 120.5121,
            15.000, 120.540);
        var bypassNext = BuildLeg(
            nextRouteId,
            1_000 + bypassNextBoardProgressOffset,
            5_000,
            15.000,
            keepNextBoardCoordinate ? 120.5121 :
                120.5121 + bypassNextBoardProgressOffset / 100_000,
            15.000,
            120.540);

        var original = BuildConfirmed(
            [first, middle, next],
            [10, 10],
            originalTime,
            originalFare,
            originalCost,
            middleRideMeters,
            middleRideSeconds,
            originalTodaId);
        var bypass = BuildConfirmed(
            [first, bypassNext],
            [bypassWalkMeters],
            bypassTime,
            bypassFare,
            bypassCost,
            todaId: bypassTodaId);

        return (original, bypass);
    }

    private static RoutingService.ConfirmedJourneyCandidate BuildConfirmed(
        List<RoutingService.JourneyLegCandidate> candidateLegs,
        List<double> transferWalks,
        double totalTime,
        double totalFare,
        double totalCost,
        double middleRideMeters = 0,
        double middleRideSeconds = 0,
        string? todaId = null)
    {
        var originAccess = BuildAccess(candidateLegs[0].Board, todaId);
        var destinationAccess = BuildAccess(candidateLegs[^1].Alight, null);
        var transferSegments = new List<RoutingService.WalkSegmentCandidate>();
        for (var index = 0; index < candidateLegs.Count - 1; index++)
        {
            transferSegments.Add(new RoutingService.WalkSegmentCandidate(
                candidateLegs[index].Alight,
                candidateLegs[index + 1].Board,
                transferWalks[index]));
        }

        var candidate = new RoutingService.JourneyCandidate(
            candidateLegs,
            originAccess,
            destinationAccess,
            transferSegments,
            totalCost);

        var planLegs = new List<JeepneyTripLeg>();
        for (var index = 0; index < candidateLegs.Count; index++)
        {
            var candidateLeg = candidateLegs[index];
            var isMiddle = candidateLegs.Count >= 3 && index == 1;
            planLegs.Add(new JeepneyTripLeg
            {
                Mode = AccessMode.Jeepney,
                RouteId = candidateLeg.RouteId,
                RouteName = candidateLeg.RouteName,
                OriginLatitude = candidateLeg.Board.Latitude,
                OriginLongitude = candidateLeg.Board.Longitude,
                DestinationLatitude = candidateLeg.Alight.Latitude,
                DestinationLongitude = candidateLeg.Alight.Longitude,
                BoardLatitude = candidateLeg.Board.Latitude,
                BoardLongitude = candidateLeg.Board.Longitude,
                AlightLatitude = candidateLeg.Alight.Latitude,
                AlightLongitude = candidateLeg.Alight.Longitude,
                DistanceMeters = isMiddle ? middleRideMeters : 2_000,
                DurationSeconds = isMiddle ? middleRideSeconds : 600,
                FarePesos = 13,
                GeneralizedCostPesos = isMiddle ? 20 : 30,
                BoardRouteProgressMeters =
                    candidateLeg.BoardFullRouteAnchor!.DistanceFromRouteStartMeters,
                AlightRouteProgressMeters =
                    candidateLeg.AlightFullRouteAnchor!.DistanceFromRouteStartMeters
            });

            if (index < transferWalks.Count)
            {
                planLegs.Add(new JeepneyTripLeg
                {
                    Mode = AccessMode.Walk,
                    OriginLatitude = candidateLeg.Alight.Latitude,
                    OriginLongitude = candidateLeg.Alight.Longitude,
                    DestinationLatitude = candidateLegs[index + 1].Board.Latitude,
                    DestinationLongitude = candidateLegs[index + 1].Board.Longitude,
                    DistanceMeters = transferWalks[index],
                    DurationSeconds = transferWalks[index] / 1.2,
                    GeneralizedCostPesos = transferWalks[index] / 500
                });
            }
        }

        var plan = new JeepneyTripPlan
        {
            Legs = planLegs,
            OriginAccess = BuildPlanAccess(todaId),
            DestinationAccess = BuildPlanAccess(null),
            TransferWalkDistancesMeters = transferWalks,
            TransferWalkTimesSeconds = transferWalks
                .Select(distance => distance / 1.2)
                .ToList(),
            TotalTimeSeconds = totalTime,
            TotalFarePesos = totalFare,
            GeneralizedCostPesos = totalCost
        };

        return new RoutingService.ConfirmedJourneyCandidate(candidate, plan);
    }

    private static RoutingService.JourneyLegCandidate BuildLeg(
        string routeId,
        double boardProgress,
        double alightProgress,
        double boardLatitude,
        double boardLongitude,
        double alightLatitude,
        double alightLongitude)
    {
        var board = (Latitude: boardLatitude, Longitude: boardLongitude);
        var alight = (Latitude: alightLatitude, Longitude: alightLongitude);
        return new RoutingService.JourneyLegCandidate(
            routeId,
            routeId,
            board,
            alight,
            BoardFullRouteAnchor: new RoutingService.RouteAnchor(
                routeId, 0, 0, boardLatitude, boardLongitude, boardProgress),
            AlightFullRouteAnchor: new RoutingService.RouteAnchor(
                routeId, 1, 0, alightLatitude, alightLongitude, alightProgress));
    }

    private static RoutingService.AccessCandidate BuildAccess(
        (double Latitude, double Longitude) anchor,
        string? todaId)
    {
        var trikePoint = todaId is null
            ? null
            : new TrikePoint(todaId, todaId, anchor.Latitude, anchor.Longitude);
        return new RoutingService.AccessCandidate(
            todaId is null ? AccessMode.Walk : AccessMode.Trike,
            anchor,
            WalkDistanceMeters: 10,
            WalkTimeSeconds: 8,
            trikePoint,
            TrikeRideDistanceMeters: todaId is null ? null : 500,
            TrikeRideTimeSeconds: todaId is null ? null : 90,
            TrikeFarePesos: todaId is null ? null : 35,
            ValueOfTimePesosPerMinute: 2,
            WalkingFatiguePesosPerKilometer: 4);
    }

    private static JeepneyAccessSegment BuildPlanAccess(string? todaId) => new()
    {
        Mode = todaId is null ? AccessMode.Walk : AccessMode.Trike,
        TrikePointId = todaId,
        WalkDistanceMeters = 10,
        WalkTimeSeconds = 8,
        TotalTimeSeconds = todaId is null ? 8 : 98,
        TotalFarePesos = todaId is null ? 0 : 35,
        GeneralizedCostPesos = todaId is null ? 0.3 : 38
    };

    private static RoutingService CreateService(
        double maxTransferWalkMeters = 400)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var tricycleRepository = new Mock<ITricyclePointRepository>();
        return new RoutingService(
            new RoadNetworkValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions
            {
                MaxTransferWalkMeters = maxTransferWalkMeters
            }));
    }

    private static RoutingService CreateCorridorService(
        double middleStartLongitude,
        double middleEndLongitude)
    {
        var routes = new List<TransportRoute>
        {
            ProductionTopologyFixture.BuildDenseRoute(
                1,
                "A",
                "First corridor",
                [(15.0000, 120.5000), (15.0000, middleStartLongitude)]),
            ProductionTopologyFixture.BuildDenseRoute(
                2,
                "B",
                "Intermediate connector",
                [
                    (15.0000, middleStartLongitude),
                    (15.0000, middleEndLongitude)
                ]),
            ProductionTopologyFixture.BuildDenseRoute(
                3,
                "C",
                "Final corridor",
                [(15.0000, middleEndLongitude), (15.0000, 120.5400)])
        };
        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 75,
            MaxRouteSamples = 200,
            MaxTransfers = 2,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 300,
            // One exact endpoint interchange per pair keeps the fixture about
            // the A-alight/C-board bypass rather than alternate downstream
            // boarding regions on the synthetic straight route.
            MaxInterchangesPerRoutePair = 1,
            MaxTransferWalkMeters = 400,
            MaxWalkAccessDistanceMeters = 150,
            MaxTotalWalkingMetersPerJourney = 2_500,
            MaxWalkOnlyTripDistanceMeters = 100,
            MaxWalkTrikeTripDistanceMeters = 100,
            MaxStaticRouteSegmentJumpMeters = 15_000,
            MaxBoardingVariantsPerRoute = 8
        };

        return ProductionTopologyFixture.CreateService(
            options,
            new RoadNetworkValhallaService(),
            routes,
            []);
    }

    private static string[] RouteIds(
        RoutingService.ConfirmedJourneyCandidate candidate) =>
        candidate.Candidate.Legs.Select(leg => leg.RouteId).ToArray();

    private static string[] PlanRouteIds(JeepneyTripPlan plan) =>
        plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Select(leg => leg.RouteId!)
            .ToArray();
}
