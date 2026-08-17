using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for Route segments. Route segment sequences are ordered by SegmentOrder.
/// </summary>
public sealed class RouteSegmentRepository(TukiDbContext context) : IRouteSegmentRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<RouteSegment>> GetOrderedSegmentsForRouteAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.FromRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Include(segment => segment.ToRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Where(segment => segment.RouteId == routeId && segment.IsActive)
            .OrderBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public Task<RouteSegment?> GetByIdAsync(long segmentId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.FromRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Include(segment => segment.ToRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(segment => segment.SegmentId == segmentId, cancellationToken);

    public Task<List<RouteSegment>> GetFromStopAsync(long stopId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.ToRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Where(segment => segment.FromRouteStop.StopId == stopId && segment.IsActive)
            .OrderBy(segment => segment.Route.RouteName)
            .ThenBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public Task<List<RouteSegment>> GetToStopAsync(long stopId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.FromRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Where(segment => segment.ToRouteStop.StopId == stopId && segment.IsActive)
            .OrderBy(segment => segment.Route.RouteName)
            .ThenBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public async Task<RouteSegment> AddAsync(RouteSegment segment, CancellationToken cancellationToken = default)
    {
        await _context.RouteSegments.AddAsync(segment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return segment;
    }

    public async Task<RouteSegment> UpdateAsync(RouteSegment segment, CancellationToken cancellationToken = default)
    {
        _context.RouteSegments.Update(segment);
        await _context.SaveChangesAsync(cancellationToken);
        return segment;
    }

    public async Task<bool> DeactivateAsync(long segmentId, CancellationToken cancellationToken = default)
    {
        var segment = await _context.RouteSegments.FirstOrDefaultAsync(segment => segment.SegmentId == segmentId, cancellationToken);
        if (segment is null)
        {
            return false;
        }

        segment.IsActive = false;
        segment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
