using backend.Models.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Services.Routing;

/// <summary>
/// Signals that active route or TODA data changed. The next routing reader
/// rebuilds a complete snapshot before it can observe the new version.
/// </summary>
public interface IRoutingNetworkChangeNotifier
{
    void Invalidate(string reason);
}

internal interface IRoutingNetworkSnapshotProvider
{
    Task<RoutingNetworkSnapshotAccess> GetSnapshotAsync(
        Func<CancellationToken, Task<RoutingNetworkSnapshot>> build,
        CancellationToken cancellationToken);
}

internal sealed record RoutingNetworkSnapshotAccess(
    RoutingNetworkSnapshot Snapshot,
    bool BuiltSnapshot);

/// <summary>
/// A complete, read-only-by-ownership view of all active static routing data
/// and its derived indexes. A snapshot is never changed after publication.
/// </summary>
internal sealed record RoutingNetworkSnapshot(
    long Version,
    IReadOnlyList<StaticJeepneyRoute> Routes,
    IReadOnlyList<TrikePoint> TrikePoints,
    IReadOnlyDictionary<string,
        IReadOnlyList<(double Latitude, double Longitude)>> RouteSamples,
    IReadOnlyDictionary<string, RoutingService.FullRouteGeometry> RouteGeometries,
    IReadOnlyDictionary<string,
        IReadOnlyList<RoutingService.RouteAnchor>> RouteSearchAnchors,
    IReadOnlyDictionary<string,
        IReadOnlyList<RoutingService.RouteInterchange>> InterchangesByRoute);

/// <summary>
/// Pins one snapshot version for all routing passes in the same request,
/// including the preferred and transfer-depth fallback planners.
/// </summary>
internal sealed class RoutingNetworkSnapshotScope
{
    private RoutingNetworkSnapshot? _snapshot;

    public RoutingNetworkSnapshot? Snapshot => Volatile.Read(ref _snapshot);

    public RoutingNetworkSnapshot Pin(RoutingNetworkSnapshot snapshot) =>
        Interlocked.CompareExchange(ref _snapshot, snapshot, null) ?? snapshot;
}

/// <summary>
/// Coordinates one process-wide snapshot build. Readers retain the immutable
/// version they acquired while invalidation advances the desired version.
/// Publication is a single atomic reference swap after a complete build.
/// </summary>
public sealed class RoutingNetworkSnapshotProvider :
    IRoutingNetworkSnapshotProvider,
    IRoutingNetworkChangeNotifier,
    IDisposable
{
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly ILogger<RoutingNetworkSnapshotProvider> _logger;
    private RoutingNetworkSnapshot? _current;
    private long _desiredVersion = 1;

    public RoutingNetworkSnapshotProvider(
        ILogger<RoutingNetworkSnapshotProvider>? logger = null)
    {
        _logger = logger ?? NullLogger<RoutingNetworkSnapshotProvider>.Instance;
    }

    public void Invalidate(string reason)
    {
        var version = Interlocked.Increment(ref _desiredVersion);
        _logger.LogInformation(
            "Routing network snapshot invalidated; Version={Version} Reason={Reason}",
            version,
            reason);
    }

    async Task<RoutingNetworkSnapshotAccess>
        IRoutingNetworkSnapshotProvider.GetSnapshotAsync(
        Func<CancellationToken, Task<RoutingNetworkSnapshot>> build,
        CancellationToken cancellationToken)
    {
        var desiredVersion = Volatile.Read(ref _desiredVersion);
        var current = Volatile.Read(ref _current);
        if (current?.Version == desiredVersion)
            return new RoutingNetworkSnapshotAccess(current, false);

        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            var builtSnapshot = false;
            while (true)
            {
                desiredVersion = Volatile.Read(ref _desiredVersion);
                current = Volatile.Read(ref _current);
                if (current?.Version == desiredVersion)
                    return new RoutingNetworkSnapshotAccess(current, builtSnapshot);

                var candidate = await build(cancellationToken);
                builtSnapshot = true;

                // An admin mutation may commit while the snapshot is being
                // constructed. Discard that now-stale candidate and rebuild
                // before publishing anything to new readers.
                if (desiredVersion != Volatile.Read(ref _desiredVersion))
                    continue;

                var published = candidate with { Version = desiredVersion };
                Volatile.Write(ref _current, published);
                _logger.LogInformation(
                    "Published routing network snapshot Version={Version} Routes={RouteCount} TodaPoints={TodaPointCount}",
                    published.Version,
                    published.Routes.Count,
                    published.TrikePoints.Count);
                return new RoutingNetworkSnapshotAccess(published, true);
            }
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public void Dispose() => _buildLock.Dispose();
}
