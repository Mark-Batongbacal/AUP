using System.Text.Json;
using backend.Models.Database;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Services.Routing;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Routing;

public sealed class PhaseERoutingParityTests
{
    [Fact]
    public async Task DirectRoute_ColdAndWarmCacheReturnIdenticalOrderedPlans()
    {
        var options = ProductionTopologyFixture.DefaultOptions(maxTransfers: 0);
        await AssertProductionParity(
            options,
            (15.0956, 120.5451),
            (15.1038, 120.5850),
            preferences: null,
            plans => Assert.Contains(plans, plan => plan.TransferCount == 0));
    }

    [Fact]
    public async Task OneTransfer_ColdAndWarmCacheReturnIdenticalOrderedPlans()
    {
        var options = ProductionTopologyFixture.DefaultOptions(maxTransfers: 1);
        await AssertProductionParity(
            options,
            ProductionTopologyFixture.Origin,
            (15.1238, 120.6038),
            preferences: null,
            plans => Assert.Contains(plans, plan => plan.TransferCount == 1));
    }

    [Fact]
    public async Task TwoTransfers_ColdAndWarmCacheReturnIdenticalOrderedPlans()
    {
        using var cache = CreateCache();
        using var snapshots = new RoutingNetworkSnapshotProvider();
        var valhalla = new CountingValhallaService(
            new RoadNetworkValhallaService());
        var coldService = TransferChainTopologyFixture.CreateService(
            valhalla: valhalla,
            resultCache: cache,
            snapshotProvider: snapshots);
        var cold = await coldService.PlanTripsAsync(
            TransferChainTopologyFixture.Origin.Latitude,
            TransferChainTopologyFixture.Origin.Longitude,
            TransferChainTopologyFixture.Destination.Latitude,
            TransferChainTopologyFixture.Destination.Longitude);
        var callsAfterColdPlan = valhalla.TotalCalls;

        var warmService = TransferChainTopologyFixture.CreateService(
            valhalla: valhalla,
            resultCache: cache,
            snapshotProvider: snapshots);
        var warm = await warmService.PlanTripsAsync(
            TransferChainTopologyFixture.Origin.Latitude,
            TransferChainTopologyFixture.Origin.Longitude,
            TransferChainTopologyFixture.Destination.Latitude,
            TransferChainTopologyFixture.Destination.Longitude);

        Assert.Equal(Serialize(cold), Serialize(warm));
        Assert.Contains(cold, plan => plan.TransferCount == 2);
        Assert.Equal(callsAfterColdPlan, valhalla.TotalCalls);
    }

    [Fact]
    public async Task LoopSelfTransfer_ColdAndWarmCachePreserveOccurrenceSemantics()
    {
        var loop = ProductionTopologyFixture.BuildDenseRoute(
            1,
            "X",
            "Looping X",
            [
                (15.0000, 120.5000),
                (15.0000, 120.5100),
                (15.0200, 120.5100),
                (15.0200, 120.5300),
                (15.0003, 120.5300),
                (15.0003, 120.5102),
                (14.9900, 120.5102)
            ]);
        var options = new RoutingOptions
        {
            DefaultSampleIntervalMeters = 100,
            MaxRouteSamples = 200,
            MaxInterchangesPerRoutePair = 4,
            MaxTransferWalkMeters = 150,
            MinimumSelfTransferProgressMeters = 1_000,
            MinimumSelfTransferRouteToWalkRatio = 3,
            MaxWalkAccessDistanceMeters = 100,
            MaxTotalWalkingMetersPerJourney = 5_000,
            MaxWalkOnlyTripDistanceMeters = 25,
            MaxWalkTrikeTripDistanceMeters = 25,
            MaxTripOptions = 20,
            MaxCandidatesToConfirm = 200,
            MaxTransfers = 2
        };

        await AssertProductionParity(
            options,
            (15.0000, 120.5000),
            (14.9900, 120.5102),
            preferences: null,
            plans => Assert.Contains(plans, plan =>
                plan.Legs.Count(leg =>
                    leg.Mode == AccessMode.Jeepney && leg.RouteId == "X") == 2),
            routes: [loop],
            trikePoints: []);
    }

    [Fact]
    public async Task TricycleOriginAndDestinationAccess_ColdAndWarmCacheMatch()
    {
        var originOptions = ProductionTopologyFixture.DefaultOptions();
        await AssertProductionParity(
            originOptions,
            ProductionTopologyFixture.Origin,
            ProductionTopologyFixture.Destination,
            preferences: null,
            plans => Assert.Contains(plans, plan =>
                plan.OriginAccess.Mode == AccessMode.Trike));

        var destinationRoute = ProductionTopologyFixture.BuildDenseRoute(
            50,
            "DEST-TRIKE",
            "Destination tricycle route",
            [(15.1000, 120.5000), (15.1000, 120.5500)]);
        var destinationToda = ProductionTopologyFixture.BuildToda(
            50,
            "TODA-DEST",
            15.1000,
            120.5502);
        var destinationOptions = ProductionTopologyFixture.DefaultOptions(
            maxTransfers: 0,
            maxWalkAccessDistanceMeters: 500,
            maxWalkToTrikePointMeters: 500);
        await AssertProductionParity(
            destinationOptions,
            (15.1000, 120.5002),
            (15.1000, 120.5700),
            preferences: null,
            plans => Assert.Contains(plans, plan =>
                plan.DestinationAccess.Mode == AccessMode.Trike),
            routes: [destinationRoute],
            trikePoints: [destinationToda]);
    }

    [Theory]
    [InlineData(JourneyOptimizationPreference.Fastest)]
    [InlineData(JourneyOptimizationPreference.Cheapest)]
    [InlineData(JourneyOptimizationPreference.Efficient)]
    public async Task PreferenceVariants_ColdAndWarmCacheReturnIdenticalOrder(
        JourneyOptimizationPreference preference)
    {
        await AssertProductionParity(
            ProductionTopologyFixture.DefaultOptions(),
            ProductionTopologyFixture.Origin,
            ProductionTopologyFixture.Destination,
            new JourneyPlanningPreferences(
                OptimizationPreference: preference),
            Assert.NotEmpty);
    }

    private static async Task AssertProductionParity(
        RoutingOptions options,
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination,
        JourneyPlanningPreferences? preferences,
        Action<List<JeepneyTripPlan>> assertScenario,
        List<TransportRoute>? routes = null,
        List<TricyclePoint>? trikePoints = null)
    {
        using var cache = CreateCache();
        using var snapshots = new RoutingNetworkSnapshotProvider();
        var valhalla = new CountingValhallaService(
            new RoadNetworkValhallaService());
        var coldService = ProductionTopologyFixture.CreateService(
            options,
            valhalla,
            routes,
            trikePoints,
            cache,
            snapshots);
        var cold = await coldService.PlanTripsAsync(
            origin.Latitude,
            origin.Longitude,
            destination.Latitude,
            destination.Longitude,
            preferences);
        var callsAfterColdPlan = valhalla.TotalCalls;

        var warmService = ProductionTopologyFixture.CreateService(
            options,
            valhalla,
            routes,
            trikePoints,
            cache,
            snapshots);
        var warm = await warmService.PlanTripsAsync(
            origin.Latitude,
            origin.Longitude,
            destination.Latitude,
            destination.Longitude,
            preferences);

        assertScenario(cold);
        Assert.Equal(Serialize(cold), Serialize(warm));
        Assert.Equal(callsAfterColdPlan, valhalla.TotalCalls);
    }

    private static string Serialize(List<JeepneyTripPlan> plans) =>
        JsonSerializer.Serialize(plans);

    private static ValhallaResultCache CreateCache() =>
        new(Options.Create(new ValhallaResultCacheOptions
        {
            SizeLimit = 1_000_000,
            SlidingExpirationSeconds = 60,
            AbsoluteExpirationSeconds = 120
        }));

    private sealed class CountingValhallaService(IValhallaService inner)
        : IValhallaService
    {
        private int _matrixCalls;
        private int _routeCalls;

        public int TotalCalls =>
            Volatile.Read(ref _matrixCalls) + Volatile.Read(ref _routeCalls);

        public Task<ValhallaRouteResponse> GetRouteAsync(
            double startLatitude,
            double startLongitude,
            double endLatitude,
            double endLongitude,
            string costing = "pedestrian",
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _routeCalls);
            cancellationToken.ThrowIfCancellationRequested();
            var meters = ProductionTopologyFixture.Haversine(
                (startLatitude, startLongitude),
                (endLatitude, endLongitude)) * 1.2;
            return Task.FromResult(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Summary = new ValhallaSummary
                    {
                        Length = meters / 1_000,
                        Time = Math.Max(1, meters / 5.6)
                    },
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
            Interlocked.Increment(ref _matrixCalls);
            return inner.GetMatrixAsync(
                source,
                targets,
                costing,
                cancellationToken);
        }
    }
}
