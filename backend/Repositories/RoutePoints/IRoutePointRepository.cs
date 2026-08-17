using backend.Models.Database;

namespace backend.Repositories;

public interface IRoutePointRepository
{
    Task<List<RoutePoint>> GetOrderedByRouteAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RoutePoint>> ReplaceForRouteAsync(
        long routeId,
        IReadOnlyList<RoutePoint> routePoints,
        CancellationToken cancellationToken = default);
}
