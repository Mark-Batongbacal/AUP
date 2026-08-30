using backend.Models.Valhalla;
using backend.Services.Routing;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Routing;

public sealed class ValhallaResultCacheTests
{
    [Fact]
    public async Task ConcurrentIdenticalMisses_AreSingleFlight()
    {
        using var cache = CreateCache();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var key = MatrixKey(version: 1, latitude: 15.1);

        var requests = Enumerable.Range(0, 32)
            .Select(_ => Get(cache, key, async _ =>
            {
                Interlocked.Increment(ref calls);
                entered.TrySetResult();
                return await release.Task;
            }))
            .ToList();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult("authoritative");

        var results = await Task.WhenAll(requests);
        Assert.All(results, result => Assert.Equal("authoritative", result));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExactCoordinates_AreNotRoundedOrConflated()
    {
        using var cache = CreateCache();
        var calls = 0;
        var latitude = 15.1;
        var adjacentDouble = Math.BitIncrement(latitude);

        var first = await Get(
            cache,
            MatrixKey(1, latitude),
            _ => Task.FromResult($"value-{Interlocked.Increment(ref calls)}"));
        var second = await Get(
            cache,
            MatrixKey(1, adjacentDouble),
            _ => Task.FromResult($"value-{Interlocked.Increment(ref calls)}"));

        Assert.NotEqual(first, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SnapshotVersionChange_MakesOldRouteAndTodaResultsUnreachable()
    {
        using var cache = CreateCache();
        using var snapshots = new RoutingNetworkSnapshotProvider();
        var provider = (IRoutingNetworkSnapshotProvider)snapshots;
        var calls = 0;

        var firstSnapshot = await provider.GetSnapshotAsync(
            _ => Task.FromResult(EmptySnapshot()),
            default);
        var first = await Get(
            cache,
            MatrixKey(firstSnapshot.Snapshot.Version, 15.1),
            _ => Task.FromResult(Interlocked.Increment(ref calls).ToString()));

        snapshots.Invalidate("active jeepney route changed");
        var routeSnapshot = await provider.GetSnapshotAsync(
            _ => Task.FromResult(EmptySnapshot()),
            default);
        var afterRouteChange = await Get(
            cache,
            MatrixKey(routeSnapshot.Snapshot.Version, 15.1),
            _ => Task.FromResult(Interlocked.Increment(ref calls).ToString()));

        snapshots.Invalidate("active TODA changed");
        var todaSnapshot = await provider.GetSnapshotAsync(
            _ => Task.FromResult(EmptySnapshot()),
            default);
        var afterTodaChange = await Get(
            cache,
            MatrixKey(todaSnapshot.Snapshot.Version, 15.1),
            _ => Task.FromResult(Interlocked.Increment(ref calls).ToString()));

        Assert.Equal("1", first);
        Assert.Equal("2", afterRouteChange);
        Assert.Equal("3", afterTodaChange);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task FaultTimeoutMalformedAndUnderlyingCancellation_AreRetryable()
    {
        using var cache = CreateCache();

        await AssertRetryableFailure(
            cache,
            MatrixKey(1, 15.11),
            () => new HttpRequestException("500"));
        await AssertRetryableFailure(
            cache,
            MatrixKey(1, 15.12),
            () => new TimeoutException("timeout"));
        await AssertRetryableFailure(
            cache,
            MatrixKey(1, 15.13),
            () => new InvalidOperationException("malformed response"));

        var canceledCalls = 0;
        var canceledKey = MatrixKey(1, 15.14);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get(
            cache,
            canceledKey,
            _ =>
            {
                Interlocked.Increment(ref canceledCalls);
                return Task.FromCanceled<string>(new CancellationToken(true));
            }));
        Assert.Equal(
            "recovered",
            await Get(cache, canceledKey, _ =>
            {
                Interlocked.Increment(ref canceledCalls);
                return Task.FromResult("recovered");
            }));
        Assert.Equal(2, canceledCalls);
    }

    [Fact]
    public async Task CallerCancellation_DoesNotCancelSharedAuthoritativeWork()
    {
        using var cache = CreateCache();
        var release = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var key = MatrixKey(1, 15.2);
        using var cancellation = new CancellationTokenSource();

        var canceledWaiter = Get(cache, key, async _ =>
        {
            Interlocked.Increment(ref calls);
            return await release.Task;
        }, cancellation.Token);
        var survivingWaiter = Get(
            cache,
            key,
            _ => throw new InvalidOperationException("must coalesce"));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWaiter);
        release.SetResult("completed");

        Assert.Equal("completed", await survivingWaiter);
        Assert.Equal("completed", await Get(
            cache,
            key,
            _ => throw new InvalidOperationException("must hit cache")));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task WeightedStorage_RemainsBounded()
    {
        using var cache = CreateCache(sizeLimit: 3);

        for (var index = 0; index < 20; index++)
        {
            await Get(
                cache,
                MatrixKey(1, 15 + index * 0.001),
                _ => Task.FromResult(index.ToString()));
        }

        Assert.InRange(cache.EntryCount, 0, 3);
    }

    private static async Task AssertRetryableFailure(
        ValhallaResultCache cache,
        ValhallaCacheKey key,
        Func<Exception> exception)
    {
        var calls = 0;
        await Assert.ThrowsAsync(exception().GetType(), () => Get(
            cache,
            key,
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromException<string>(exception());
            }));

        Assert.Equal("recovered", await Get(cache, key, _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("recovered");
        }));
        Assert.Equal(2, calls);
    }

    private static ValhallaResultCache CreateCache(long sizeLimit = 10_000) =>
        new(Options.Create(new ValhallaResultCacheOptions
        {
            SizeLimit = sizeLimit,
            SlidingExpirationSeconds = 60,
            AbsoluteExpirationSeconds = 120
        }));

    private static Task<string> Get(
        ValhallaResultCache cache,
        ValhallaCacheKey key,
        Func<CancellationToken, Task<string>> factory,
        CancellationToken cancellationToken = default) =>
        ((IValhallaResultCache)cache).GetOrCreateAsync(
            key,
            ValhallaCacheUsage.General,
            factory,
            _ => 1,
            cancellationToken);

    private static ValhallaCacheKey MatrixKey(long version, double latitude) =>
        ValhallaCacheKey.Matrix(
            version,
            new ValhallaLocation { Lat = latitude, Lon = 120.5 },
            [new ValhallaLocation { Lat = 15.2, Lon = 120.6 }],
            "pedestrian");

    private static RoutingNetworkSnapshot EmptySnapshot() => new(
        Version: 0,
        Routes: [],
        TrikePoints: [],
        RouteSamples: new Dictionary<string,
            IReadOnlyList<(double Latitude, double Longitude)>>(),
        RouteGeometries: new Dictionary<string, RoutingService.FullRouteGeometry>(),
        RouteSearchAnchors: new Dictionary<string,
            IReadOnlyList<RoutingService.RouteAnchor>>(),
        InterchangesByRoute: new Dictionary<string,
            IReadOnlyList<RoutingService.RouteInterchange>>());
}
