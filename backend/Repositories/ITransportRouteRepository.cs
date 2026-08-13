using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportRouteRepository
{
    Task<List<transport_route>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<transport_route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<transport_route?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default);

    Task<List<transport_route>> GetByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<transport_route?> GetWithEndpointsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<transport_route?> GetWithRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<transport_route?> GetWithOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<route_stop>> GetOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<transport_route?> GetWithRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<route_segment>> GetOrderedRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<transport_route> AddAsync(transport_route route, CancellationToken cancellationToken = default);

    Task<transport_route> UpdateAsync(transport_route route, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid routeId, CancellationToken cancellationToken = default);
}
