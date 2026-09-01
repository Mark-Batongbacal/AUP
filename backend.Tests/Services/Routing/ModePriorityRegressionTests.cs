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
/// Regression coverage for jeepney-first mode semantics: the jeepney is the
/// PRIMARY corridor mode, while walking and tricycles are feeder/access
/// modes. The default (efficient) recommendation must be built around a
/// practical jeepney corridor when one exists -- without ever forcing a
/// jeepney onto trips where it makes no sense, and without dishonestly
/// reshaping the pure Fastest/Cheapest objectives.
/// Test numbers correspond to the extension ticket's required-coverage list.
/// </summary>
public sealed class ModePriorityRegressionTests
{
    // TEST 1 -- a short local hop where the jeepney is genuinely awkward:
    // both ends sit off the corridor, so riding it means walking down to the
    // route, waiting, riding a small section, then walking back up. A direct
    // tricycle is clearly the sensible local journey and must be allowed to
    // be the recommendation.
    [Fact]
    public async Task PlanTripsAsync_ShortLocalTrip_AllowsDirectTrikeAsDefault()
    {
        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 5_000,
            maxWalkAccessDistanceMeters: 500);

        var plans = await service.PlanTripsAsync(
            15.0030, 120.5000,  // ~330m north of the corridor
            15.0030, 120.5140); // ~1.5 km away, also off-corridor

        Assert.NotEmpty(plans);
        var efficient = SinglePlanFor(plans, "efficient");

        Assert.DoesNotContain(efficient.Legs, leg => leg.Mode == AccessMode.Jeepney);
    }

    // TEST 2 + TEST 6 -- a medium/long trip where a practical jeepney
    // corridor exists and a direct tricycle is somewhat faster only because
    // it dodges jeepney boarding wait. The default must stay jeepney-based;
    // Fastest and Cheapest stay honest.
    [Fact]
    public async Task PlanTripsAsync_LongTripWithGoodJeepney_KeepsJeepneyAsDefault()
    {
        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 20_000,
            trikeSpeedMetersPerSecond: 9.0); // genuinely quicker than the jeepney

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5900); // ~9.7 km along the corridor

        Assert.NotEmpty(plans);

        var efficient = SinglePlanFor(plans, "efficient");
        Assert.Contains(efficient.Legs, leg => leg.Mode == AccessMode.Jeepney);

        var cheapest = SinglePlanFor(plans, "cheapest");
        Assert.Contains(cheapest.Legs, leg => leg.Mode == AccessMode.Jeepney);
    }

    // TEST 3 -- no jeepney route serves this corridor at all, so the direct
    // tricycle is free to become the default.
    [Fact]
    public async Task PlanTripsAsync_LongTripWithNoPracticalJeepney_AllowsDirectTrikeDefault()
    {
        // The only jeepney route runs far to the north and is useless here.
        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 20_000,
            corridorLatitude: 15.2000);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5900);

        Assert.NotEmpty(plans);
        var efficient = SinglePlanFor(plans, "efficient");

        Assert.DoesNotContain(efficient.Legs, leg => leg.Mode == AccessMode.Jeepney);
    }

    // TEST 4 -- the healthy feeder shape: a short tricycle hop onto a long
    // jeepney corridor is a valid, and preferred, journey.
    [Fact]
    public async Task PlanTripsAsync_ShortTrikeFeederOntoLongJeepney_IsValidAndPreferred()
    {
        var toda = BuildToda(1, "ORIGIN-TODA", 15.0040, 120.5000);

        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 20_000,
            trikePoints: [toda],
            // Too far to walk to the corridor, so the short trike hop is the
            // only way to reach it.
            maxWalkAccessDistanceMeters: 200,
            maxWalkToTrikePointMeters: 600);

        var plans = await service.PlanTripsAsync(
            15.0045, 120.5000,
            15.0000, 120.5900);

        Assert.NotEmpty(plans);
        var efficient = SinglePlanFor(plans, "efficient");

        var jeepneyDistance = efficient.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Sum(leg => leg.DistanceMeters);
        var trikeDistance = efficient.Legs
            .Where(leg => leg.Mode == AccessMode.Trike)
            .Sum(leg => leg.DistanceMeters);

        Assert.True(
            jeepneyDistance > 5_000,
            $"Expected the jeepney to carry the corridor, got {jeepneyDistance:F0}m.");
        Assert.True(
            trikeDistance < jeepneyDistance,
            $"Trike ({trikeDistance:F0}m) must stay a feeder, not out-cover the " +
            $"jeepney ({jeepneyDistance:F0}m).");
    }

    // TEST 7 -- Fastest must remain honest. When the tricycle genuinely is
    // the quickest option, Fastest may still report it even though the
    // default recommendation is jeepney-based.
    [Fact]
    public async Task PlanTripsAsync_FastestMayStillReturnDirectTrike()
    {
        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 20_000,
            trikeSpeedMetersPerSecond: 14.0); // decisively faster than the jeepney

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5900);

        Assert.NotEmpty(plans);

        var fastest = SinglePlanFor(plans, "fastest");
        Assert.DoesNotContain(fastest.Legs, leg => leg.Mode == AccessMode.Jeepney);

        // ...while the default still builds around the corridor.
        var efficient = SinglePlanFor(plans, "efficient");
        Assert.Contains(efficient.Legs, leg => leg.Mode == AccessMode.Jeepney);
    }

    // TEST 8 -- Cheapest must remain honest, and the jeepney's flat fare
    // should win it outright on a long corridor.
    [Fact]
    public async Task PlanTripsAsync_CheapestReturnsJeepneyJourney()
    {
        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 20_000);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5900);

        Assert.NotEmpty(plans);
        var cheapest = SinglePlanFor(plans, "cheapest");

        Assert.Contains(cheapest.Legs, leg => leg.Mode == AccessMode.Jeepney);

        var directTrikePlans = plans
            .Where(plan => plan.Legs.All(leg => leg.Mode != AccessMode.Jeepney))
            .ToList();
        Assert.All(directTrikePlans, plan =>
            Assert.True(
                plan.TotalFarePesos >= cheapest.TotalFarePesos,
                "A jeepney journey must not be beaten on fare by a direct trike."));
    }

    // TEST 9 -- a bad jeepney must never be forced. The corridor runs the
    // wrong way for this trip, so the only jeepney journey available would
    // be a token hop; the direct tricycle stays the recommendation.
    [Fact]
    public async Task PlanTripsAsync_DoesNotForceJeepneyWhenOnlyATokenHopExists()
    {
        var service = CreateCorridorService(
            // A very short corridor: even riding all of it leaves the jeepney
            // far below the primary-mode distance threshold.
            corridorEndLongitude: 120.5080,
            maxWalkTrikeTripDistanceMeters: 20_000);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.5600);

        Assert.NotEmpty(plans);
        var efficient = SinglePlanFor(plans, "efficient");

        var jeepneyDistance = efficient.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Sum(leg => leg.DistanceMeters);

        Assert.True(
            jeepneyDistance < 2_000,
            "A token jeepney hop must not be forced as the recommendation.");
    }

    // TEST 10 (partial) -- the role rule is a preference over already-valid
    // journeys, so it must not resurrect a feeder-shadowing journey: a long
    // trike chasing a jeepney downstream stays rejected even though the
    // resulting plan would contain a jeepney leg.
    [Fact]
    public async Task PlanTripsAsync_JeepneyPreferenceDoesNotResurrectFeederShadowing()
    {
        var farToda = BuildToda(1, "FAR-TODA", 15.0040, 120.5850);

        var service = CreateCorridorService(
            corridorEndLongitude: 120.6000,
            maxWalkTrikeTripDistanceMeters: 50,
            trikePoints: [farToda],
            maxWalkAccessDistanceMeters: 300,
            maxWalkToTrikePointMeters: 20_000);

        var plans = await service.PlanTripsAsync(
            15.0000, 120.5000,
            15.0000, 120.6000);

        Assert.NotEmpty(plans);
        Assert.All(plans, plan =>
        {
            var board = plan.Legs.FirstOrDefault(leg => leg.Mode == AccessMode.Jeepney);
            if (board is null)
                return;

            Assert.True(
                board.BoardLongitude < 120.5300,
                $"A long trike must not shadow the corridor to board at " +
                $"{board.BoardLongitude:F6}.");
        });
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static JeepneyTripPlan SinglePlanFor(
        IEnumerable<JeepneyTripPlan> plans,
        string objective) =>
        Assert.Single(plans.Where(plan =>
            plan.RecommendationType.Split(',').Contains(objective)));

    private static TricyclePoint BuildToda(
        int id, string code, double latitude, double longitude) => new()
        {
            TricyclePointId = id,
            PointCode = code,
            PointName = code,
            CenterLatitude = latitude,
            CenterLongitude = longitude,
            IsActive = true
        };

    /// <summary>
    /// An east-west jeepney corridor stored as a dense polyline, mirroring
    /// real route data. A two-point route would collapse every route sample
    /// onto one projected boarding position.
    /// </summary>
    private static TransportRoute BuildCorridor(
        double startLongitude,
        double endLongitude,
        double latitude)
    {
        const double stepDegrees = 0.001; // ~110m
        var points = new List<RoutePoint>();
        var order = 0;

        for (var longitude = startLongitude;
             longitude < endLongitude - 1e-9;
             longitude += stepDegrees)
        {
            points.Add(new RoutePoint
            {
                RouteId = 1,
                PointOrder = order++,
                Latitude = latitude,
                Longitude = Math.Round(longitude, 6)
            });
        }

        points.Add(new RoutePoint
        {
            RouteId = 1,
            PointOrder = order,
            Latitude = latitude,
            Longitude = endLongitude
        });

        return new TransportRoute
        {
            RouteId = 1,
            RouteCode = "CORRIDOR",
            RouteName = "Test corridor",
            OriginName = "Start",
            DestinationName = "End",
            IsActive = true,
            TransportMode = new TransportMode { Code = "JEEPNEY", Name = "Jeepney" },
            RoutePoints = points
        };
    }

    private static RoutingService CreateCorridorService(
        double corridorEndLongitude,
        double maxWalkTrikeTripDistanceMeters,
        double trikeSpeedMetersPerSecond = 5.6,
        List<TricyclePoint>? trikePoints = null,
        double maxWalkAccessDistanceMeters = 300,
        double maxWalkToTrikePointMeters = 600,
        double corridorLatitude = 15.0000)
    {
        var route = BuildCorridor(120.5000, corridorEndLongitude, corridorLatitude);

        // With no TODA supplied, place one at the origin so a direct trike is
        // always an available alternative to compete with the jeepney.
        var effectiveTrikePoints = trikePoints ??
            [BuildToda(1, "ORIGIN-TODA", 15.0000, 120.5000)];

        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 150,
            MaxRouteSamples = 80,
            MaxTransfers = 0,
            MaxTripOptions = 10,
            MaxCandidatesToConfirm = 200,
            MaxWalkAccessDistanceMeters = maxWalkAccessDistanceMeters,
            MaxWalkToTrikePointMeters = maxWalkToTrikePointMeters,
            MaxNearbyTrikeCandidates = 4,
            MaxTotalWalkingMetersPerJourney = 3_000,
            MaxWalkOnlyTripDistanceMeters = 50,
            MaxWalkTrikeTripDistanceMeters = maxWalkTrikeTripDistanceMeters,
            MaxStaticRouteSegmentJumpMeters = 15_000,
            TrikeSpeedMetersPerSecond = trikeSpeedMetersPerSecond,
            FeederShadowingMinProgressMeters = 300,
            FeederShadowingAccessDistanceRatio = 0.60,
            PrimaryJeepneyMinimumDistanceMeters = 2_000,
            PrimaryJeepneyMinimumJourneyShare = 0.5
        };

        var routeRepository = new Mock<ITransportRouteRepository>();
        routeRepository
            .Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([route]);

        var tricycleRepository = new Mock<ITricyclePointRepository>();
        tricycleRepository
            .Setup(repository => repository.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectiveTrikePoints);

        return new RoutingService(
            new StraightLineValhallaService(trikeSpeedMetersPerSecond),
            routeRepository.Object,
            tricycleRepository.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(options));
    }

    private sealed class StraightLineValhallaService(double trikeSpeedMetersPerSecond)
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
            var speed = isPedestrian ? 1.2 : trikeSpeedMetersPerSecond;

            IReadOnlyList<ValhallaMatrixResult> results = targets
                .Select((target, index) =>
                {
                    var distance = Math.Sqrt(
                        Math.Pow((source.Lat - target.Lat) * 111_000, 2) +
                        Math.Pow((source.Lon - target.Lon) * 111_000, 2));

                    return new ValhallaMatrixResult
                    {
                        FromIndex = 0,
                        ToIndex = index,
                        Distance = distance / 1_000,
                        Time = Math.Max(1, distance / speed)
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }
}
