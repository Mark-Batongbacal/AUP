using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for Route-Stop relationships. Route sequences are always ordered by StopOrder.
/// </summary>
public sealed class RouteStopRepository(SupabaseDbContext context) : IRouteStopRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<RouteStop>> GetOrderedStopsForRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.RouteStops
            .AsNoTracking()
            .Include(routeStop => routeStop.Stop)
            .Where(routeStop => routeStop.RouteId == routeId)
            .OrderBy(routeStop => routeStop.StopOrder)
            .ToListAsync(cancellationToken);

    public Task<List<RouteStop>> GetRoutesForStopAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.RouteStops
            .AsNoTracking()
            .Include(routeStop => routeStop.Route)
                .ThenInclude(Route => Route.TransportMode)
            .Where(routeStop => routeStop.StopId == stopId)
            .OrderBy(routeStop => routeStop.Route.RouteName)
            .ThenBy(routeStop => routeStop.StopOrder)
            .ToListAsync(cancellationToken);

    public Task<RouteStop?> GetByIdAsync(Guid routeStopId, CancellationToken cancellationToken = default) =>
        _context.RouteStops
            .AsNoTracking()
            .Include(routeStop => routeStop.Route)
            .Include(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(routeStop => routeStop.RouteStopId == routeStopId, cancellationToken);

    public async Task<RouteStop> AddAsync(RouteStop routeStop, CancellationToken cancellationToken = default)
    {
        await _context.RouteStops.AddAsync(routeStop, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return routeStop;
    }

    public async Task<bool> UpdateStopOrderAsync(Guid routeStopId, int stopOrder, CancellationToken cancellationToken = default)
    {
        var routeStop = await _context.RouteStops.FirstOrDefaultAsync(routeStop => routeStop.RouteStopId == routeStopId, cancellationToken);
        if (routeStop is null)
        {
            return false;
        }

        routeStop.StopOrder = stopOrder;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid routeStopId, CancellationToken cancellationToken = default)
    {
        var routeStop = await _context.RouteStops.FirstOrDefaultAsync(routeStop => routeStop.RouteStopId == routeStopId, cancellationToken);
        if (routeStop is null)
        {
            return false;
        }

        _context.RouteStops.Remove(routeStop);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
