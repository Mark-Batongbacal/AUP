using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportRouteRepository
{
    Task<List<TransportRoute>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetAllActiveWithOrderedPointsAsync(
        CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetAllByTransportModeCodeForAdminAsync(
        string transportModeCode,
        bool includeActive,
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetByIdAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetByIdWithPointsForAdminAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetTrackedByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetLatestWithPolylineAsync(CancellationToken cancellationToken = default);

    Task<List<TransportRoute>> GetByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithEndpointsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithRouteStopsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithOrderedRouteStopsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RouteStop>> GetOrderedRouteStopsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute?> GetWithRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetOrderedRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default);

    Task<TransportRoute> AddAsync(TransportRoute Route, CancellationToken cancellationToken = default);

    Task<TransportRoute> ReplaceAsync(long routeId, TransportRoute replacement, CancellationToken cancellationToken = default);

    Task<TransportRoute?> ReplaceDraftGeometryAsync(
        long routeId,
        IReadOnlyList<RoutePoint> routePoints,
        IReadOnlyList<RouteWaypoint> routeWaypoints,
        string encodedPolyline,
        CancellationToken cancellationToken = default);

    Task<TransportRoute> UpdateAsync(TransportRoute Route, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(long routeId, CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(long routeId, CancellationToken cancellationToken = default);
}
