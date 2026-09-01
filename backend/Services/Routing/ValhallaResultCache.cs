using System.Collections.Concurrent;
using backend.Models.Valhalla;
using backend.Services.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

public sealed class ValhallaResultCacheOptions
{
    public const string SectionName = "Valhalla:ResultCache";

    public long SizeLimit { get; init; } = 250_000;
    public int SlidingExpirationSeconds { get; init; } = 600;
    public int AbsoluteExpirationSeconds { get; init; } = 1_800;

    public bool IsValid() =>
        SizeLimit > 0 &&
        SlidingExpirationSeconds > 0 &&
        AbsoluteExpirationSeconds >= SlidingExpirationSeconds;
}

internal enum ValhallaCacheUsage
{
    General,
    StaticTransfer
}

internal interface IValhallaResultCache
{
    Task<T> GetOrCreateAsync<T>(
        ValhallaCacheKey key,
        ValhallaCacheUsage usage,
        Func<CancellationToken, Task<T>> factory,
        Func<T, long> size,
        CancellationToken cancellationToken);
}

public sealed class ValhallaResultCache : IValhallaResultCache, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly ConcurrentDictionary<ValhallaCacheKey, Lazy<Task<object>>>
        _inflight = new();
    private readonly ITukiTelemetry _telemetry;
    private readonly TimeSpan _slidingExpiration;
    private readonly TimeSpan _absoluteExpiration;
    private readonly CancellationToken _shutdownToken;
    private readonly long _sizeLimit;

    public ValhallaResultCache(
        IOptions<ValhallaResultCacheOptions> options,
        ITukiTelemetry? telemetry = null,
        IHostApplicationLifetime? applicationLifetime = null)
    {
        var configured = options.Value;
        if (!configured.IsValid())
        {
            throw new ArgumentException(
                "Valhalla result cache configuration is invalid.",
                nameof(options));
        }

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = configured.SizeLimit
        });
        _sizeLimit = configured.SizeLimit;
        _telemetry = telemetry ?? NullTukiTelemetry.Instance;
        _slidingExpiration = TimeSpan.FromSeconds(
            configured.SlidingExpirationSeconds);
        _absoluteExpiration = TimeSpan.FromSeconds(
            configured.AbsoluteExpirationSeconds);
        _shutdownToken = applicationLifetime?.ApplicationStopping ??
            CancellationToken.None;
    }

    async Task<T> IValhallaResultCache.GetOrCreateAsync<T>(
        ValhallaCacheKey key,
        ValhallaCacheUsage usage,
        Func<CancellationToken, Task<T>> factory,
        Func<T, long> size,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out object? cached) && cached is T value)
        {
            RecordHit(key, usage);
            RecordEntryCount();
            return value;
        }

        var candidate = new Lazy<Task<object>>(
            async () =>
            {
                var created = await factory(_shutdownToken);
                var entrySize = Math.Min(
                    _sizeLimit,
                    Math.Max(1, size(created)));
                _cache.Set(
                    key,
                    created!,
                    new MemoryCacheEntryOptions
                    {
                        Size = entrySize,
                        SlidingExpiration = _slidingExpiration,
                        AbsoluteExpirationRelativeToNow = _absoluteExpiration
                    });
                RecordEntryCount();
                return created!;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);

        var shared = _inflight.GetOrAdd(key, candidate);
        var ownsMiss = ReferenceEquals(shared, candidate);
        if (ownsMiss)
        {
            RecordMiss(key, usage);
            _ = shared.Value.ContinueWith(
                (_, state) =>
                {
                    var removal =
                        ((ValhallaResultCache Cache,
                          ValhallaCacheKey Key,
                          Lazy<Task<object>> Task))state!;
                    removal.Cache._inflight.TryRemove(
                        new KeyValuePair<ValhallaCacheKey, Lazy<Task<object>>>(
                            removal.Key,
                            removal.Task));
                },
                (this, key, shared),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        else
        {
            _telemetry.IncrementRouting("valhalla_cache_coalesced_waiters");
            _telemetry.IncrementRouting("valhalla_calls_avoided");
        }

        var result = await shared.Value.WaitAsync(cancellationToken);
        return (T)result;
    }

    public void Dispose() => _cache.Dispose();

    internal int EntryCount => _cache.Count;

    private void RecordHit(ValhallaCacheKey key, ValhallaCacheUsage usage)
    {
        _telemetry.IncrementRouting("valhalla_cache_hits");
        _telemetry.IncrementRouting(
            $"valhalla_{key.Operation}_cache_hits");
        _telemetry.IncrementRouting("valhalla_calls_avoided");
        if (usage == ValhallaCacheUsage.StaticTransfer)
            _telemetry.IncrementRouting("static_transfer_cache_hits");
    }

    private void RecordMiss(ValhallaCacheKey key, ValhallaCacheUsage usage)
    {
        _telemetry.IncrementRouting("valhalla_cache_misses");
        _telemetry.IncrementRouting(
            $"valhalla_{key.Operation}_cache_misses");
        if (usage == ValhallaCacheUsage.StaticTransfer)
            _telemetry.IncrementRouting("static_transfer_cache_misses");
    }

    private void RecordEntryCount() =>
        _telemetry.SetRoutingValue("valhalla_cache_entries", _cache.Count);
}

internal sealed class PassThroughValhallaResultCache : IValhallaResultCache
{
    public static PassThroughValhallaResultCache Instance { get; } = new();

    public Task<T> GetOrCreateAsync<T>(
        ValhallaCacheKey key,
        ValhallaCacheUsage usage,
        Func<CancellationToken, Task<T>> factory,
        Func<T, long> size,
        CancellationToken cancellationToken) => factory(cancellationToken);
}

internal sealed class ValhallaCacheKey : IEquatable<ValhallaCacheKey>
{
    private const string MatrixOptions = "units=kilometers;verbose=true;v=1";
    private const string RouteOptions = "locations=2;v=1";

    private readonly ExactCoordinate[] _coordinates;
    private readonly int _hashCode;

    private ValhallaCacheKey(
        long snapshotVersion,
        string operation,
        string costing,
        string options,
        ExactCoordinate[] coordinates)
    {
        SnapshotVersion = snapshotVersion;
        Operation = operation;
        Costing = costing;
        Options = options;
        _coordinates = coordinates;

        var hash = new HashCode();
        hash.Add(snapshotVersion);
        hash.Add(operation, StringComparer.Ordinal);
        hash.Add(costing, StringComparer.Ordinal);
        hash.Add(options, StringComparer.Ordinal);
        foreach (var coordinate in coordinates)
            hash.Add(coordinate);
        _hashCode = hash.ToHashCode();
    }

    public long SnapshotVersion { get; }
    public string Operation { get; }
    public string Costing { get; }
    public string Options { get; }

    public static ValhallaCacheKey Matrix(
        long snapshotVersion,
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing)
    {
        var coordinates = new ExactCoordinate[targets.Count + 1];
        coordinates[0] = ExactCoordinate.From(source.Lat, source.Lon);
        for (var index = 0; index < targets.Count; index++)
        {
            coordinates[index + 1] = ExactCoordinate.From(
                targets[index].Lat,
                targets[index].Lon);
        }

        return new ValhallaCacheKey(
            snapshotVersion,
            "matrix",
            costing,
            MatrixOptions,
            coordinates);
    }

    public static ValhallaCacheKey Route(
        long snapshotVersion,
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing) =>
        new(
            snapshotVersion,
            "route",
            costing,
            RouteOptions,
            [
                ExactCoordinate.From(startLatitude, startLongitude),
                ExactCoordinate.From(endLatitude, endLongitude)
            ]);

    public bool Equals(ValhallaCacheKey? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null ||
            SnapshotVersion != other.SnapshotVersion ||
            !string.Equals(Operation, other.Operation, StringComparison.Ordinal) ||
            !string.Equals(Costing, other.Costing, StringComparison.Ordinal) ||
            !string.Equals(Options, other.Options, StringComparison.Ordinal) ||
            _coordinates.Length != other._coordinates.Length)
        {
            return false;
        }

        return _coordinates.AsSpan().SequenceEqual(other._coordinates);
    }

    public override bool Equals(object? obj) =>
        obj is ValhallaCacheKey other && Equals(other);

    public override int GetHashCode() => _hashCode;

    private readonly record struct ExactCoordinate(long Latitude, long Longitude)
    {
        public static ExactCoordinate From(double latitude, double longitude) =>
            new(
                BitConverter.DoubleToInt64Bits(latitude),
                BitConverter.DoubleToInt64Bits(longitude));
    }
}

internal static class ValhallaCacheSize
{
    public static long Matrix(IReadOnlyList<ValhallaMatrixResult> results) =>
        1L + results.Count;

    public static long Route(ValhallaRouteResponse response)
    {
        var legs = response.Trip?.Legs;
        if (legs is null)
            return 1;

        return 1L + legs.Count + legs.Sum(leg =>
            (long)leg.Points.Count + leg.Maneuvers.Count +
            Math.Max(1, leg.Shape.Length / 32));
    }
}
