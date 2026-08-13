using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport routes. Methods that include navigation properties return null when
/// the requested route does not exist.
/// </summary>
public sealed class TransportRouteRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<transport_route>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Where(route => route.is_active)
            .OrderBy(route => route.route_name)
            .ToListAsync(cancellationToken);

    public Task<transport_route?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);

    public Task<transport_route?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .FirstOrDefaultAsync(route => route.route_code == routeCode, cancellationToken);

    public Task<List<transport_route>> GetByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Where(route => route.transport_mode_id == transportModeId && route.is_active)
            .OrderBy(route => route.route_name)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Includes the route's start stop, end stop, and transport mode.
    /// </summary>
    public Task<transport_route?> GetWithEndpointsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Include(route => route.start_stop)
            .Include(route => route.end_stop)
            .Include(route => route.transport_mode)
            .FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);

    /// <summary>
    /// Includes route-stop rows and each stop. Use GetOrderedRouteStopsAsync when ordered sequence
    /// materialization is required.
    /// </summary>
    public Task<transport_route?> GetWithRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Include(route => route.route_stops)
                .ThenInclude(routeStop => routeStop.stop)
            .FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);

    /// <summary>
    /// Includes route-stop rows and stops ordered by stop_order.
    /// </summary>
    public Task<transport_route?> GetWithOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Include(route => route.route_stops.OrderBy(routeStop => routeStop.stop_order))
                .ThenInclude(routeStop => routeStop.stop)
            .FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);

    public Task<List<route_stop>> GetOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.route_stops
            .AsNoTracking()
            .Include(routeStop => routeStop.stop)
            .Where(routeStop => routeStop.route_id == routeId)
            .OrderBy(routeStop => routeStop.stop_order)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Includes active route segments and their from/to stops ordered by segment_order.
    /// </summary>
    public Task<transport_route?> GetWithRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.transport_routes
            .AsNoTracking()
            .Include(route => route.route_segments.Where(segment => segment.is_active).OrderBy(segment => segment.segment_order))
                .ThenInclude(segment => segment.from_stop)
            .Include(route => route.route_segments.Where(segment => segment.is_active).OrderBy(segment => segment.segment_order))
                .ThenInclude(segment => segment.to_stop)
            .FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);

    /// <summary>
    /// Returns active route segments and their from/to stops ordered by segment_order.
    /// </summary>
    public Task<List<route_segment>> GetOrderedRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.route_segments
            .AsNoTracking()
            .Include(segment => segment.from_stop)
            .Include(segment => segment.to_stop)
            .Where(segment => segment.route_id == routeId && segment.is_active)
            .OrderBy(segment => segment.segment_order)
            .ToListAsync(cancellationToken);

    public async Task<transport_route> AddAsync(transport_route route, CancellationToken cancellationToken = default)
    {
        await _context.transport_routes.AddAsync(route, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return route;
    }

    public async Task<transport_route> UpdateAsync(transport_route route, CancellationToken cancellationToken = default)
    {
        _context.transport_routes.Update(route);
        await _context.SaveChangesAsync(cancellationToken);
        return route;
    }

    public async Task<bool> DeactivateAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var route = await _context.transport_routes.FirstOrDefaultAsync(route => route.route_id == routeId, cancellationToken);
        if (route is null)
        {
            return false;
        }

        route.is_active = false;
        route.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
