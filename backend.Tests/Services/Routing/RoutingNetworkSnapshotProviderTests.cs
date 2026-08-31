using backend.Models.Database;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class RoutingNetworkSnapshotProviderTests
{
    [Fact]
    public async Task Provider_BuildsOnceForConcurrentReaders()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var snapshots = (IRoutingNetworkSnapshotProvider)provider;
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var buildCount = 0;

        async Task<RoutingNetworkSnapshot> Build(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref buildCount);
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return EmptySnapshot();
        }

        var readers = Enumerable.Range(0, 20)
            .Select(_ => snapshots.GetSnapshotAsync(Build, default))
            .ToArray();
        await entered.Task;
        Assert.Equal(1, Volatile.Read(ref buildCount));

        release.TrySetResult();
        var results = await Task.WhenAll(readers);

        Assert.Equal(1, buildCount);
        Assert.Single(results, result => result.BuiltSnapshot);
        Assert.All(results, result => Assert.Same(
            results[0].Snapshot,
            result.Snapshot));
        Assert.Equal(1, results[0].Snapshot.Version);
    }

    [Fact]
    public async Task Invalidation_RebuildsThenAtomicallyPublishesANewVersion()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var snapshots = (IRoutingNetworkSnapshotProvider)provider;
        var buildCount = 0;

        Task<RoutingNetworkSnapshot> Build(CancellationToken _)
        {
            buildCount++;
            return Task.FromResult(EmptySnapshot());
        }

        var first = await snapshots.GetSnapshotAsync(Build, default);
        provider.Invalidate("test mutation");
        var second = await snapshots.GetSnapshotAsync(Build, default);

        Assert.Equal(2, buildCount);
        Assert.Equal(1, first.Snapshot.Version);
        Assert.Equal(2, second.Snapshot.Version);
        Assert.NotSame(first.Snapshot, second.Snapshot);
        Assert.Equal(1, first.Snapshot.Version);
    }

    [Fact]
    public async Task Invalidation_AtomicallyReplacesTheSnapshotSpatialIndex()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var snapshots = (IRoutingNetworkSnapshotProvider)provider;
        var routes = new[]
        {
            SpatialRoute("old", 15.0, 120.5)
        };

        Task<RoutingNetworkSnapshot> Build(CancellationToken _) =>
            Task.FromResult(SnapshotWithRoutes(routes));

        var first = await snapshots.GetSnapshotAsync(Build, default);
        routes = [SpatialRoute("new", 15.2, 120.7)];
        provider.Invalidate("route geometry changed");
        var second = await snapshots.GetSnapshotAsync(Build, default);

        Assert.Equal(
            ["old"],
            first.Snapshot.SpatialRouteIndex.FindNearbyRoutes(
                15.0,
                120.5,
                100));
        Assert.Empty(second.Snapshot.SpatialRouteIndex.FindNearbyRoutes(
            15.0,
            120.5,
            100));
        Assert.Equal(
            ["new"],
            second.Snapshot.SpatialRouteIndex.FindNearbyRoutes(
                15.2,
                120.7,
                100));
    }

    [Fact]
    public async Task Invalidation_AtomicallyReplacesTransferReachability()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var snapshots = (IRoutingNetworkSnapshotProvider)provider;
        var routes = new[]
        {
            SpatialRoute("A", 15.0, 120.5),
            SpatialRoute("B", 15.1, 120.6)
        };
        var connected = true;

        Task<RoutingNetworkSnapshot> Build(CancellationToken _)
        {
            var interchanges = connected
                ? new Dictionary<string,
                    IReadOnlyList<RoutingService.RouteInterchange>>
                {
                    ["A"] =
                    [
                        new RoutingService.RouteInterchange(
                            1,
                            "B",
                            "B",
                            1,
                            20)
                    ]
                }
                : new Dictionary<string,
                    IReadOnlyList<RoutingService.RouteInterchange>>();
            return Task.FromResult(SnapshotWithRoutes(routes, interchanges));
        }

        var first = await snapshots.GetSnapshotAsync(Build, default);
        connected = false;
        provider.Invalidate("interchange topology changed");
        var second = await snapshots.GetSnapshotAsync(Build, default);
        IReadOnlySet<string> destination = new HashSet<string>(["B"]);

        Assert.True(first.Snapshot.TransferReachability.CanReachAny(
            "A",
            destination,
            1));
        Assert.False(second.Snapshot.TransferReachability.CanReachAny(
            "A",
            destination,
            1));
    }

    [Fact]
    public async Task FailedRebuild_DoesNotPublishAPartialSnapshotAndCanRetry()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var snapshots = (IRoutingNetworkSnapshotProvider)provider;
        var first = await snapshots.GetSnapshotAsync(
            _ => Task.FromResult(EmptySnapshot()),
            default);
        provider.Invalidate("test failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            snapshots.GetSnapshotAsync(
                _ => throw new InvalidOperationException("build failed"),
                default));

        var recovered = await snapshots.GetSnapshotAsync(
            _ => Task.FromResult(EmptySnapshot()),
            default);
        Assert.Equal(1, first.Snapshot.Version);
        Assert.Equal(2, recovered.Snapshot.Version);
        Assert.NotSame(first.Snapshot, recovered.Snapshot);
    }

    [Fact]
    public async Task RoutingServices_SharingProvider_QueryStaticNetworkOnlyOnce()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var routes = new Mock<ITransportRouteRepository>();
        routes.Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var todas = new Mock<ITricyclePointRepository>();
        todas.Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var valhalla = new Mock<IValhallaService>(MockBehavior.Strict);

        RoutingService CreateService() => new(
            valhalla.Object,
            routes.Object,
            todas.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions()),
            tripAreaValidator: null,
            telemetry: null,
            provider);

        await CreateService().FindNearbyRoutesAsync(15.1, 120.6);
        await CreateService().FindNearbyRoutesAsync(15.1, 120.6);

        routes.Verify(repository => repository.GetAllActiveWithOrderedPointsAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        todas.Verify(repository => repository.GetAllActiveAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MalformedRoutesAreRejectedBeforeSpatialIndexConstruction()
    {
        var routes = new Mock<ITransportRouteRepository>();
        routes.Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TransportRoute
                {
                    RouteId = 1,
                    RouteCode = "MALFORMED",
                    RouteName = "Malformed",
                    IsActive = true,
                    TransportMode = new TransportMode
                    {
                        Code = "JEEPNEY",
                        Name = "Jeepney"
                    },
                    RoutePoints =
                    [
                        new RoutePoint
                        {
                            RouteId = 1,
                            PointOrder = 0,
                            Latitude = double.NaN,
                            Longitude = 120.5
                        },
                        new RoutePoint
                        {
                            RouteId = 1,
                            PointOrder = 1,
                            Latitude = 15.0,
                            Longitude = 120.6
                        }
                    ]
                }
            ]);
        var todas = new Mock<ITricyclePointRepository>();
        todas.Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var valhalla = new Mock<IValhallaService>(MockBehavior.Strict);
        var service = new RoutingService(
            valhalla.Object,
            routes.Object,
            todas.Object,
            NullLogger<RoutingService>.Instance,
            Options.Create(new RoutingOptions()));

        var result = await service.FindNearbyRoutesAsync(15.0, 120.5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task RequestScope_KeepsPreferredAndFallbackPassesOnOneVersion()
    {
        using var provider = new RoutingNetworkSnapshotProvider();
        var scope = new RoutingNetworkSnapshotScope();
        var routes = new Mock<ITransportRouteRepository>();
        routes.Setup(repository => repository.GetAllActiveWithOrderedPointsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var todas = new Mock<ITricyclePointRepository>();
        todas.Setup(repository => repository.GetAllActiveAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var valhalla = new Mock<IValhallaService>(MockBehavior.Strict);

        RoutingService CreateService(RoutingNetworkSnapshotScope requestScope) =>
            new(
                valhalla.Object,
                routes.Object,
                todas.Object,
                NullLogger<RoutingService>.Instance,
                Options.Create(new RoutingOptions()),
                tripAreaValidator: null,
                telemetry: null,
                provider,
                requestScope);

        await CreateService(scope).FindNearbyRoutesAsync(15.1, 120.6);

        provider.Invalidate("mutation between routing passes");
        await CreateService(scope).FindNearbyRoutesAsync(15.1, 120.6);

        routes.Verify(repository => repository.GetAllActiveWithOrderedPointsAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        todas.Verify(repository => repository.GetAllActiveAsync(
            It.IsAny<CancellationToken>()), Times.Once);

        await CreateService(new RoutingNetworkSnapshotScope())
            .FindNearbyRoutesAsync(15.1, 120.6);
        routes.Verify(repository => repository.GetAllActiveWithOrderedPointsAsync(
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        todas.Verify(repository => repository.GetAllActiveAsync(
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

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
            IReadOnlyList<RoutingService.RouteInterchange>>(),
        TransferReachability: RouteTransferReachability.Build(
            [],
            new Dictionary<string,
                IReadOnlyList<RoutingService.RouteInterchange>>()),
        SpatialRouteIndex: RouteSpatialIndex.Build([]),
        RoutesWithTodaAccess: new HashSet<string>(StringComparer.Ordinal));

    private static RoutingNetworkSnapshot SnapshotWithRoutes(
        IReadOnlyList<backend.Models.Routing.StaticJeepneyRoute> routes,
        IReadOnlyDictionary<string,
            IReadOnlyList<RoutingService.RouteInterchange>>? interchanges = null)
    {
        interchanges ??= new Dictionary<string,
            IReadOnlyList<RoutingService.RouteInterchange>>();
        return new(
        Version: 0,
        Routes: routes,
        TrikePoints: [],
        RouteSamples: new Dictionary<string,
            IReadOnlyList<(double Latitude, double Longitude)>>(),
        RouteGeometries: new Dictionary<string, RoutingService.FullRouteGeometry>(),
        RouteSearchAnchors: new Dictionary<string,
            IReadOnlyList<RoutingService.RouteAnchor>>(),
        InterchangesByRoute: interchanges,
        TransferReachability: RouteTransferReachability.Build(
            routes,
            interchanges),
        SpatialRouteIndex: RouteSpatialIndex.Build(routes),
        RoutesWithTodaAccess: new HashSet<string>(StringComparer.Ordinal));
    }

    private static backend.Models.Routing.StaticJeepneyRoute SpatialRoute(
        string id,
        double latitude,
        double longitude) => new()
        {
            RouteId = id,
            RouteName = id,
            Coordinates =
            [
                [longitude, latitude],
                [longitude + 0.001, latitude + 0.001]
            ]
        };
}
