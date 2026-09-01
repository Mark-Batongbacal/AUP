using backend.Models.Routing;

namespace backend.Services.Routing;

internal interface IRouteTransferReachability
{
    bool CanReachAny(
        string routeId,
        IReadOnlySet<string> destinationRouteIds,
        int remainingTransfers);
}

/// <summary>
/// Conservative route-level lower bounds for the number of transfers needed
/// to reach another route. Occurrence and progress constraints remain the
/// responsibility of the live transfer traversal: ignoring those constraints
/// here can only underestimate the required transfers, so a negative answer
/// is safe while a positive answer may deliberately be a false positive.
/// </summary>
internal sealed class RouteTransferReachability : IRouteTransferReachability
{
    private readonly IReadOnlyDictionary<string, int> _routeOrdinals;
    private readonly int[][] _minimumTransfers;

    private RouteTransferReachability(
        IReadOnlyDictionary<string, int> routeOrdinals,
        int[][] minimumTransfers)
    {
        _routeOrdinals = routeOrdinals;
        _minimumTransfers = minimumTransfers;
    }

    public static IRouteTransferReachability Build(
        IReadOnlyList<StaticJeepneyRoute> routes,
        IReadOnlyDictionary<string,
            IReadOnlyList<RoutingService.RouteInterchange>> interchangesByRoute)
    {
        var routeOrdinals = routes
            .Select((route, ordinal) => (route.RouteId, ordinal))
            .ToDictionary(
                pair => pair.RouteId,
                pair => pair.ordinal,
                StringComparer.Ordinal);
        var adjacency = new int[routes.Count][];
        for (var ordinal = 0; ordinal < routes.Count; ordinal++)
        {
            var routeId = routes[ordinal].RouteId;
            adjacency[ordinal] = interchangesByRoute.TryGetValue(
                    routeId,
                    out var edges)
                ? edges
                    .Where(edge => !string.Equals(
                        routeId,
                        edge.OtherRouteId,
                        StringComparison.Ordinal))
                    .Select(edge => routeOrdinals.GetValueOrDefault(
                        edge.OtherRouteId,
                        -1))
                    .Where(other => other >= 0)
                    .Distinct()
                    .ToArray()
                : [];
        }

        var minimumTransfers = new int[routes.Count][];
        for (var source = 0; source < routes.Count; source++)
        {
            var distances = Enumerable.Repeat(int.MaxValue, routes.Count).ToArray();
            var queue = new Queue<int>();
            distances[source] = 0;
            queue.Enqueue(source);
            while (queue.TryDequeue(out var current))
            {
                var nextDistance = distances[current] + 1;
                foreach (var next in adjacency[current])
                {
                    if (distances[next] <= nextDistance)
                        continue;

                    distances[next] = nextDistance;
                    queue.Enqueue(next);
                }
            }

            minimumTransfers[source] = distances;
        }

        return new RouteTransferReachability(routeOrdinals, minimumTransfers);
    }

    public bool CanReachAny(
        string routeId,
        IReadOnlySet<string> destinationRouteIds,
        int remainingTransfers)
    {
        if (remainingTransfers < 0 ||
            !_routeOrdinals.TryGetValue(routeId, out var source))
        {
            return false;
        }

        foreach (var destinationRouteId in destinationRouteIds)
        {
            if (_routeOrdinals.TryGetValue(
                    destinationRouteId,
                    out var destination) &&
                _minimumTransfers[source][destination] <= remainingTransfers)
            {
                return true;
            }
        }

        return false;
    }
}
