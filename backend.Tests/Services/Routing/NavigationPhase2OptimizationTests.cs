using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class NavigationPhase2OptimizationTests
{
    [Fact]
    public void RoutingOptions_RejectsInvalidPhase2Tuning()
    {
        Assert.False(new RoutingOptions
        {
            BoardingDiversityBucketMeters = 0
        }.IsValid(out _));

        Assert.False(new RoutingOptions
        {
            JourneyLegContinuityToleranceMeters = 0
        }.IsValid(out _));
    }

    [Fact]
    public async Task PlanTripsAsync_ReturnedTransferPlanHasContinuousPhysicalLegs()
    {
        var service = CreateTransferService();

        var plans = await service.PlanTripsAsync(
            15.0000,
            120.5000,
            15.0100,
            120.5100);

        var plan = Assert.Single(plans.Where(candidate => candidate.TransferCount == 1));
        Assert.True(plan.Legs.Count >= 2);

        for (var index = 0; index < plan.Legs.Count - 1; index++)
        {
            var current = plan.Legs[index];
            var next = plan.Legs[index + 1];
            var gap = DistanceMeters(
                current.DestinationLatitude,
                current.DestinationLongitude,
                next.OriginLatitude,
                next.OriginLongitude);

            Assert.True(
                gap <= 25,
                $"Leg {index} -> {index + 1} has a {gap:F1}m continuity gap.");
        }
    }

    [Fact]
    public async Task GeometryEnrichment_AnchorsEveryShapeToItsPhysicalLegEndpoints()
    {
        var service = CreateTransferService();
        var plans = await service.PlanTripsAsync(
            15.0000,
            120.5000,
            15.0100,
            120.5100);
        var plan = Assert.Single(plans.Where(candidate => candidate.TransferCount == 1));

        await service.EnrichSelectedPlanGeometryAsync([plan]);

        foreach (var leg in plan.Legs)
        {
            Assert.True(leg.Geometry.Count >= 2);
            Assert.Equal(leg.OriginLatitude, leg.Geometry[0].Latitude, 7);
            Assert.Equal(leg.OriginLongitude, leg.Geometry[0].Longitude, 7);
            Assert.Equal(leg.DestinationLatitude, leg.Geometry[^1].Latitude, 7);
            Assert.Equal(leg.DestinationLongitude, leg.Geometry[^1].Longitude, 7);
        }
    }

    [Fact]
    public void ConfirmationBudget_ReservesAccessProfileWithoutLosingPhysicalDiversity()
    {
        var service = CreateSelectionService(maxCandidatesToConfirm: 20);
        var routeChain = new[] { "A", "B", "C" };
        var walkTwin = BuildSelectionCandidate(
            routeChain,
            AccessMode.Walk,
            todaId: null,
            provisionalCost: 1);
        var originTrike = BuildSelectionCandidate(
            routeChain,
            AccessMode.Trike,
            todaId: "TODA-ORIGIN",
            provisionalCost: 10_000);

        // Every ordinary objective prefers these candidates, and the walk
        // twin is also cheaper in the exact same physical boarding bucket.
        // Only bounded access-profile diversity can retain originTrike.
        var distractors = Enumerable.Range(0, 40)
            .Select(index => BuildSelectionCandidate(
                [$"DISTRACTOR-{index:D2}"],
                AccessMode.Walk,
                todaId: null,
                provisionalCost: 10 + index))
            .ToList();
        List<RoutingService.JourneyCandidate> candidates =
        [
            walkTwin,
            originTrike,
            .. distractors
        ];

        var selected = service.SelectCandidatesToConfirmWithDiversity(candidates);
        var reversed = service.SelectCandidatesToConfirmWithDiversity(
            candidates.AsEnumerable().Reverse().ToList());

        Assert.Equal(20, selected.Count);
        Assert.Contains(originTrike, selected);
        Assert.Contains(walkTwin, selected);
        Assert.Contains(selected, candidate =>
            candidate.Legs[0].RouteId.StartsWith("DISTRACTOR-", StringComparison.Ordinal));

        // Repository enumeration cannot determine which profiles survive.
        Assert.Equal(
            selected.Select(candidate => candidate.TotalGeneralizedCostPesos).Order(),
            reversed.Select(candidate => candidate.TotalGeneralizedCostPesos).Order());
    }

    [Fact]
    public void ConfirmationBudget_WalkingPreferenceChangesPreConfirmationRepresentative()
    {
        var service = CreateSelectionService(maxCandidatesToConfirm: 1);
        var lowWalking = BuildSelectionCandidate(
            ["A"],
            AccessMode.Walk,
            todaId: null,
            provisionalCost: 10);
        var walkingFriendly = BuildSelectionCandidate(
            ["A"],
            AccessMode.Walk,
            todaId: null,
            provisionalCost: 11,
            alightProgressOffsetMeters: 1) with
        {
            OriginAccess = lowWalking.OriginAccess with
            {
                WalkDistanceMeters = 1_000
            }
        };

        var ordinary = service.SelectCandidatesToConfirmWithDiversity(
            [lowWalking, walkingFriendly]);
        var prefersMoreWalking = service.SelectCandidatesToConfirmWithDiversity(
            [lowWalking, walkingFriendly],
            new JourneyPlanningPreferences(
                WalkingPreference: JourneyWalkingPreference.More));

        Assert.Same(lowWalking, Assert.Single(ordinary));
        Assert.Same(walkingFriendly, Assert.Single(prefersMoreWalking));
    }

    [Fact]
    public void BoardingDiversity_KeepsDistinctPhysicalRegionFromSameProgressBucket()
    {
        var service = CreateSelectionService(maxCandidatesToConfirm: 20);
        var wrongFirst = BuildConnectionCandidate(
            15.118993, 120.569791, 2_834.725, 1);
        var expectedRegion = BuildConnectionCandidate(
            15.117495, 120.568805, 3_032.020, 50);
        var wrongRetraced = BuildConnectionCandidate(
            15.118993, 120.569791, 3_229.315, 2);

        var representatives = service.SelectPhysicalBoardingRepresentatives(
            [wrongFirst, expectedRegion, wrongRetraced]);

        Assert.Equal(2, representatives.Count);
        Assert.Contains(representatives, candidate =>
            candidate.BoardAccess.Anchor.Latitude ==
                expectedRegion.BoardAccess.Anchor.Latitude &&
            candidate.BoardAccess.Anchor.Longitude ==
                expectedRegion.BoardAccess.Anchor.Longitude);
    }

    [Fact]
    public void ConfirmationBudget_AccessProfileKeepsDistinctTransitOccurrences()
    {
        var service = CreateSelectionService(maxCandidatesToConfirm: 20);
        var routeChain = new[] { "A", "B", "C" };
        var firstOccurrence = BuildSelectionCandidate(
            routeChain,
            AccessMode.Trike,
            todaId: "TODA-ORIGIN",
            provisionalCost: 10_000,
            alightProgressOffsetMeters: 0);
        var downstreamOccurrence = BuildSelectionCandidate(
            routeChain,
            AccessMode.Trike,
            todaId: "TODA-ORIGIN",
            provisionalCost: 10_001,
            alightProgressOffsetMeters: 500);

        // These candidates intentionally share route IDs, access mode/TODA,
        // and every boarding bucket. Only their authoritative alighting /
        // transfer occurrences differ. Cheap walk distractors consume every
        // ordinary objective and the physical-board reservation, so both can
        // survive only when access-profile diversity retains that occurrence.
        var distractors = Enumerable.Range(0, 40)
            .Select(index => BuildSelectionCandidate(
                [$"DISTRACTOR-{index:D2}"],
                AccessMode.Walk,
                todaId: null,
                provisionalCost: 10 + index))
            .ToList();
        List<RoutingService.JourneyCandidate> candidates =
        [
            firstOccurrence,
            downstreamOccurrence,
            .. distractors
        ];

        var selected = service.SelectCandidatesToConfirmWithDiversity(candidates);
        var reversed = service.SelectCandidatesToConfirmWithDiversity(
            candidates.AsEnumerable().Reverse().ToList());

        Assert.Contains(firstOccurrence, selected);
        Assert.Contains(downstreamOccurrence, selected);
        Assert.Equal(
            selected.Select(candidate => candidate.TotalGeneralizedCostPesos).Order(),
            reversed.Select(candidate => candidate.TotalGeneralizedCostPesos).Order());
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("one-transfer")]
    [InlineData("two-transfer")]
    [InlineData("loop-self-transfer")]
    [InlineData("tricycle-access")]
    [InlineData("preference-fastest-less-walking")]
    [InlineData("preference-cheapest-more-walking")]
    [InlineData("preference-efficient")]
    public void OptimizedSelection_MatchesReferenceCandidateKeysInExactOrder(
        string scenario)
    {
        var service = CreateSelectionService(maxCandidatesToConfirm: 20);
        var (candidates, preferences) = BuildSelectionParityScenario(scenario);

        var reference = service.SelectCandidatesToConfirmWithDiversityReference(
            candidates,
            preferences);
        var optimized = service.SelectCandidatesToConfirmWithDiversity(
            candidates,
            preferences);

        var referenceKeys = reference
            .Select(RoutingService.GetJourneyCandidateSelectionKey)
            .ToList();
        var optimizedKeys = optimized
            .Select(RoutingService.GetJourneyCandidateSelectionKey)
            .ToList();

        Assert.Equal(referenceKeys, optimizedKeys);
    }

    private static (List<RoutingService.JourneyCandidate> Candidates,
        JourneyPlanningPreferences? Preferences)
        BuildSelectionParityScenario(string scenario)
    {
        JourneyPlanningPreferences? preferences = scenario switch
        {
            "preference-fastest-less-walking" => new JourneyPlanningPreferences(
                MaxFarePesos: 150,
                MaxWalkingMeters: 2_000,
                WalkingPreference: JourneyWalkingPreference.Less,
                OptimizationPreference: JourneyOptimizationPreference.Fastest),
            "preference-cheapest-more-walking" => new JourneyPlanningPreferences(
                MaxFarePesos: 150,
                MaxWalkingMeters: 2_000,
                WalkingPreference: JourneyWalkingPreference.More,
                OptimizationPreference: JourneyOptimizationPreference.Cheapest),
            "preference-efficient" => new JourneyPlanningPreferences(
                MaxFarePesos: 150,
                MaxWalkingMeters: 2_000,
                OptimizationPreference: JourneyOptimizationPreference.Efficient),
            _ => null
        };

        var candidates = Enumerable.Range(0, 80)
            .Select(index =>
            {
                IReadOnlyList<string> routeIds = scenario switch
                {
                    "direct" => [$"DIRECT-{index % 9:D2}"],
                    "one-transfer" =>
                        [$"ONE-A-{index % 7:D2}", $"ONE-B-{index % 5:D2}"],
                    "two-transfer" =>
                    [
                        $"TWO-A-{index % 7:D2}",
                        $"TWO-B-{index % 5:D2}",
                        $"TWO-C-{index % 3:D2}"
                    ],
                    "loop-self-transfer" =>
                        [$"LOOP-{index % 4:D2}", $"LOOP-{index % 4:D2}"],
                    "tricycle-access" =>
                        [$"TRIKE-A-{index % 6:D2}", $"TRIKE-B-{index % 4:D2}"],
                    "preference-fastest-less-walking" or
                    "preference-cheapest-more-walking" or
                    "preference-efficient" =>
                        [$"PREF-A-{index % 8:D2}", $"PREF-B-{index % 6:D2}"],
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(scenario),
                        scenario,
                        "Unknown selection parity scenario")
                };

                var usesTrike = scenario == "tricycle-access" && index % 3 != 0;
                return BuildSelectionCandidate(
                    routeIds,
                    usesTrike ? AccessMode.Trike : AccessMode.Walk,
                    usesTrike ? $"TODA-{index % 5:D2}" : null,
                    provisionalCost: 10 + index % 11,
                    alightProgressOffsetMeters: index * 7.25);
            })
            .ToList();

        return (candidates, preferences);
    }

    private static RoutingService CreateTransferService()
    {
        var routes = new List<TransportRoute>
        {
            BuildRoute(
                1,
                "A",
                "West-East",
                [(15.0000, 120.5000), (15.0000, 120.5100)]),
            BuildRoute(
                2,
                "B",
                "South-North",
                [(15.0000, 120.5100), (15.0100, 120.5100)])
        };

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 100,
            MaxRouteSamples = 50,
            MaxTransfers = 1,
            MaxInterchangesPerRoutePair = 4,
            MaxTransferWalkMeters = 100,
            MaxWalkAccessDistanceMeters = 50,
            MaxTotalWalkingMetersPerJourney = 500,
            MaxWalkOnlyTripDistanceMeters = 20,
            MaxWalkTrikeTripDistanceMeters = 20,
            MaxCandidatesToConfirm = 50,
            MaxTripOptions = 10,
            BoardingDiversityBucketMeters = 250,
            JourneyLegContinuityToleranceMeters = 25
        };

        return new RoutingService(
            new StraightLineValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private static RoutingService CreateSelectionService(
        int maxCandidatesToConfirm)
    {
        var routeRepository = new Mock<ITransportRouteRepository>();
        var tricycleRepository = new Mock<ITricyclePointRepository>();
        return new RoutingService(
            new StraightLineValhallaService(),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions
            {
                MaxCandidatesToConfirm = maxCandidatesToConfirm,
                BoardingDiversityBucketMeters = 250
            }));
    }

    private static RoutingService.JourneyCandidate BuildSelectionCandidate(
        IReadOnlyList<string> routeIds,
        AccessMode originMode,
        string? todaId,
        double provisionalCost,
        double alightProgressOffsetMeters = 0)
    {
        var legs = routeIds.Select((routeId, index) =>
        {
            var boardProgress = 1_000.0 + index * 2_000;
            var alightProgress = boardProgress + 1_000 + alightProgressOffsetMeters;
            var board = (Latitude: 15.0 + index * 0.001, Longitude: 120.5);
            var alight = (Latitude: board.Latitude + 0.0005, Longitude: 120.5);
            return new RoutingService.JourneyLegCandidate(
                routeId,
                routeId,
                board,
                alight,
                BoardFullRouteAnchor: new RoutingService.RouteAnchor(
                    routeId, 0, 0, board.Latitude, board.Longitude, boardProgress),
                AlightFullRouteAnchor: new RoutingService.RouteAnchor(
                    routeId, 0, 1, alight.Latitude, alight.Longitude, alightProgress));
        }).ToList();

        var firstBoard = legs[0].Board;
        var lastAlight = legs[^1].Alight;
        var origin = BuildAccess(originMode, todaId, firstBoard);
        var destination = BuildAccess(AccessMode.Walk, null, lastAlight);
        return new RoutingService.JourneyCandidate(
            legs,
            origin,
            destination,
            [],
            provisionalCost);
    }

    private static RoutingService.RouteConnectionCandidate
        BuildConnectionCandidate(
            double latitude,
            double longitude,
            double progress,
            double cost) =>
        new(
            "R",
            "R",
            BuildAccess(AccessMode.Walk, null, (latitude, longitude)) with
            {
                FullRouteAnchor = new RoutingService.RouteAnchor(
                    "R", 0, 0, latitude, longitude, progress)
            },
            BuildAccess(AccessMode.Walk, null, (15.2, 120.6)) with
            {
                FullRouteAnchor = new RoutingService.RouteAnchor(
                    "R", 1, 0, 15.2, 120.6, progress + 1_000)
            },
            0,
            1,
            cost);

    private static RoutingService.AccessCandidate BuildAccess(
        AccessMode mode,
        string? todaId,
        (double Latitude, double Longitude) anchor)
    {
        var trikePoint = mode == AccessMode.Trike
            ? new TrikePoint(
                todaId!,
                todaId!,
                anchor.Latitude - 0.01,
                anchor.Longitude)
            : null;
        return new RoutingService.AccessCandidate(
            mode,
            anchor,
            WalkDistanceMeters: mode == AccessMode.Trike ? 1_000 : 1,
            WalkTimeSeconds: mode == AccessMode.Trike ? 1_000 : 1,
            trikePoint,
            TrikeRideDistanceMeters: mode == AccessMode.Trike ? 10_000 : null,
            TrikeRideTimeSeconds: mode == AccessMode.Trike ? 10_000 : null,
            TrikeFarePesos: mode == AccessMode.Trike ? 50 : null,
            ValueOfTimePesosPerMinute: 2,
            WalkingFatiguePesosPerKilometer: 3);
    }

    private static TransportRoute BuildRoute(
        int id,
        string code,
        string name,
        IReadOnlyList<(double Latitude, double Longitude)> coordinates) =>
        new()
        {
            RouteId = id,
            RouteCode = code,
            RouteName = name,
            OriginName = "Origin",
            DestinationName = "Destination",
            IsActive = true,
            TransportMode = new TransportMode
            {
                Code = "JEEPNEY",
                Name = "Jeepney"
            },
            RoutePoints = coordinates
                .Select((coordinate, index) => new RoutePoint
                {
                    RouteId = id,
                    PointOrder = index,
                    Latitude = coordinate.Latitude,
                    Longitude = coordinate.Longitude
                })
                .ToList()
        };

    private static double DistanceMeters(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude) =>
        Math.Sqrt(
            Math.Pow((fromLatitude - toLatitude) * 111_000, 2) +
            Math.Pow((fromLongitude - toLongitude) * 111_000, 2));

    private sealed class StraightLineValhallaService : IValhallaService
    {
        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Legs =
                    [
                        new ValhallaLeg
                        {
                            Points =
                            [
                                [startLongitude, startLatitude],
                                [endLongitude, endLatitude]
                            ]
                        }
                    ]
                }
            });
        }

        public Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
            ValhallaLocation source,
            IReadOnlyList<ValhallaLocation> targets,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    var distance = DistanceMeters(
                        source.Lat,
                        source.Lon,
                        target.Lat,
                        target.Lon);
                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = distance / 1_000,
                        Time = Math.Max(1, distance / 1.2)
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }
}
