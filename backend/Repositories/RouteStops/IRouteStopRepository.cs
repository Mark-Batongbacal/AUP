using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteStopRepository
{
    Task<List<RouteStop>> GetOrderedStopsForRouteAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetRoutesForStopAsync(long stopId, CancellationToken cancellationToken = default);

    Task<RouteStop?> GetByIdAsync(long routeStopId, CancellationToken cancellationToken = default);

    Task<RouteStop> AddAsync(RouteStop routeStop, CancellationToken cancellationToken = default);

    Task<bool> UpdateStopOrderAsync(long routeStopId, int stopOrder, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(long routeStopId, CancellationToken cancellationToken = default);
}
