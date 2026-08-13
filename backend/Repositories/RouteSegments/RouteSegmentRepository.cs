using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for Route segments. Route segment sequences are ordered by SegmentOrder.
/// </summary>
public sealed class RouteSegmentRepository(SupabaseDbContext context) : IRouteSegmentRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<RouteSegment>> GetOrderedSegmentsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.FromStop)
            .Include(segment => segment.ToStop)
            .Where(segment => segment.RouteId == routeId && segment.IsActive)
            .OrderBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public Task<RouteSegment?> GetByIdAsync(long segmentId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.FromStop)
            .Include(segment => segment.ToStop)
            .FirstOrDefaultAsync(segment => segment.SegmentId == segmentId, cancellationToken);

    public Task<List<RouteSegment>> GetFromStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.ToStop)
            .Where(segment => segment.FromStopId == stopId && segment.IsActive)
            .OrderBy(segment => segment.Route.RouteName)
            .ThenBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public Task<List<RouteSegment>> GetToStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.Route)
            .Include(segment => segment.FromStop)
            .Where(segment => segment.ToStopId == stopId && segment.IsActive)
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
