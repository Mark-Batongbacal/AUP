using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteSegmentRepository
{
    Task<List<route_segment>> GetOrderedSegmentsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<route_segment?> GetByIdAsync(long segmentId, CancellationToken cancellationToken = default);

    Task<List<route_segment>> GetFromStopAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<List<route_segment>> GetToStopAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<route_segment> AddAsync(route_segment segment, CancellationToken cancellationToken = default);

    Task<route_segment> UpdateAsync(route_segment segment, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(long segmentId, CancellationToken cancellationToken = default);
}
