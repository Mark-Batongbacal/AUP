using backend.Services.Routing;
using backend.Services.Telemetry;

namespace backend.Tests.Services.Routing;

public sealed class TransferSearchPruningTests
{
    [Fact]
    public async Task PlanTripsAsync_ReusesExactPathEdgeFilteringWithoutChangingJourney()
    {
        var telemetry = new RecordingTelemetry();
        var service = ProductionTopologyFixture.CreateService(
            telemetry: telemetry);

        var plans = await service.PlanTripsAsync(
            ProductionTopologyFixture.Origin.Latitude,
            ProductionTopologyFixture.Origin.Longitude,
            ProductionTopologyFixture.Destination.Latitude,
            ProductionTopologyFixture.Destination.Longitude);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => plan.TransferCount == 2);
        Assert.True(telemetry.Count("transfer_edge_filter_cache_hits") > 0);
        Assert.True(
            telemetry.Count("interchange_edges_visited") <
            telemetry.Count("transfer_edge_state_combinations"));
    }

    [Fact]
    public async Task PlanTripsAsync_MaterializesOnlySelectedTransferExpansions()
    {
        var telemetry = new RecordingTelemetry();
        var service = ProductionTopologyFixture.CreateService(
            telemetry: telemetry);

        var plans = await service.PlanTripsAsync(
            ProductionTopologyFixture.Origin.Latitude,
            ProductionTopologyFixture.Origin.Longitude,
            ProductionTopologyFixture.Destination.Latitude,
            ProductionTopologyFixture.Destination.Longitude);

        Assert.NotEmpty(plans);
        var considered = telemetry.Count(
            "transfer_expansion_descriptors_considered");
        var materialized = telemetry.Count(
            "transfer_expansion_states_materialized");
        Assert.True(considered > materialized);
        Assert.Equal(
            considered - materialized,
            telemetry.Count("transfer_expansion_states_never_materialized"));
        Assert.Equal(
            telemetry.Count("transfer_frontier_states_selected"),
            materialized);
    }

    private sealed class RecordingTelemetry : ITukiTelemetry
    {
        private readonly Dictionary<string, long> _counts = [];

        public long Count(string name) => _counts.GetValueOrDefault(name);

        public void Event(
            string eventName,
            Guid? tripSessionId = null,
            string? outcome = null) { }

        public IDisposable Measure(string operationName) => EmptyScope.Instance;

        public void RecordRequest(
            string path,
            int statusCode,
            double elapsedMilliseconds) { }

        public IRoutingTelemetryScope BeginRoutingPlan(
            string source,
            CancellationToken cancellationToken = default) =>
            EmptyScope.Instance;

        public IRoutingTelemetryScope BeginRoutingPass(
            int maxTransfers,
            CancellationToken cancellationToken = default) =>
            EmptyScope.Instance;

        public IDisposable BeginRoutingStage(string stageName) =>
            EmptyScope.Instance;

        public IDisposable MeasureRouting(string operationName) =>
            EmptyScope.Instance;

        public void IncrementRouting(string metricName, long value = 1) =>
            _counts[metricName] = Count(metricName) + value;

        public void SetRoutingValue(string metricName, double value) { }
        public void ObserveRouting(string metricName, double value) { }

        public void RecordRoutingAccessDiscoveryRoute(
            string routeId,
            int routeSampleCount,
            double boardDiscoveryMilliseconds,
            double directConnectionDiscoveryMilliseconds,
            double prefixComputationMilliseconds,
            double destinationAccessMilliseconds,
            long todaCandidatesConsidered,
            long todaCandidatesSurvivingFilters,
            long todaCandidatesSelected,
            long boardAccessAlternatives,
            long destinationAccessAlternatives,
            long directConnections) { }

        private sealed class EmptyScope : IRoutingTelemetryScope
        {
            public static EmptyScope Instance { get; } = new();
            public void Complete(string outcome) { }
            public void Dispose() { }
        }
    }
}
