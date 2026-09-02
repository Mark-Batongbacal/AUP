using System.Diagnostics;
using backend.Models.Routing;
using backend.Services.Telemetry;
using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

public sealed class RoutingAdmissionOptions
{
    public const string SectionName = "Routing:AdmissionControl";

    /// <summary>
    /// Maximum number of route-planning pipelines executing in this process.
    /// This is intentionally separate from Valhalla's HTTP concurrency limit.
    /// </summary>
    public int MaxConcurrentPlans { get; init; } = 4;

    /// <summary>
    /// Maximum number of callers waiting in FIFO order. A value of zero
    /// rejects every request that cannot start immediately.
    /// </summary>
    public int MaxQueueLength { get; init; } = 8;

    public int MaxQueueWaitSeconds { get; init; } = 25;
    public int RetryAfterSeconds { get; init; } = 5;

    public bool IsValid() =>
        MaxConcurrentPlans is >= 1 and <= 1_024 &&
        MaxQueueLength is >= 0 and <= 100_000 &&
        MaxQueueWaitSeconds is >= 1 and <= 3_600 &&
        RetryAfterSeconds is >= 1 and <= 3_600;
}

public enum RoutingAdmissionRejectionReason
{
    QueueFull,
    QueueTimeout
}

public sealed class RoutingAdmissionRejectedException(
    RoutingAdmissionRejectionReason reason,
    int retryAfterSeconds) : Exception(
        reason == RoutingAdmissionRejectionReason.QueueFull
            ? "Route planning is at capacity. Please retry shortly."
            : "Route planning did not begin before the queue wait limit. Please retry shortly.")
{
    public RoutingAdmissionRejectionReason Reason { get; } = reason;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}

public interface IRoutingAdmissionController
{
    ValueTask<IDisposable> AcquireAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Process-wide admission controller for CPU- and Valhalla-intensive route
/// planning. The explicit linked-list queue provides deterministic FIFO
/// admission and O(1) removal when a queued request is canceled or times out.
/// </summary>
public sealed class RoutingAdmissionController : IRoutingAdmissionController
{
    private readonly object _sync = new();
    private readonly LinkedList<Waiter> _queue = [];
    private readonly RoutingAdmissionOptions _options;
    private readonly ITukiTelemetry _telemetry;
    private readonly ILogger<RoutingAdmissionController> _logger;
    private int _activeCount;

    public RoutingAdmissionController(
        IOptions<RoutingAdmissionOptions> options,
        ITukiTelemetry telemetry,
        ILogger<RoutingAdmissionController> logger)
    {
        _options = options.Value;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async ValueTask<IDisposable> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Waiter? waiter;
        AdmissionLease? immediateLease = null;
        int activeCount;
        int queueDepth;

        lock (_sync)
        {
            if (_activeCount < _options.MaxConcurrentPlans)
            {
                _activeCount++;
                activeCount = _activeCount;
                queueDepth = _queue.Count;
                waiter = null;
                immediateLease = new AdmissionLease(this);
            }
            else
            {
                if (_queue.Count >= _options.MaxQueueLength)
                {
                    RecordState(_activeCount, _queue.Count);
                    _telemetry.IncrementRouting("routing_admission_rejected");
                    throw new RoutingAdmissionRejectedException(
                        RoutingAdmissionRejectionReason.QueueFull,
                        _options.RetryAfterSeconds);
                }

                waiter = new Waiter();
                waiter.Node = _queue.AddLast(waiter);
                activeCount = _activeCount;
                queueDepth = _queue.Count;
            }
        }

        RecordState(activeCount, queueDepth);
        _telemetry.SetRoutingValue(
            "routing_admission_concurrency_limit",
            _options.MaxConcurrentPlans);
        _telemetry.SetRoutingValue(
            "routing_admission_queue_limit",
            _options.MaxQueueLength);

        if (immediateLease is not null)
        {
            _telemetry.IncrementRouting("routing_admission_admitted");
            _telemetry.ObserveRouting("routing_admission_wait_ms", 0);
            return immediateLease;
        }

        _telemetry.IncrementRouting("routing_admission_queued");
        var started = Stopwatch.GetTimestamp();
        try
        {
            var lease = await waiter!.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(_options.MaxQueueWaitSeconds),
                cancellationToken);
            var waitMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.IncrementRouting("routing_admission_admitted");
            _telemetry.ObserveRouting(
                "routing_admission_wait_ms",
                waitMilliseconds);
            RecordCurrentState();
            return lease;
        }
        catch (OperationCanceledException)
        {
            await ReleaseRaceWinnerAsync(waiter!);
            _telemetry.IncrementRouting("routing_admission_canceled");
            _telemetry.ObserveRouting(
                "routing_admission_wait_ms",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            RecordCurrentState();
            throw;
        }
        catch (TimeoutException)
        {
            await ReleaseRaceWinnerAsync(waiter!);
            var waitMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.IncrementRouting("routing_admission_timed_out");
            _telemetry.ObserveRouting(
                "routing_admission_wait_ms",
                waitMilliseconds);
            RecordCurrentState();
            _logger.LogWarning(
                "TukiRoutingAdmission Outcome=queue_timeout WaitMs={WaitMilliseconds} Active={ActiveCount} QueueDepth={QueueDepth}",
                waitMilliseconds,
                ActiveCount,
                QueueDepth);
            throw new RoutingAdmissionRejectedException(
                RoutingAdmissionRejectionReason.QueueTimeout,
                _options.RetryAfterSeconds);
        }
    }

    private int ActiveCount
    {
        get
        {
            lock (_sync)
                return _activeCount;
        }
    }

    private int QueueDepth
    {
        get
        {
            lock (_sync)
                return _queue.Count;
        }
    }

    private async Task ReleaseRaceWinnerAsync(Waiter waiter)
    {
        AdmissionLease? admittedLease = null;
        lock (_sync)
        {
            if (waiter.Node?.List is not null)
            {
                _queue.Remove(waiter.Node);
                waiter.Node = null;
                return;
            }

            if (waiter.Admitted)
                admittedLease = null;
            else
                return;
        }

        // Admission may win the race with timeout/cancellation after WaitAsync
        // has chosen the exception. Receive and release that transferred slot.
        admittedLease = await waiter.Completion.Task.ConfigureAwait(false);
        admittedLease.Dispose();
    }

    private void Release()
    {
        Waiter? next = null;
        AdmissionLease? lease = null;
        int activeCount;
        int queueDepth;

        lock (_sync)
        {
            _activeCount--;
            if (_activeCount < 0)
                throw new InvalidOperationException(
                    "Routing admission lease was released more than once.");

            if (_queue.First is { } first)
            {
                next = first.Value;
                _queue.RemoveFirst();
                next.Node = null;
                next.Admitted = true;
                _activeCount++;
                lease = new AdmissionLease(this);
            }

            activeCount = _activeCount;
            queueDepth = _queue.Count;
        }

        RecordState(activeCount, queueDepth);
        if (next is not null)
            next.Completion.TrySetResult(lease!);
    }

    private void RecordCurrentState()
    {
        lock (_sync)
            RecordState(_activeCount, _queue.Count);
    }

    private void RecordState(int activeCount, int queueDepth)
    {
        _telemetry.SetRoutingValue(
            "routing_admission_active_count",
            activeCount);
        _telemetry.SetRoutingValue(
            "routing_admission_queue_depth",
            queueDepth);
    }

    private sealed class Waiter
    {
        public TaskCompletionSource<AdmissionLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        public bool Admitted { get; set; }
    }

    private sealed class AdmissionLease(RoutingAdmissionController owner)
        : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Release();
        }
    }
}

/// <summary>
/// The one outer routing boundary. A single lease covers both transfer-depth
/// passes inside TransferFallbackRoutingService, avoiding nested acquisition.
/// </summary>
public sealed class RoutingAdmissionControlledService(
    IRoutingPlanningPipeline inner,
    IRoutingAdmissionController admission,
    ITukiTelemetry telemetry) : IRoutingService, IJourneyGeometryEnricher
{
    public Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        inner.FindNearbyRoutesAsync(latitude, longitude, cancellationToken);

    public Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default) =>
        PlanAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences: null,
            cancellationToken);

    public Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken = default) =>
        PlanAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences,
            cancellationToken);

    public Task EnrichSelectedPlanGeometryAsync(
        IReadOnlyList<JeepneyTripPlan> plans,
        CancellationToken cancellationToken = default) =>
        inner.EnrichSelectedPlanGeometryAsync(plans, cancellationToken);

    private async Task<List<JeepneyTripPlan>> PlanAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken)
    {
        using var planTelemetry = telemetry.BeginRoutingPlan(
            "RoutingAdmissionControlledService",
            cancellationToken);
        try
        {
            using var lease = await admission.AcquireAsync(cancellationToken);
            var plans = await inner.PlanTripsAsync(
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                preferences,
                cancellationToken);
            planTelemetry.Complete(plans.Count == 0 ? "no_route" : "success");
            return plans;
        }
        catch (RoutingAdmissionRejectedException)
        {
            planTelemetry.Complete("admission_rejected");
            throw;
        }
    }
}
