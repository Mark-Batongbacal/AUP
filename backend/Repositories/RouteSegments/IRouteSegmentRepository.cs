using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteSegmentRepository
{
    Task<List<RouteSegment>> GetOrderedSegmentsForRouteAsync(long routeId, CancellationToken cancellationToken = default);

    Task<RouteSegment?> GetByIdAsync(long segmentId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetFromStopAsync(long stopId, CancellationToken cancellationToken = default);

    Task<List<RouteSegment>> GetToStopAsync(long stopId, CancellationToken cancellationToken = default);

    Task<RouteSegment> AddAsync(RouteSegment segment, CancellationToken cancellationToken = default);

    Task<RouteSegment> UpdateAsync(RouteSegment segment, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(long segmentId, CancellationToken cancellationToken = default);
}
