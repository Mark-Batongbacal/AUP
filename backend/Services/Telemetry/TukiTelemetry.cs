using System.Diagnostics;

namespace backend.Services.Telemetry;

public interface ITukiTelemetry
{
    void Event(string eventName, Guid? tripSessionId = null, string? outcome = null);
    IDisposable Measure(string operationName);
    void RecordRequest(string path, int statusCode, double elapsedMilliseconds);
}

public sealed class TukiTelemetry(ILogger<TukiTelemetry> logger) : ITukiTelemetry
{
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

    private sealed class Measurement(ILogger logger, string operation) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        public void Dispose() => logger.LogInformation(
            "TukiLatency {Operation} ElapsedMs={ElapsedMs}", operation,
            Stopwatch.GetElapsedTime(_started).TotalMilliseconds);
    }
}

public sealed class NullTukiTelemetry : ITukiTelemetry
{
    public static NullTukiTelemetry Instance { get; } = new();
    public void Event(string eventName, Guid? tripSessionId = null, string? outcome = null) { }
    public IDisposable Measure(string operationName) => Empty.Instance;
    public void RecordRequest(string path, int statusCode, double elapsedMilliseconds) { }
    private sealed class Empty : IDisposable
    {
        public static Empty Instance { get; } = new();
        public void Dispose() { }
    }
}
