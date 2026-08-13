using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteStopRepository
{
    Task<List<RouteStop>> GetOrderedStopsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetRoutesForStopAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<RouteStop?> GetByIdAsync(Guid routeStopId, CancellationToken cancellationToken = default);

    Task<RouteStop> AddAsync(RouteStop routeStop, CancellationToken cancellationToken = default);

    Task<bool> UpdateStopOrderAsync(Guid routeStopId, int stopOrder, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid routeStopId, CancellationToken cancellationToken = default);
}
