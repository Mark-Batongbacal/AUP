using System.Net;
using System.Text;
using backend.Models.Valhalla;
using backend.Services.Routing;
using backend.Services.Telemetry;
using Microsoft.Extensions.Configuration;

namespace backend.Tests.Services.Routing;

public sealed class ValhallaServiceTimingTests
{
    [Fact]
    public async Task GetMatrixAsync_UsesConfiguredWalkingSpeedForConfirmedTime()
    {
        var service = CreateService(
            distanceKilometers: 0.9,
            valhallaTimeSeconds: 123,
            walkingSpeedMetersPerSecond: 1.5,
            trikeSpeedMetersPerSecond: 4.0);

        var results = await service.GetMatrixAsync(
            new ValhallaLocation { Lat = 15.0, Lon = 120.5 },
            [new ValhallaLocation { Lat = 15.01, Lon = 120.51 }],
            "pedestrian");

        var result = Assert.Single(results);
        Assert.Equal(0.9, result.Distance);
        Assert.Equal(600, result.Time!.Value, 6);
    }

    [Fact]
    public async Task GetMatrixAsync_UsesConfiguredTrikeSpeedInsteadOfAutoEta()
    {
        var service = CreateService(
            distanceKilometers: 2.0,
            valhallaTimeSeconds: 180,
            walkingSpeedMetersPerSecond: 1.2,
            trikeSpeedMetersPerSecond: 4.0);

        var results = await service.GetMatrixAsync(
            new ValhallaLocation { Lat = 15.0, Lon = 120.5 },
            [new ValhallaLocation { Lat = 15.02, Lon = 120.52 }],
            "auto");

        var result = Assert.Single(results);
        Assert.Equal(2.0, result.Distance);
        Assert.Equal(500, result.Time!.Value, 6);
    }

    [Fact]
    public async Task GetMatrixAsync_LeavesUnownedCostingTimeUntouched()
    {
        var service = CreateService(
            distanceKilometers: 2.0,
            valhallaTimeSeconds: 180,
            walkingSpeedMetersPerSecond: 1.2,
            trikeSpeedMetersPerSecond: 4.0);

        var results = await service.GetMatrixAsync(
            new ValhallaLocation { Lat = 15.0, Lon = 120.5 },
            [new ValhallaLocation { Lat = 15.02, Lon = 120.52 }],
            "bicycle");

        var result = Assert.Single(results);
        Assert.Equal(180, result.Time!.Value, 6);
    }

    [Fact]
    public async Task GetMatrixAsync_RecordsValhallaWaitExecutionAndCallCount()
    {
        var telemetry = new RecordingTelemetry();
        var service = CreateService(
            distanceKilometers: 0.9,
            valhallaTimeSeconds: 123,
            walkingSpeedMetersPerSecond: 1.5,
            trikeSpeedMetersPerSecond: 4.0,
            telemetry);

        await service.GetMatrixAsync(
            new ValhallaLocation { Lat = 15.0, Lon = 120.5 },
            [new ValhallaLocation { Lat = 15.01, Lon = 120.51 }],
            "pedestrian");

        Assert.Equal(1, telemetry.Counts["valhalla_matrix_http_calls"]);
        Assert.Equal(5, telemetry.Values["valhalla_concurrency_limit"]);
        Assert.Single(telemetry.Observations["valhalla_gate_wait_ms"]);
        Assert.Single(telemetry.Observations["valhalla_execution_ms"]);
    }

    [Fact]
    public async Task SharedGate_LimitsConcurrencyAcrossTypedServiceInstances()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Valhalla:MaxConcurrentRequests"] = "1"
            })
            .Build();
        using var gate = new ValhallaConcurrencyGate(configuration);
        using var handler = new ConcurrencyTrackingHandler();
        using var firstClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://valhalla.test")
        };
        using var secondClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://valhalla.test")
        };
        var first = new ValhallaService(
            firstClient,
            configuration,
            concurrencyGate: gate);
        var second = new ValhallaService(
            secondClient,
            configuration,
            concurrencyGate: gate);

        await Task.WhenAll(
            first.GetMatrixAsync(
                new ValhallaLocation { Lat = 15, Lon = 120.5 },
                [new ValhallaLocation { Lat = 15.01, Lon = 120.51 }]),
            second.GetMatrixAsync(
                new ValhallaLocation { Lat = 15, Lon = 120.5 },
                [new ValhallaLocation { Lat = 15.02, Lon = 120.52 }]));

        Assert.Equal(1, handler.MaximumConcurrency);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SharedGate_CancelledWaiterDoesNotConsumeAPermit()
    {
        var configuration = ConcurrencyConfiguration(1);
        using var gate = new ValhallaConcurrencyGate(configuration);
        using var heldPermit = await gate.AcquireAsync(default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await gate.AcquireAsync(cancellation.Token));

        heldPermit.Dispose();
        using var nextPermit = await gate.AcquireAsync(default);
    }

    [Fact]
    public async Task ValhallaFailure_ReleasesSharedGatePermit()
    {
        var configuration = ConcurrencyConfiguration(1);
        using var gate = new ValhallaConcurrencyGate(configuration);
        using var client = new HttpClient(new FailOnceHandler())
        {
            BaseAddress = new Uri("https://valhalla.test")
        };
        var service = new ValhallaService(
            client,
            configuration,
            concurrencyGate: gate);
        var source = new ValhallaLocation { Lat = 15, Lon = 120.5 };
        ValhallaLocation[] targets =
            [new() { Lat = 15.01, Lon = 120.51 }];

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetMatrixAsync(source, targets));
        var recovered = await service.GetMatrixAsync(source, targets);

        Assert.Single(recovered);
    }

    private static ValhallaService CreateService(
        double distanceKilometers,
        double valhallaTimeSeconds,
        double walkingSpeedMetersPerSecond,
        double trikeSpeedMetersPerSecond,
        ITukiTelemetry? telemetry = null)
    {
        var json = $$"""
            {
              "sources_to_targets": [[
                {
                  "from_index": 0,
                  "to_index": 0,
                  "distance": {{distanceKilometers}},
                  "time": {{valhallaTimeSeconds}}
                }
              ]]
            }
            """;

        var client = new HttpClient(new StubHandler(json))
        {
            BaseAddress = new Uri("https://valhalla.test")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Routing:WalkingSpeedMetersPerSecond"] =
                    walkingSpeedMetersPerSecond.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                ["Routing:TrikeSpeedMetersPerSecond"] =
                    trikeSpeedMetersPerSecond.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                ["Routing:TrikeCostingModel"] = "auto"
            })
            .Build();

        return new ValhallaService(client, configuration, telemetry);
    }

    private static IConfiguration ConcurrencyConfiguration(int concurrency) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Valhalla:MaxConcurrentRequests"] = concurrency.ToString()
            })
            .Build();

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ConcurrencyTrackingHandler : HttpMessageHandler
    {
        private const string MatrixJson = """
            {
              "sources_to_targets": [[
                {
                  "from_index": 0,
                  "to_index": 0,
                  "distance": 1.0,
                  "time": 100
                }
              ]]
            }
            """;
        private int _active;
        private int _maximumConcurrency;
        private int _callCount;

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);
        public int CallCount => Volatile.Read(ref _callCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var active = Interlocked.Increment(ref _active);
            var observed = Volatile.Read(ref _maximumConcurrency);
            while (active > observed)
            {
                var prior = Interlocked.CompareExchange(
                    ref _maximumConcurrency,
                    active,
                    observed);
                if (prior == observed)
                    break;
                observed = prior;
            }

            try
            {
                await Task.Delay(50, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        MatrixJson,
                        Encoding.UTF8,
                        "application/json")
                };
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class FailOnceHandler : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
                throw new HttpRequestException("simulated failure");

            const string json = """
                {
                  "sources_to_targets": [[
                    {
                      "from_index": 0,
                      "to_index": 0,
                      "distance": 1.0,
                      "time": 100
                    }
                  ]]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class RecordingTelemetry : ITukiTelemetry
    {
        public Dictionary<string, long> Counts { get; } = [];
        public Dictionary<string, double> Values { get; } = [];
        public Dictionary<string, List<double>> Observations { get; } = [];

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

        public IDisposable MeasureRouting(string operationName) => EmptyScope.Instance;

        public void IncrementRouting(string metricName, long value = 1) =>
            Counts[metricName] = Counts.GetValueOrDefault(metricName) + value;

        public void SetRoutingValue(string metricName, double value) =>
            Values[metricName] = value;

        public void ObserveRouting(string metricName, double value)
        {
            if (!Observations.TryGetValue(metricName, out var values))
            {
                values = [];
                Observations[metricName] = values;
            }

            values.Add(value);
        }

        private sealed class EmptyScope : IRoutingTelemetryScope
        {
            public static EmptyScope Instance { get; } = new();
            public void Complete(string outcome) { }
            public void Dispose() { }
        }
    }
}
