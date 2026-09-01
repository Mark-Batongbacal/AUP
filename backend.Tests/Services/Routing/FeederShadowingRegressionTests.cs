using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

/// <summary>
/// Regression coverage for the navOptimization feeder-shadowing fix: a
/// feeder (walk/trike) must get the passenger TO transit, not replace a
/// large share of the SAME downstream jeepney corridor so they can board
/// much farther along it. Test numbers in method names/comments correspond
/// to the routing bug ticket's numbered required-coverage list.
/// </summary>
public sealed class FeederShadowingRegressionTests
{
    // TEST 1 -- exact screenshot failure: a long trike ride that eats almost
    // the entire jeepney corridor to reach a board far downstream must be
    // rejected in favor of the short, near-start board.
    [Fact]
    public async Task PlanTripsAsync_RejectsLongTricycleThatSkipsMostOfJeepneyCorridor()
    {
        var service = CreateLongCorridorService(
            corridorEndLongitude: 120.6000,
            todaLatitude: 15.0060,
            trikeSpeedMetersPerSecond: 5.6);

        var plans = await service.PlanTripsAsync(
            15.0060, 120.5000,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            var firstJeepney = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            Assert.True(
                firstJeepney.BoardLongitude < 120.5200,
                $"Expected every returned plan to board near the corridor start " +
                $"(west of 120.5200), got {firstJeepney.BoardLongitude:F6} for plan " +
                $"'{plan.RecommendationType}'.");
        }
    }

    // TEST 2 -- cross-mode comparison: an earlier WALK board and a later
    // TRIKE board on the same corridor must be compared against each other.
    // This specifically guards against reintroducing origin-access-mode into
    // the comparison grouping key.
    [Fact]
    public async Task PlanTripsAsync_ComparesWalkEarlyBoardAgainstTrikeLateBoard()
    {
        var service = CreateLongCorridorService(
            corridorEndLongitude: 120.6000,
            todaLatitude: 15.0000, // TODA sits essentially at the corridor start
            todaLongitude: 120.4995,
            trikeSpeedMetersPerSecond: 5.6,
            maxWalkAccessDistanceMeters: 250); // near board is WALK; far board can't be

        var plans = await service.PlanTripsAsync(
            15.0002, 120.5000,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            var firstJeepney = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            Assert.True(
                firstJeepney.BoardLongitude < 120.5200,
                $"A trike-served far board must not survive uncompared against the " +
                $"nearby walk board; got {firstJeepney.BoardLongitude:F6}.");
        }
    }

    // TEST 3 -- different TODA identity must not exempt a farther board from
    // comparison either.
    [Fact]
    public async Task PlanTripsAsync_ComparesAcrossDifferentTrikePointIds()
    {
        var route = BuildStraightRoute(120.5000, 120.6000);

        var earlyToda = new TricyclePoint
        {
            TricyclePointId = 1,
            PointCode = "TODA-A",
            PointName = "Near TODA",
            CenterLatitude = 15.0002,
            CenterLongitude = 120.4998,
            IsActive = true
        };
        var lateToda = new TricyclePoint
        {
            TricyclePointId = 2,
            PointCode = "TODA-B",
            PointName = "Far TODA",
            CenterLatitude = 15.0060,
            CenterLongitude = 120.5850,
            IsActive = true
        };

        var service = CreateService(
            route,
            [earlyToda, lateToda],
            new CostingAwareValhallaService(5.6),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 150,
                MaxRouteSamples = 80,
                MaxTransfers = 0,
                MaxTripOptions = 10,
                MaxCandidatesToConfirm = 200,
                MaxWalkAccessDistanceMeters = 300,
                MaxWalkToTrikePointMeters = 500,
                MaxNearbyTrikeCandidates = 4,
                MaxTotalWalkingMetersPerJourney = 2_000,
                MaxWalkOnlyTripDistanceMeters = 50,
                MaxWalkTrikeTripDistanceMeters = 50,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                FeederShadowingMinProgressMeters = 300,
                FeederShadowingAccessDistanceRatio = 0.60
            });

        var plans = await service.PlanTripsAsync(
            15.0002, 120.4998,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            var firstJeepney = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            Assert.True(
                firstJeepney.BoardLongitude < 120.5300,
                $"TODA-B's far board must be compared against TODA-A's near board " +
                $"despite differing TrikePointId, got {firstJeepney.BoardLongitude:F6}.");
        }
    }

    // TEST 4 -- an earlier board that LOOKS near on geometry but has no real
    // pedestrian/trike path must not block a genuinely reachable later board.
    // This proves rejection is never "always board earliest" -- it requires
    // an actually-accessible earlier alternative.
    [Fact]
    public async Task PlanTripsAsync_LaterBoardSurvivesWhenEarlierBoardIsInaccessible()
    {
        // The corridor's western third has no usable pedestrian path at
        // all (a river, a walled compound), and there is no TODA here, so
        // the geometrically nearest boards are genuinely unreachable by any
        // mode. Walking limits are generous enough to reach past the
        // obstacle, so a later board is the only real option.
        var service = CreateLongCorridorService(
            corridorEndLongitude: 120.6000,
            todaLatitude: 15.0060,
            trikeSpeedMetersPerSecond: 5.6,
            blockedPedestrianLongitudeRange: (120.5000, 120.5300),
            maxWalkAccessDistanceMeters: 5_000,
            includeTrikePoint: false,
            maxTotalWalkingMetersPerJourney: 6_000);

        var plans = await service.PlanTripsAsync(
            15.0060, 120.5000,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
            plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney).BoardLongitude > 120.5300);
    }

    // TEST 5 -- a legitimate network-access optimization: a slightly farther
    // board with a dramatically shorter confirmed walk must survive.
    [Fact]
    public async Task PlanTripsAsync_SlightlyFartherBoardWithMuchShorterWalkSurvives()
    {
        var route = BuildStraightRoute(120.5000, 120.5500);

        // The origin sits beside the corridor at 120.5030, so its own
        // perpendicular projection lands in the "near" zone. Pedestrian
        // access is realistic road distance rather than straight-line: that
        // nearest zone requires a long detour (2.5km), while a point only a
        // few hundred metres further along the corridor is served by a
        // direct path (400m). Everywhere else falls back to straight-line.
        double? PedestrianOverride(ValhallaLocation target) => target.Lon switch
        {
            >= 120.5000 and < 120.5060 => 2_500,
            >= 120.5060 and < 120.5110 => 400,
            _ => null
        };

        var service = CreateService(
            route,
            [],
            new CostingAwareValhallaService(5.6, PedestrianOverride),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 50,
                MaxRouteSamples = 120,
                MaxTransfers = 0,
                MaxTripOptions = 10,
                MaxCandidatesToConfirm = 200,
                MaxWalkAccessDistanceMeters = 2_600,
                MaxWalkOnlyTripDistanceMeters = 50,
                MaxWalkTrikeTripDistanceMeters = 50,
                MaxTotalWalkingMetersPerJourney = 3_000,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                // Provisional (pre-confirmation) ranking is straight-line-only
                // and cannot see that the geometrically nearest cluster turns
                // out to have a bad real pedestrian path. A wider per-route
                // quota keeps the genuinely-better, slightly-farther sample
                // from being crowded out before Valhalla can confirm it.
                MaxBoardingVariantsPerRoute = 20,
                FeederShadowingMinProgressMeters = 300,
                FeederShadowingAccessDistanceRatio = 0.60
            });

        var plans = await service.PlanTripsAsync(
            15.0020, 120.5030,
            15.0000, 120.5500);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
        {
            var board = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            return board.BoardLongitude is >= 120.5060 and < 120.5110;
        });
    }

    [Fact]
    public async Task PlanTripsAsync_ShortDownstreamBoardWithoutAccessImprovementIsRejected()
    {
        var route = BuildStraightRoute(120.5000, 120.5035);
        var toda = new TricyclePoint
        {
            TricyclePointId = 1,
            PointCode = "START-TODA",
            PointName = "Start TODA",
            CenterLatitude = 15.0000,
            CenterLongitude = 120.5000,
            IsActive = true
        };
        var service = CreateService(
            route,
            [toda],
            new CostingAwareValhallaService(5.6),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 50,
                MaxRouteSamples = 30,
                MaxTransfers = 0,
                MaxTripOptions = 10,
                MaxCandidatesToConfirm = 100,
                MaxBoardingVariantsPerRoute = 20,
                MaxWalkAccessDistanceMeters = 20,
                MaxWalkToTrikePointMeters = 100,
                MaxNearbyTrikeCandidates = 2,
                MaxTotalWalkingMetersPerJourney = 500,
                MaxWalkOnlyTripDistanceMeters = 20,
                MaxWalkTrikeTripDistanceMeters = 20,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                FeederShadowingMinProgressMeters = 300,
                FeederShadowingAccessDistanceRatio = 0.60
            });

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5035);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            var board = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            Assert.True(
                board.BoardLongitude <= 120.5011,
                $"A feeder with no confirmed distance improvement must not replace " +
                $"a short section of the same corridor; got {board.BoardLongitude:F6}.");
        });
    }

    // TEST 6 -- origin genuinely sits near the far end of a route; the only
    // realistically reachable board is a high-progress one, and it must not
    // be rejected merely for having high absolute progress. Shadowing is
    // relative to an earlier ACCESSIBLE alternative, and none exists here.
    [Fact]
    public async Task PlanTripsAsync_LateBoardSurvivesWhenNoEarlierAlternativeExists()
    {
        var service = CreateLongCorridorService(
            corridorEndLongitude: 120.6000,
            todaLatitude: 15.0060,
            trikeSpeedMetersPerSecond: 5.6,
            // Everything except the corridor's eastern sixth is unreachable:
            // the origin is realistically only near the far end.
            blockedPedestrianLongitudeRange: (120.5000, 120.5800),
            maxWalkAccessDistanceMeters: 1_500);

        var plans = await service.PlanTripsAsync(
            15.0060, 120.5900,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            var board = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            Assert.True(
                board.BoardLongitude >= 120.5800,
                $"Expected the only realistically reachable (late) board to survive, " +
                $"got {board.BoardLongitude:F6}.");
        });
    }

    // TEST 7 -- a strong shadow cannot be excused by "fastest": even a
    // materially faster total trip does not legitimize a feeder that eats
    // most of the jeepney corridor. Directly guards against reintroducing
    // the old fastest/cheapest escape hatch. (See also
    // NavigationBoardingOptimizationTests.
    // PlanTripsAsync_RejectsFartherBoardEvenWhenFastestGainIsSubstantial,
    // which covers the same invariant against the original bug's own
    // regression scenario.)
    [Fact]
    public async Task PlanTripsAsync_StrongShadowIsRejectedDespiteFasterTotalTrip()
    {
        var service = CreateLongCorridorService(
            corridorEndLongitude: 120.6000,
            todaLatitude: 15.0060,
            trikeSpeedMetersPerSecond: 6.5); // a bit faster than the jeepney

        var plans = await service.PlanTripsAsync(
            15.0060, 120.5000,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        var fastest = plans.Single(plan =>
            plan.RecommendationType.Split(',').Contains("fastest"));
        var firstJeepney = fastest.Legs.First(leg => leg.Mode == AccessMode.Jeepney);

        Assert.True(
            firstJeepney.BoardLongitude < 120.5200,
            $"Even the fastest plan must not board via a corridor-skipping trike; " +
            $"got {firstJeepney.BoardLongitude:F6}.");
    }

    // TEST 8 -- the same principle applied to a transfer: a long transfer
    // walk cannot replace most of the SECOND jeepney route either.
    [Fact]
    public async Task PlanTripsAsync_TransferCannotReplaceMostOfSecondRoute()
    {
        // Route A is short and gets the passenger to a transfer area at the
        // start of Route B. Route B is a long corridor; the transfer point
        // sits right at its start, but a much later point on Route B is only
        // reachable via an absurdly long transfer walk that eats almost all
        // of Route B's length.
        var routeA = BuildStraightRoute(
            120.4900, 120.4995, routeId: 1, routeCode: "A", routeName: "Feeder route A");
        var routeB = BuildStraightRoute(
            120.5000, 120.6000, routeId: 2, routeCode: "B", routeName: "Long corridor B");

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([routeA, routeB]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 150,
            MaxRouteSamples = 80,
            MaxTransfers = 1,
            MaxInterchangesPerRoutePair = 4,
            // Deliberately permissive: candidate generation must be able to
            // see the absurd transfer so pruning -- not a tight cap -- is
            // what rejects it.
            MaxTransferWalkMeters = 10_000,
            MaxWalkAccessDistanceMeters = 300,
            MaxWalkOnlyTripDistanceMeters = 50,
            MaxWalkTrikeTripDistanceMeters = 50,
            MaxTotalWalkingMetersPerJourney = 12_000,
            MaxCandidatesToConfirm = 200,
            MaxTripOptions = 10,
            BoardingDiversityBucketMeters = 250,
            JourneyLegContinuityToleranceMeters = 25,
            MaxStaticRouteSegmentJumpMeters = 15_000,
            FeederShadowingMinProgressMeters = 300,
            FeederShadowingAccessDistanceRatio = 0.60
        };

        var service = new RoutingService(
            new CostingAwareValhallaService(5.6),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));

        var plans = await service.PlanTripsAsync(
            15.0000, 120.4900,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            var jeepneyLegs = plan.Legs.Where(leg => leg.Mode == AccessMode.Jeepney).ToList();
            if (jeepneyLegs.Count < 2)
                continue;

            Assert.True(
                jeepneyLegs[1].BoardLongitude < 120.5300,
                $"A transfer must not replace most of route B's length; " +
                $"got second board at {jeepneyLegs[1].BoardLongitude:F6}.");
        }
    }

    // TEST 9 -- candidate survival before confirmation: a cluster of samples
    // provisionally beats the corridor start on every cheap (straight-line)
    // heuristic, but all fail real pedestrian confirmation. Unless the
    // earliest-route-progress candidate is guaranteed a generation slot, it
    // never even reaches Valhalla, and this route confirms nothing.
    [Fact]
    public async Task PlanTripsAsync_EarliestProgressCandidateReachesConfirmation_WhenMiddleClusterFailsConfirmation()
    {
        var route = BuildStraightRoute(120.5000, 120.5500);

        // Every useful interior board between 120.5100 and the destination --
        // the provisionally closest/cheapest/fastest cluster to the origin at
        // (15.0003, 120.5250) -- is unreachable on foot. The destination
        // endpoint remains reachable but cannot start a forward transit leg.
        double? PedestrianOverride(ValhallaLocation target) =>
            target.Lon >= 120.5100 && target.Lon < 120.5500
                ? double.PositiveInfinity
                : null;

        var service = CreateService(
            route,
            [],
            new CostingAwareValhallaService(5.6, PedestrianOverride),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 100,
                MaxRouteSamples = 80,
                MaxTransfers = 0,
                MaxTripOptions = 10,
                MaxCandidatesToConfirm = 200,
                MaxWalkAccessDistanceMeters = 3_000,
                MaxWalkOnlyTripDistanceMeters = 50,
                MaxWalkTrikeTripDistanceMeters = 50,
                MaxTotalWalkingMetersPerJourney = 3_500,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                FeederShadowingMinProgressMeters = 300,
                FeederShadowingAccessDistanceRatio = 0.60
            });

        var plans = await service.PlanTripsAsync(
            15.0003, 120.5250,
            15.0000, 120.5500);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan =>
        {
            var board = plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney);
            return board.BoardLongitude < 120.5100;
        });
    }

    // A candidate can be provisionally inside the walk-access cap because
    // generation only has a straight-line estimate, then confirm to a longer
    // road walk. That ineligible walk must be removed before pairwise feeder
    // shadowing: otherwise it can act as an earlier-board reference and erase
    // the only genuinely reachable tricycle-fed journey before the facade
    // later removes the walk itself.
    [Fact]
    public async Task PlanTripsAsync_OverCapConfirmedWalkCannotShadowValidTrikeAccess()
    {
        var route = BuildStraightRoute(120.5000, 120.5500);
        var origin = (Latitude: 15.0018, Longitude: 120.5000);
        var toda = new TricyclePoint
        {
            TricyclePointId = 1,
            PointCode = "ONLY-REACHABLE-TODA",
            PointName = "Only reachable TODA",
            CenterLatitude = origin.Latitude,
            CenterLongitude = origin.Longitude,
            IsActive = true
        };

        // Early route points look <300m away provisionally, but the legal
        // pedestrian path is 350m. The TODA cannot drive to those early
        // points either; its first legal route meeting is about 900m farther
        // along the corridor. Without pre-pruning access eligibility, the
        // invalid 350m walk is an apparently shorter reference and shadows
        // that valid tricycle journey.
        double? PedestrianOverride(ValhallaLocation target) =>
            Math.Abs(target.Lat - 15.0000) < 1e-6 && target.Lon < 120.5030
                ? 350
                : null;
        double? MotorizedOverride(ValhallaLocation target) =>
            Math.Abs(target.Lat - 15.0000) < 1e-6 && target.Lon < 120.5080
                ? double.PositiveInfinity
                : null;

        var service = CreateService(
            route,
            [toda],
            new CostingAwareValhallaService(
                5.6,
                PedestrianOverride,
                MotorizedOverride),
            new RoutingOptions
            {
                DefaultSampleIntervalMeters = 100,
                MaxRouteSamples = 80,
                MaxTransfers = 0,
                MaxTripOptions = 10,
                MaxCandidatesToConfirm = 200,
                MaxBoardingVariantsPerRoute = 30,
                MaxWalkAccessDistanceMeters = 300,
                MaxWalkToTrikePointMeters = 100,
                MaxNearbyTrikeCandidates = 4,
                MaxTotalWalkingMetersPerJourney = 1_000,
                MaxWalkOnlyTripDistanceMeters = 50,
                MaxWalkTrikeTripDistanceMeters = 50,
                MaxStaticRouteSegmentJumpMeters = 15_000,
                FeederShadowingMinProgressMeters = 300,
                FeederShadowingAccessDistanceRatio = 0.60
            });

        var plans = await service.PlanTripsAsync(
            origin.Latitude,
            origin.Longitude,
            15.0000,
            120.5500);
        var transitPlans = plans
            .Where(plan => plan.Legs.Any(leg => leg.Mode == AccessMode.Jeepney))
            .ToList();

        Assert.NotEmpty(transitPlans);
        Assert.DoesNotContain(
            transitPlans,
            plan => plan.OriginAccess.Mode == AccessMode.Walk);
        Assert.Contains(transitPlans, plan =>
            plan.OriginAccess.Mode == AccessMode.Trike &&
            plan.OriginAccess.TrikePointId == "ONLY-REACHABLE-TODA" &&
            plan.Legs.First(leg => leg.Mode == AccessMode.Jeepney)
                .BoardLongitude >= 120.5080);
    }

    // -----------------------------------------------------------------
    // Shared scenario builders
    // -----------------------------------------------------------------

    /// <summary>
    /// Builds an east-west corridor as a dense polyline (a point roughly
    /// every 110m), mirroring how real routes are stored. A two-point route
    /// would be useless here: with a single segment every route sample
    /// projects onto the same perpendicular foot, so the planner would only
    /// ever see ONE distinct boarding position and boarding-choice tests
    /// would pass vacuously.
    /// </summary>
    private static TransportRoute BuildStraightRoute(
        double startLongitude,
        double endLongitude,
        int routeId = 1,
        string routeCode = "CORRIDOR",
        string routeName = "Test corridor")
    {
        const double stepDegrees = 0.001; // ~110m at this latitude
        var points = new List<RoutePoint>();
        var order = 0;

        for (var longitude = startLongitude;
             longitude < endLongitude - 1e-9;
             longitude += stepDegrees)
        {
            points.Add(new RoutePoint
            {
                RouteId = routeId,
                PointOrder = order++,
                Latitude = 15.0000,
                Longitude = Math.Round(longitude, 6)
            });
        }

        points.Add(new RoutePoint
        {
            RouteId = routeId,
            PointOrder = order,
            Latitude = 15.0000,
            Longitude = endLongitude
        });

        return new TransportRoute
        {
            RouteId = routeId,
            RouteCode = routeCode,
            RouteName = routeName,
            OriginName = "Start",
            DestinationName = "End",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = points
        };
    }

    private static RoutingService CreateLongCorridorService(
        double corridorEndLongitude,
        double todaLatitude,
        double trikeSpeedMetersPerSecond,
        (double MinLongitude, double MaxLongitude)? blockedPedestrianLongitudeRange = null,
        double todaLongitude = 120.5000,
        double maxWalkAccessDistanceMeters = 100,
        bool includeTrikePoint = true,
        double maxTotalWalkingMetersPerJourney = 2_000)
    {
        var route = BuildStraightRoute(120.5000, corridorEndLongitude);

        var toda = new TricyclePoint
        {
            TricyclePointId = 1,
            PointCode = "ORIGIN-TODA",
            PointName = "Origin TODA",
            CenterLatitude = todaLatitude,
            CenterLongitude = todaLongitude,
            IsActive = true
        };

        // Only blocks targets ON the corridor (latitude ~15.0000). A TODA
        // sitting beside the corridor must stay reachable, otherwise the
        // trike alternative silently disappears too.
        double? PedestrianOverride(ValhallaLocation target)
        {
            if (blockedPedestrianLongitudeRange is not { } range)
                return null;
            if (Math.Abs(target.Lat - 15.0000) > 1e-6)
                return null;
            return target.Lon >= range.MinLongitude && target.Lon <= range.MaxLongitude
                ? double.PositiveInfinity
                : null;
        }

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 150,
            MaxRouteSamples = 80,
            MaxTransfers = 0,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 200,
            MaxWalkAccessDistanceMeters = maxWalkAccessDistanceMeters,
            NormalWalkingPreferenceAccessMeters = maxWalkAccessDistanceMeters,
            MaxWalkToTrikePointMeters = 500,
            MaxNearbyTrikeCandidates = 4,
            MaxTotalWalkingMetersPerJourney = maxTotalWalkingMetersPerJourney,
            MaxWalkOnlyTripDistanceMeters = 50,
            MaxWalkTrikeTripDistanceMeters = 50,
            MaxStaticRouteSegmentJumpMeters = 15_000,
            FeederShadowingMinProgressMeters = 300,
            FeederShadowingAccessDistanceRatio = 0.60
        };

        return CreateService(
            route,
            includeTrikePoint ? [toda] : [],
            new CostingAwareValhallaService(trikeSpeedMetersPerSecond, PedestrianOverride),
            options);
    }

    private static RoutingService CreateService(
        TransportRoute route,
        List<TricyclePoint> trikePoints,
        IValhallaService valhalla,
        RoutingOptions options)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([route]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(trikePoints);

        return new RoutingService(
            valhalla,
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }


    /// <summary>
    /// A Valhalla fake using real (non-inflated) trike speed and straight-
    /// line distance by default, with an optional pedestrian-only override
    /// keyed by target location -- returning a fixed distance to simulate a
    /// real road-network shortcut/detour, or double.PositiveInfinity to
    /// simulate a coordinate with no usable pedestrian path at all.
    /// </summary>
    private sealed class CostingAwareValhallaService(
        double trikeSpeedMetersPerSecond,
        Func<ValhallaLocation, double?>? pedestrianDistanceOverrideByTarget = null,
        Func<ValhallaLocation, double?>? motorizedDistanceOverrideByTarget = null)
        : IValhallaService
    {
        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
            ValhallaLocation source,
            IReadOnlyList<ValhallaLocation> targets,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isPedestrian = string.Equals(
                costing, "pedestrian", StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    var overrideDistance = isPedestrian
                        ? pedestrianDistanceOverrideByTarget?.Invoke(target)
                        : motorizedDistanceOverrideByTarget?.Invoke(target);

                    if (overrideDistance is { } distanceMeters)
                    {
                        if (double.IsPositiveInfinity(distanceMeters))
                        {
                            return new ValhallaMatrixResult
                            {
                                FromIndex = 0,
                                ToIndex = index,
                                Distance = null,
                                Time = null
                            };
                        }

                        return new ValhallaMatrixResult
                        {
                            FromIndex = 0,
                            ToIndex = index,
                            Distance = distanceMeters / 1_000,
                            Time = distanceMeters /
                                (isPedestrian ? 1.2 : trikeSpeedMetersPerSecond)
                        };
                    }

                    var straightLineDistance = DistanceMeters(source, target);
                    var speed = isPedestrian ? 1.2 : trikeSpeedMetersPerSecond;

                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = straightLineDistance / 1_000,
                        Time = Math.Max(1, straightLineDistance / speed)
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }

    private static double DistanceMeters(
        ValhallaLocation source,
        ValhallaLocation target) =>
        Math.Sqrt(
            Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
            Math.Pow((source.Lon - target.Lon) * 111_000, 2));
}
