using System.Diagnostics;

namespace backend.Services.Telemetry;

public interface ITukiTelemetry
{
    void Event(string eventName, Guid? tripSessionId = null, string? outcome = null);
    IDisposable Measure(string operationName);
    void RecordRequest(string path, int statusCode, double elapsedMilliseconds);
    IRoutingTelemetryScope BeginRoutingPlan(
        string source,
        CancellationToken cancellationToken = default);
    IRoutingTelemetryScope BeginRoutingPass(
        int maxTransfers,
        CancellationToken cancellationToken = default);
    IDisposable BeginRoutingStage(string stageName);
    IDisposable MeasureRouting(string operationName);
    void IncrementRouting(string metricName, long value = 1);
    void SetRoutingValue(string metricName, double value);
    void ObserveRouting(string metricName, double value);
}

public sealed class TukiTelemetry(ILogger<TukiTelemetry> logger) : ITukiTelemetry
{
    private readonly AsyncLocal<RoutingPlanTelemetryContext?> _routingPlan = new();
    private readonly AsyncLocal<RoutingPassTelemetryContext?> _routingPass = new();
    private readonly AsyncLocal<string?> _routingStage = new();

    public void Event(string eventName, Guid? tripSessionId = null, string? outcome = null) =>
        logger.LogInformation("TukiEvent {EventName} Session={TripSessionId} Outcome={Outcome}",
            eventName, tripSessionId, outcome);

    public IDisposable Measure(string operationName) => new Measurement(logger, operationName);

    public void RecordRequest(string path, int statusCode, double elapsedMilliseconds) =>
        logger.LogInformation(
            "TukiRequest {Path} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
            path,
            statusCode,
            elapsedMilliseconds);

    public IRoutingTelemetryScope BeginRoutingPlan(
        string source,
        CancellationToken cancellationToken = default)
    {
        if (_routingPlan.Value is { } currentPlan)
            return new NestedRoutingTelemetryScope(
                currentPlan.Complete,
                cancellationToken);

        var context = new RoutingPlanTelemetryContext(source);
        _routingPlan.Value = context;
        return new RoutingPlanTelemetryScope(
            context,
            cancellationToken,
            () =>
            {
                _routingPass.Value = null;
                _routingPlan.Value = null;
                var snapshot = context.Snapshot();
                logger.Log(
                    LogLevel.Information,
                    new EventId(1_001, "TukiRoutingPlan"),
                    new RoutingPlanLogState(snapshot),
                    null,
                    static (state, _) => state.ToString());
            });
    }

    public IRoutingTelemetryScope BeginRoutingPass(
        int maxTransfers,
        CancellationToken cancellationToken = default)
    {
        var plan = _routingPlan.Value;
        if (plan is null)
            return NestedRoutingTelemetryScope.Instance;

        var prior = _routingPass.Value;
        var pass = new RoutingPassTelemetryContext(maxTransfers);
        _routingPass.Value = pass;
        return new RoutingPassTelemetryScope(
            pass,
            cancellationToken,
            () =>
            {
                _routingPass.Value = prior;
                plan.AddPass(pass.Snapshot());
            });
    }

    public IDisposable BeginRoutingStage(string stageName)
    {
        var prior = _routingStage.Value;
        _routingStage.Value = stageName;
        return new RoutingStageScope(() => _routingStage.Value = prior);
    }

    public IDisposable MeasureRouting(string operationName)
    {
        var plan = _routingPlan.Value;
        var pass = _routingPass.Value;
        return plan is null
            ? EmptyMeasurement.Instance
            : new RoutingMeasurement(elapsedMilliseconds =>
            {
                plan.Observe(operationName, elapsedMilliseconds);
                pass?.Observe(operationName, elapsedMilliseconds);
            });
    }

    public void IncrementRouting(string metricName, long value = 1)
    {
        var plan = _routingPlan.Value;
        plan?.Increment(metricName, value);
        _routingPass.Value?.Increment(metricName, value);

        if (_routingStage.Value is { Length: > 0 } stage &&
            IsStageAttributedCount(metricName))
        {
            plan?.Increment($"{stage}_{metricName}", value);
            _routingPass.Value?.Increment($"{stage}_{metricName}", value);
        }
    }

    public void SetRoutingValue(string metricName, double value)
    {
        var plan = _routingPlan.Value;
        plan?.SetValue(metricName, value);
        _routingPass.Value?.SetValue(metricName, value);
    }

    public void ObserveRouting(string metricName, double value)
    {
        var plan = _routingPlan.Value;
        plan?.Observe(metricName, value);
        _routingPass.Value?.Observe(metricName, value);

        if (_routingStage.Value is { Length: > 0 } stage &&
            IsStageAttributedObservation(metricName))
        {
            plan?.Observe($"{stage}_{metricName}", value);
            _routingPass.Value?.Observe($"{stage}_{metricName}", value);
        }
    }

    private static bool IsStageAttributedCount(string metricName) =>
        metricName is
            "valhalla_matrix_http_calls" or
            "valhalla_route_http_calls" or
            "request_local_matrix_cache_hits" or
            "request_local_matrix_cache_misses";

    private static bool IsStageAttributedObservation(string metricName) =>
        metricName is
            "valhalla_gate_wait_ms" or
            "valhalla_execution_ms";

    private sealed class Measurement(ILogger logger, string operation) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        public void Dispose() => logger.LogInformation(
            "TukiLatency {Operation} ElapsedMs={ElapsedMs}", operation,
            Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
    }

    private sealed class RoutingMeasurement(Action<double> record) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            record(Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
        }
    }

    private sealed class EmptyMeasurement : IDisposable
    {
        public static EmptyMeasurement Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed class RoutingStageScope(Action dispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                dispose();
        }
    }
}

public sealed class NullTukiTelemetry : ITukiTelemetry
{
    public static NullTukiTelemetry Instance { get; } = new();
    public void Event(string eventName, Guid? tripSessionId = null, string? outcome = null) { }
    public IDisposable Measure(string operationName) => Empty.Instance;
    public void RecordRequest(string path, int statusCode, double elapsedMilliseconds) { }
    public IRoutingTelemetryScope BeginRoutingPlan(
        string source,
        CancellationToken cancellationToken = default) =>
        NestedRoutingTelemetryScope.Instance;
    public IRoutingTelemetryScope BeginRoutingPass(
        int maxTransfers,
        CancellationToken cancellationToken = default) =>
        NestedRoutingTelemetryScope.Instance;
    public IDisposable BeginRoutingStage(string stageName) => Empty.Instance;
    public IDisposable MeasureRouting(string operationName) => Empty.Instance;
    public void IncrementRouting(string metricName, long value = 1) { }
    public void SetRoutingValue(string metricName, double value) { }
    public void ObserveRouting(string metricName, double value) { }
    private sealed class Empty : IDisposable
    {
        public static Empty Instance { get; } = new();
        public void Dispose() { }
    }
}
