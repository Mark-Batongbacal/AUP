using backend.Models.Routing;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

public sealed class RouteTransferReachabilityTests
{
    [Fact]
    public void CanReachAny_RespectsDirectedTransferBudget()
    {
        var reachability = Build(
            ["A", "B", "C", "DEAD"],
            ("A", "B"),
            ("B", "C"));
        IReadOnlySet<string> destination = new HashSet<string>(["C"]);

        Assert.False(reachability.CanReachAny("A", destination, 1));
        Assert.True(reachability.CanReachAny("A", destination, 2));
        Assert.True(reachability.CanReachAny("B", destination, 1));
        Assert.True(reachability.CanReachAny("C", destination, 0));
        Assert.False(reachability.CanReachAny("DEAD", destination, 3));
        Assert.False(reachability.CanReachAny("C", new HashSet<string>(["A"]), 3));
    }

    [Fact]
    public void CanReachAny_TreatsOccurrenceConstraintsConservatively()
    {
        // The route-level edge deliberately says nothing about whether index
        // 30 is reachable from a particular entry occurrence. Returning true
        // leaves that authoritative progress decision to transfer traversal.
        var routes = Routes("LOOP", "FINAL");
        var edges = new Dictionary<string,
            IReadOnlyList<RoutingService.RouteInterchange>>(StringComparer.Ordinal)
        {
            ["LOOP"] =
            [
                new RoutingService.RouteInterchange(
                    30,
                    "FINAL",
                    "FINAL",
                    2,
                    20)
            ]
        };
        var reachability = RouteTransferReachability.Build(routes, edges);

        Assert.True(reachability.CanReachAny(
            "LOOP",
            new HashSet<string>(["FINAL"]),
            1));
    }

    [Fact]
    public async Task ConcurrentReads_AreSafe()
    {
        var reachability = Build(
            ["A", "B", "C"],
            ("A", "B"),
            ("B", "C"));
        IReadOnlySet<string> destination = new HashSet<string>(["C"]);

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ =>
            Task.Run(() => reachability.CanReachAny("A", destination, 2))));

        Assert.All(results, Assert.True);
    }

    [Fact]
    public void EmptyNetwork_ReturnsFalse()
    {
        var reachability = RouteTransferReachability.Build(
            [],
            new Dictionary<string,
                IReadOnlyList<RoutingService.RouteInterchange>>());

        Assert.False(reachability.CanReachAny(
            "missing",
            new HashSet<string>(["destination"]),
            2));
    }

    private static IRouteTransferReachability Build(
        IReadOnlyList<string> routeIds,
        params (string From, string To)[] connections)
    {
        var routes = Routes(routeIds.ToArray());
        var edges = connections
            .GroupBy(connection => connection.From, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RoutingService.RouteInterchange>)group
                    .Select(connection => new RoutingService.RouteInterchange(
                        1,
                        connection.To,
                        connection.To,
                        1,
                        20))
                    .ToList(),
                StringComparer.Ordinal);
        return RouteTransferReachability.Build(routes, edges);
    }

    private static IReadOnlyList<StaticJeepneyRoute> Routes(
        params string[] routeIds) => routeIds
        .Select(routeId => new StaticJeepneyRoute
        {
            RouteId = routeId,
            RouteName = routeId,
            Coordinates =
            [
                [120.5, 15.0],
                [120.51, 15.01]
            ]
        })
        .ToList();
}
