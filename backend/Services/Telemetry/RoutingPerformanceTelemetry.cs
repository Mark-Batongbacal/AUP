using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace backend.Services.Telemetry;

public interface IRoutingTelemetryScope : IDisposable
{
    void Complete(string outcome);
}

internal sealed record RoutingMetricObservation(
    long Count,
    double Sum,
    double Maximum);

internal sealed record RoutingAccessDiscoveryRouteSnapshot(
    string RouteId,
    int RouteSampleCount,
    double BoardDiscoveryMilliseconds,
    double DirectConnectionDiscoveryMilliseconds,
    double PrefixComputationMilliseconds,
    double DestinationAccessMilliseconds,
    long TodaCandidatesConsidered,
    long TodaCandidatesSurvivingFilters,
    long TodaCandidatesSelected,
    long BoardAccessAlternatives,
    long DestinationAccessAlternatives,
    long DirectConnections);

internal sealed record RoutingPassTelemetrySnapshot(
    int MaxTransfers,
    string Outcome,
    double ElapsedMilliseconds,
    IReadOnlyDictionary<string, long> Counts,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, RoutingMetricObservation> Observations,
    IReadOnlyList<RoutingAccessDiscoveryRouteSnapshot> AccessDiscoveryRoutes);

internal sealed record RoutingPlanTelemetrySnapshot(
    Guid PlanId,
    string Source,
    string Outcome,
    double ElapsedMilliseconds,
    IReadOnlyDictionary<string, long> Counts,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, RoutingMetricObservation> Observations,
    IReadOnlyList<RoutingPassTelemetrySnapshot> Passes);

internal sealed class RoutingPlanLogState :
    IReadOnlyList<KeyValuePair<string, object?>>
{
    private const string OriginalFormat = "TukiRoutingPlan {PlanId}";
    private readonly RoutingPlanTelemetrySnapshot _snapshot;
    private readonly List<KeyValuePair<string, object?>> _properties;

    public RoutingPlanLogState(RoutingPlanTelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;
        _properties =
        [
            new("EventName", "TukiRoutingPlan"),
            new("PlanId", snapshot.PlanId),
            new("Source", snapshot.Source),
            new("Outcome", snapshot.Outcome),
            new("ElapsedMs", snapshot.ElapsedMilliseconds),
            new("Passes", snapshot.Passes)
        ];

        _properties.AddRange(snapshot.Counts.Select(metric =>
            new KeyValuePair<string, object?>(metric.Key, metric.Value)));
        _properties.AddRange(snapshot.Values.Select(metric =>
            new KeyValuePair<string, object?>(metric.Key, metric.Value)));
        foreach (var metric in snapshot.Observations)
        {
            _properties.Add(new($"{metric.Key}_count", metric.Value.Count));
            _properties.Add(new($"{metric.Key}_sum", metric.Value.Sum));
            _properties.Add(new($"{metric.Key}_max", metric.Value.Maximum));
        }

        _properties.Add(new("{OriginalFormat}", OriginalFormat));
    }

    public int Count => _properties.Count;
    public KeyValuePair<string, object?> this[int index] => _properties[index];
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        _properties.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public override string ToString() =>
        $"TukiRoutingPlan {JsonSerializer.Serialize(_snapshot)}";
}

internal abstract class RoutingTelemetryContext
{
    private readonly ConcurrentDictionary<string, long> _counts =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> _values =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RoutingMetricAccumulator> _observations =
        new(StringComparer.Ordinal);

    public void Increment(string metricName, long value) =>
        _counts.AddOrUpdate(metricName, value, (_, current) => current + value);

    public void SetValue(string metricName, double value) =>
        _values[metricName] = value;

    public void Observe(string metricName, double value) =>
        _observations.GetOrAdd(metricName, _ => new RoutingMetricAccumulator())
            .Observe(value);

    protected IReadOnlyDictionary<string, long> CountsSnapshot() =>
        new Dictionary<string, long>(_counts, StringComparer.Ordinal);

    protected IReadOnlyDictionary<string, double> ValuesSnapshot() =>
        new Dictionary<string, double>(_values, StringComparer.Ordinal);

    protected IReadOnlyDictionary<string, RoutingMetricObservation>
        ObservationsSnapshot() =>
        _observations.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Snapshot(),
            StringComparer.Ordinal);
}

internal sealed class RoutingPlanTelemetryContext(string source)
    : RoutingTelemetryContext
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly ConcurrentQueue<RoutingPassTelemetrySnapshot> _passes = new();
    private readonly object _outcomeSync = new();
    private string _outcome = "incomplete";
    private int _completed;

    public Guid PlanId { get; } = Guid.NewGuid();
    public string Source { get; } = source;

    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    public void Complete(string outcome)
    {
        lock (_outcomeSync)
            _outcome = outcome;
        Volatile.Write(ref _completed, 1);
    }

    public void AddPass(RoutingPassTelemetrySnapshot pass) => _passes.Enqueue(pass);

    public RoutingPlanTelemetrySnapshot Snapshot() =>
        new(
            PlanId,
            Source,
            _outcome,
            Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
            CountsSnapshot(),
            ValuesSnapshot(),
            ObservationsSnapshot(),
            _passes.ToArray());
}

internal sealed class RoutingPassTelemetryContext(int maxTransfers)
    : RoutingTelemetryContext
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly object _outcomeSync = new();
    private string _outcome = "incomplete";
    private int _completed;
    private readonly ConcurrentQueue<RoutingAccessDiscoveryRouteSnapshot>
        _accessDiscoveryRoutes = new();

    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    public void AddAccessDiscoveryRoute(
        RoutingAccessDiscoveryRouteSnapshot route) =>
        _accessDiscoveryRoutes.Enqueue(route);

    public void Complete(string outcome)
    {
        lock (_outcomeSync)
            _outcome = outcome;
        Volatile.Write(ref _completed, 1);
    }

    public RoutingPassTelemetrySnapshot Snapshot() =>
        new(
            maxTransfers,
            _outcome,
            Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
            CountsSnapshot(),
            ValuesSnapshot(),
            ObservationsSnapshot(),
            _accessDiscoveryRoutes.ToArray());
}

internal sealed class RoutingMetricAccumulator
{
    private readonly object _sync = new();
    private long _count;
    private double _sum;
    private double _maximum;

    public void Observe(double value)
    {
        lock (_sync)
        {
            _count++;
            _sum += value;
            _maximum = _count == 1 ? value : Math.Max(_maximum, value);
        }
    }

    public RoutingMetricObservation Snapshot()
    {
        lock (_sync)
            return new RoutingMetricObservation(_count, _sum, _maximum);
    }
}

internal sealed class RoutingPlanTelemetryScope(
    RoutingPlanTelemetryContext context,
    CancellationToken cancellationToken,
    Action dispose) : IRoutingTelemetryScope
{
    private int _disposed;

    public void Complete(string outcome) => context.Complete(outcome);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (!context.IsCompleted)
            context.Complete(cancellationToken.IsCancellationRequested ? "canceled" : "failed");

        dispose();
    }
}

internal sealed class RoutingPassTelemetryScope(
    RoutingPassTelemetryContext context,
    CancellationToken cancellationToken,
    Action dispose) : IRoutingTelemetryScope
{
    private int _disposed;

    public void Complete(string outcome) => context.Complete(outcome);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (!context.IsCompleted)
            context.Complete(cancellationToken.IsCancellationRequested ? "canceled" : "failed");

        dispose();
    }
}

internal sealed class NestedRoutingTelemetryScope(
    Action<string>? complete = null,
    CancellationToken cancellationToken = default)
    : IRoutingTelemetryScope
{
    private int _completed;
    private int _disposed;

    public static NestedRoutingTelemetryScope Instance { get; } = new();

    public void Complete(string outcome)
    {
        complete?.Invoke(outcome);
        Volatile.Write(ref _completed, 1);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 ||
            complete is null ||
            Volatile.Read(ref _completed) != 0)
        {
            return;
        }

        complete(cancellationToken.IsCancellationRequested ? "canceled" : "failed");
    }
}
