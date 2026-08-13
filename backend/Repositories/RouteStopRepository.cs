using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for route-stop relationships. Route sequences are always ordered by stop_order.
/// </summary>
public sealed class RouteStopRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<route_stop>> GetOrderedStopsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.route_stops
            .AsNoTracking()
            .Include(routeStop => routeStop.stop)
            .Where(routeStop => routeStop.route_id == routeId)
            .OrderBy(routeStop => routeStop.stop_order)
            .ToListAsync(cancellationToken);

    public Task<List<route_stop>> GetRoutesForStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.route_stops
            .AsNoTracking()
            .Include(routeStop => routeStop.route)
                .ThenInclude(route => route.transport_mode)
            .Where(routeStop => routeStop.stop_id == stopId)
            .OrderBy(routeStop => routeStop.route.route_name)
            .ThenBy(routeStop => routeStop.stop_order)
            .ToListAsync(cancellationToken);

    public Task<route_stop?> GetByIdAsync(Guid routeStopId, CancellationToken cancellationToken = default) =>
        _context.route_stops
            .AsNoTracking()
            .Include(routeStop => routeStop.route)
            .Include(routeStop => routeStop.stop)
            .FirstOrDefaultAsync(routeStop => routeStop.route_stop_id == routeStopId, cancellationToken);

    public async Task<route_stop> AddAsync(route_stop routeStop, CancellationToken cancellationToken = default)
    {
        await _context.route_stops.AddAsync(routeStop, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return routeStop;
    }

    public async Task<bool> UpdateStopOrderAsync(Guid routeStopId, int stopOrder, CancellationToken cancellationToken = default)
    {
        var routeStop = await _context.route_stops.FirstOrDefaultAsync(routeStop => routeStop.route_stop_id == routeStopId, cancellationToken);
        if (routeStop is null)
        {
            return false;
        }

        routeStop.stop_order = stopOrder;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid routeStopId, CancellationToken cancellationToken = default)
    {
        var routeStop = await _context.route_stops.FirstOrDefaultAsync(routeStop => routeStop.route_stop_id == routeStopId, cancellationToken);
        if (routeStop is null)
        {
            return false;
        }

        _context.route_stops.Remove(routeStop);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
