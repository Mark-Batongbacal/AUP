using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportRouteRepository
{
    Task<List<TransportRoute>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithEndpointsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetOrderedRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute> AddAsync(TransportRoute Route, CancellationToken cancellationToken = default);

    Task<TransportRoute> UpdateAsync(TransportRoute Route, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid routeId, CancellationToken cancellationToken = default);
}
