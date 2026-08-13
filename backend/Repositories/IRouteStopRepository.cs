using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteStopRepository
{
    Task<List<route_stop>> GetOrderedStopsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<route_stop>> GetRoutesForStopAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<route_stop?> GetByIdAsync(Guid routeStopId, CancellationToken cancellationToken = default);

    Task<route_stop> AddAsync(route_stop routeStop, CancellationToken cancellationToken = default);

    Task<bool> UpdateStopOrderAsync(Guid routeStopId, int stopOrder, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid routeStopId, CancellationToken cancellationToken = default);
}
