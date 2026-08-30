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

/// <summary>
/// A compact, coordinate-free view of the high-value work counters. The full
/// TukiRoutingPlan event remains the benchmark source of truth; this debug
/// event makes one request easy to inspect without expanding every pass and
/// per-route diagnostic.
/// </summary>
internal sealed class JourneyPerformanceLogState :
    IReadOnlyList<KeyValuePair<string, object?>>
{
    private const string OriginalFormat =
        "JourneyPerformance ElapsedMs={ElapsedMs} RoutesTotal={RoutesTotal} " +
        "RoutesConsidered={RoutesConsidered} BoardCandidates={BoardCandidates} " +
        "AlightCandidates={AlightCandidates} CombinationsEvaluated={CombinationsEvaluated} " +
        "TransferCandidates={TransferCandidates} CandidatesConfirmed={CandidatesConfirmed} " +
        "MatrixRequests={MatrixRequests} MatrixCacheHits={MatrixCacheHits} " +
        "RouteRequests={RouteRequests} RouteCacheHits={RouteCacheHits} " +
        "OptionsProduced={OptionsProduced}";
    private readonly List<KeyValuePair<string, object?>> _properties;

    public JourneyPerformanceLogState(RoutingPlanTelemetrySnapshot snapshot)
    {
        long Count(string name) => snapshot.Counts.GetValueOrDefault(name);
        double Value(string name) => snapshot.Values.GetValueOrDefault(name);

        _properties =
        [
            new("EventName", "JourneyPerformance"),
            new("PlanId", snapshot.PlanId),
            new("ElapsedMs", snapshot.ElapsedMilliseconds),
            new("RoutesTotal", Value("route_count")),
            new("RoutesConsidered", Value(
                "routes_considered_after_spatial_filter")),
            new("BoardCandidates", Count("board_access_alternatives")),
            new("AlightCandidates", Count("destination_access_alternatives")),
            new("CombinationsEvaluated", Count(
                "board_alight_combinations_evaluated")),
            new("TransferCandidates", Count(
                "transfer_interchange_candidates_evaluated")),
            new("CandidatesConfirmed", Count("transit_candidates_confirmed") +
                Count("access_only_candidates_confirmed")),
            new("MatrixRequests", Count("valhalla_matrix_http_calls")),
            new("MatrixCacheHits", Count("valhalla_matrix_cache_hits") +
                Count("request_local_matrix_cache_hits")),
            new("RouteRequests", Count("valhalla_route_http_calls")),
            new("RouteCacheHits", Count("valhalla_route_cache_hits")),
            new("OptionsProduced", Value("selected_plan_count")),
            new("{OriginalFormat}", OriginalFormat)
        ];
    }

    public int Count => _properties.Count;
    public KeyValuePair<string, object?> this[int index] => _properties[index];
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        _properties.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public override string ToString() => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        "JourneyPerformance ElapsedMs={0:F1} RoutesTotal={1} " +
        "RoutesConsidered={2} BoardCandidates={3} AlightCandidates={4} " +
        "CombinationsEvaluated={5} TransferCandidates={6} CandidatesConfirmed={7} " +
        "MatrixRequests={8} MatrixCacheHits={9} RouteRequests={10} " +
        "RouteCacheHits={11} OptionsProduced={12}",
        _properties[2].Value,
        _properties[3].Value,
        _properties[4].Value,
        _properties[5].Value,
        _properties[6].Value,
        _properties[7].Value,
        _properties[8].Value,
        _properties[9].Value,
        _properties[10].Value,
        _properties[11].Value,
        _properties[12].Value,
        _properties[13].Value,
        _properties[14].Value);
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
