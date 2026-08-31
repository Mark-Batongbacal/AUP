using System.Collections.Concurrent;
using backend.Services.Routing;
using backend.Services.Telemetry;

namespace backend.Tests.Services.Routing;

public sealed class LazyDestinationMaterializationTests
{
    [Fact]
    public void Selector_DoesNotMaterializeUnconsumedDescriptors()
    {
        IReadOnlyList<IReadOnlyList<Descriptor>> buckets =
        [
            Enumerable.Range(0, 50)
                .Select(index => new Descriptor($"A-{index:D2}"))
                .ToList(),
            Enumerable.Range(0, 50)
                .Select(index => new Descriptor($"B-{index:D2}"))
                .ToList()
        ];
        var materialized = 0;

        var selected = LazyRoundRobinSelector.Select(
            buckets,
            3,
            descriptor =>
            {
                materialized++;
                return new Candidate(descriptor.Key);
            },
            candidate => candidate.Key);

        Assert.Equal(["A-00", "B-00", "A-01"],
            selected.Select(candidate => candidate.Key));
        Assert.Equal(3, materialized);
        Assert.Equal(97, buckets.Sum(bucket => bucket.Count) - materialized);
    }

    [Fact]
    public void Selector_PreservesPhase2RoundRobinOrdering()
    {
        IReadOnlyList<IReadOnlyList<Descriptor>> buckets =
        [
            [new("A-1"), new("A-2"), new("A-3")],
            [new("B-1")],
            [new("C-1"), new("C-2")]
        ];

        var eager = SelectEagerReference(buckets, 6);
        var lazy = LazyRoundRobinSelector.Select(
            buckets,
            6,
            descriptor => new Candidate(descriptor.Key),
            candidate => candidate.Key);

        Assert.Equal(
            ["A-1", "B-1", "C-1", "A-2", "C-2", "A-3"],
            lazy.Select(candidate => candidate.Key));
        Assert.Equal(
            eager.Select(candidate => candidate.Key),
            lazy.Select(candidate => candidate.Key));
    }

    [Fact]
    public void Selector_PreservesDuplicateExhaustionOrdering()
    {
        IReadOnlyList<IReadOnlyList<Descriptor>> buckets =
        [
            [new("same"), new("A-2")],
            [new("same"), new("B-2"), new("B-3")],
            [new("C-1")]
        ];
        var duplicateCount = 0;

        var eager = SelectEagerReference(buckets, 5);
        var lazy = LazyRoundRobinSelector.Select(
            buckets,
            5,
            descriptor => new Candidate(descriptor.Key),
            candidate => candidate.Key,
            duplicateRejected: () => duplicateCount++);

        Assert.Equal(
            eager.Select(candidate => candidate.Key),
            lazy.Select(candidate => candidate.Key));
        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public void Selector_StopsDuringCanceledLazyConsumption()
    {
        using var cancellation = new CancellationTokenSource();
        IReadOnlyList<IReadOnlyList<Descriptor>> buckets =
        [
            [new("A-1"), new("A-2")],
            [new("B-1"), new("B-2")]
        ];
        var materialized = 0;

        Assert.Throws<OperationCanceledException>(() =>
            LazyRoundRobinSelector.Select(
                buckets,
                4,
                descriptor =>
                {
                    materialized++;
                    cancellation.Cancel();
                    return new Candidate(descriptor.Key);
                },
                candidate => candidate.Key,
                cancellation.Token));
        Assert.Equal(1, materialized);
    }

    [Fact]
    public async Task Selector_ConcurrentRequestsKeepMaterializationStateLocal()
    {
        var results = new ConcurrentBag<IReadOnlyList<string>>();

        await Task.WhenAll(Enumerable.Range(0, 16).Select(async request =>
        {
            await Task.Yield();
            IReadOnlyList<IReadOnlyList<Descriptor>> buckets =
            [
                [new($"{request}-A1"), new($"{request}-A2")],
                [new($"{request}-B1"), new($"{request}-B2")]
            ];
            var selected = LazyRoundRobinSelector.Select(
                buckets,
                3,
                descriptor => new Candidate(descriptor.Key),
                candidate => candidate.Key);
            results.Add(selected.Select(candidate => candidate.Key).ToList());
        }));

        Assert.Equal(16, results.Count);
        Assert.All(results, result =>
        {
            var request = result[0].Split('-')[0];
            Assert.Equal(
                [$"{request}-A1", $"{request}-B1", $"{request}-A2"],
                result);
        });
    }

    [Fact]
    public async Task TransferSearch_ReportsAvoidedDestinationMaterializations()
    {
        var telemetry = new RecordingTelemetry();
        var service = ProductionTopologyFixture.CreateService(telemetry: telemetry);

        var plans = await service.PlanTripsAsync(
            ProductionTopologyFixture.Origin.Latitude,
            ProductionTopologyFixture.Origin.Longitude,
            ProductionTopologyFixture.Destination.Latitude,
            ProductionTopologyFixture.Destination.Longitude);

        Assert.NotEmpty(plans);
        var considered = telemetry.Count(
            "destination_completion_descriptors_considered");
        var materialized = telemetry.Count(
            "destination_completion_candidates_materialized");
        Assert.True(considered > materialized);
        Assert.Equal(
            considered - materialized,
            telemetry.Count("destination_completion_materializations_avoided"));
        Assert.Equal(
            materialized,
            telemetry.Count("destination_completion_materializations_requested"));
        Assert.Equal(
            telemetry.Count("transfer_candidates_emitted"),
            materialized);
        Assert.True(telemetry.Count("destination_journey_prefix_reuses") > 0);
    }

    private static List<Candidate> SelectEagerReference(
        IReadOnlyList<IReadOnlyList<Descriptor>> buckets,
        int maximumItems)
    {
        var queues = buckets
            .Select(bucket => new Queue<Candidate>(bucket.Select(
                descriptor => new Candidate(descriptor.Key))))
            .ToList();
        var selected = new List<Candidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (selected.Count < maximumItems)
        {
            var addedAny = false;
            foreach (var queue in queues)
            {
                while (queue.TryDequeue(out var candidate))
                {
                    if (!seen.Add(candidate.Key))
                        continue;
                    selected.Add(candidate);
                    addedAny = true;
                    break;
                }

                if (selected.Count >= maximumItems)
                    break;
            }

            if (!addedAny)
                break;
        }

        return selected;
    }

    private sealed record Descriptor(string Key);
    private sealed record Candidate(string Key);

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
