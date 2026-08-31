using backend.Models.Database;
using backend.Models.Routing;
using backend.Services.Routing;
using backend.Services.Telemetry;

namespace backend.Tests.Services.Routing;

public sealed class AlightAccessReuseTests
{
    [Fact]
    public async Task PlanTripsAsync_ComputesExactRouteDestinationInputOnce()
    {
        var telemetry = new RecordingTelemetry();
        var service = CreateDirectService(telemetry, routeCount: 1);

        var plans = await PlanAsync(service);

        Assert.NotEmpty(plans);
        Assert.Equal(2, telemetry.Count("alight_access_computation_requests"));
        Assert.Equal(1, telemetry.Count("alight_access_computations_executed"));
        Assert.Equal(1, telemetry.Count("alight_access_unique_semantic_inputs"));
        Assert.Equal(1, telemetry.Count("alight_access_reuse_hits"));
    }

    [Fact]
    public async Task PlanTripsAsync_DoesNotReuseAcrossRouteIdentities()
    {
        var telemetry = new RecordingTelemetry();
        var service = CreateDirectService(telemetry, routeCount: 2);

        var plans = await PlanAsync(service);

        Assert.NotEmpty(plans);
        Assert.Equal(4, telemetry.Count("alight_access_computation_requests"));
        Assert.Equal(2, telemetry.Count("alight_access_computations_executed"));
        Assert.Equal(2, telemetry.Count("alight_access_unique_semantic_inputs"));
        Assert.Equal(2, telemetry.Count("alight_access_reuse_hits"));
    }

    [Fact]
    public async Task ConcurrentPlans_KeepAlightAccessReuseRequestLocal()
    {
        var requests = Enumerable.Range(0, 8)
            .Select(_ =>
            {
                var telemetry = new RecordingTelemetry();
                return (Service: CreateDirectService(telemetry, routeCount: 1),
                    Telemetry: telemetry);
            })
            .ToArray();

        var results = await Task.WhenAll(requests.Select(async request =>
            (Plans: await PlanAsync(request.Service), request.Telemetry)));

        Assert.All(results, result =>
        {
            Assert.NotEmpty(result.Plans);
            Assert.Equal(2, result.Telemetry.Count(
                "alight_access_computation_requests"));
            Assert.Equal(1, result.Telemetry.Count(
                "alight_access_computations_executed"));
            Assert.Equal(1, result.Telemetry.Count(
                "alight_access_unique_semantic_inputs"));
            Assert.Equal(1, result.Telemetry.Count(
                "alight_access_reuse_hits"));
        });
    }

    private static RoutingService CreateDirectService(
        ITukiTelemetry telemetry,
        int routeCount)
    {
        var waypoints = new[]
        {
            ProductionTopologyFixture.Origin,
            (Latitude: 15.1080, Longitude: 120.5840),
            ProductionTopologyFixture.Destination
        };
        var routes = Enumerable.Range(1, routeCount)
            .Select(index => ProductionTopologyFixture.BuildDenseRoute(
                100 + index,
                $"DIRECT-{index}",
                $"Direct route {index}",
                waypoints))
            .ToList<TransportRoute>();

        return ProductionTopologyFixture.CreateService(
            options: ProductionTopologyFixture.DefaultOptions(
                maxTransfers: 0,
                maxWalkAccessDistanceMeters: 1_500),
            routes: routes,
            trikePoints: [],
            telemetry: telemetry);
    }

    private static Task<List<JeepneyTripPlan>> PlanAsync(
        RoutingService service) => service.PlanTripsAsync(
        ProductionTopologyFixture.Origin.Latitude,
        ProductionTopologyFixture.Origin.Longitude,
        ProductionTopologyFixture.Destination.Latitude,
        ProductionTopologyFixture.Destination.Longitude);

    private sealed class RecordingTelemetry : ITukiTelemetry
    {
        private readonly Dictionary<string, long> _counts = [];

        public long Count(string name) => _counts.GetValueOrDefault(name);
        public void Event(string eventName, Guid? tripSessionId = null,
            string? outcome = null) { }
        public IDisposable Measure(string operationName) => EmptyScope.Instance;
        public void RecordRequest(string path, int statusCode,
            double elapsedMilliseconds) { }
        public IRoutingTelemetryScope BeginRoutingPlan(string source,
            CancellationToken cancellationToken = default) => EmptyScope.Instance;
        public IRoutingTelemetryScope BeginRoutingPass(int maxTransfers,
            CancellationToken cancellationToken = default) => EmptyScope.Instance;
        public IDisposable BeginRoutingStage(string stageName) => EmptyScope.Instance;
        public IDisposable MeasureRouting(string operationName) => EmptyScope.Instance;
        public void IncrementRouting(string metricName, long value = 1) =>
            _counts[metricName] = Count(metricName) + value;
        public void SetRoutingValue(string metricName, double value) { }
        public void ObserveRouting(string metricName, double value) { }
        public void RecordRoutingAccessDiscoveryRoute(string routeId,
            int routeSampleCount, double boardDiscoveryMilliseconds,
            double directConnectionDiscoveryMilliseconds,
            double prefixComputationMilliseconds,
            double destinationAccessMilliseconds, long todaCandidatesConsidered,
            long todaCandidatesSurvivingFilters, long todaCandidatesSelected,
            long boardAccessAlternatives, long destinationAccessAlternatives,
            long directConnections) { }

        private sealed class EmptyScope : IRoutingTelemetryScope
        {
            public static EmptyScope Instance { get; } = new();
            public void Complete(string outcome) { }
            public void Dispose() { }
        }
    }
}
