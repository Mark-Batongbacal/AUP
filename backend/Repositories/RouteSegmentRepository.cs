using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for route segments. Route segment sequences are ordered by segment_order.
/// </summary>
public sealed class RouteSegmentRepository(SupabaseDbContext context) : IRouteSegmentRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<route_segment>> GetOrderedSegmentsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.route_segments
            .AsNoTracking()
            .Include(segment => segment.from_stop)
            .Include(segment => segment.to_stop)
            .Where(segment => segment.route_id == routeId && segment.is_active)
            .OrderBy(segment => segment.segment_order)
            .ToListAsync(cancellationToken);

    public Task<route_segment?> GetByIdAsync(long segmentId, CancellationToken cancellationToken = default) =>
        _context.route_segments
            .AsNoTracking()
            .Include(segment => segment.route)
            .Include(segment => segment.from_stop)
            .Include(segment => segment.to_stop)
            .FirstOrDefaultAsync(segment => segment.segment_id == segmentId, cancellationToken);

    public Task<List<route_segment>> GetFromStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.route_segments
            .AsNoTracking()
            .Include(segment => segment.route)
            .Include(segment => segment.to_stop)
            .Where(segment => segment.from_stop_id == stopId && segment.is_active)
            .OrderBy(segment => segment.route.route_name)
            .ThenBy(segment => segment.segment_order)
            .ToListAsync(cancellationToken);

    public Task<List<route_segment>> GetToStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.route_segments
            .AsNoTracking()
            .Include(segment => segment.route)
            .Include(segment => segment.from_stop)
            .Where(segment => segment.to_stop_id == stopId && segment.is_active)
            .OrderBy(segment => segment.route.route_name)
            .ThenBy(segment => segment.segment_order)
            .ToListAsync(cancellationToken);

    public async Task<route_segment> AddAsync(route_segment segment, CancellationToken cancellationToken = default)
    {
        await _context.route_segments.AddAsync(segment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return segment;
    }

    public async Task<route_segment> UpdateAsync(route_segment segment, CancellationToken cancellationToken = default)
    {
        _context.route_segments.Update(segment);
        await _context.SaveChangesAsync(cancellationToken);
        return segment;
    }

    public async Task<bool> DeactivateAsync(long segmentId, CancellationToken cancellationToken = default)
    {
        var segment = await _context.route_segments.FirstOrDefaultAsync(segment => segment.segment_id == segmentId, cancellationToken);
        if (segment is null)
        {
            return false;
        }

        segment.is_active = false;
        segment.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
