using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport routes. Methods that include navigation properties return null when
/// the requested Route does not exist.
/// </summary>
public sealed class TransportRouteRepository(SupabaseDbContext context) : ITransportRouteRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<TransportRoute>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Where(Route => Route.IsActive)
            .OrderBy(Route => Route.RouteName)
            .ToListAsync(cancellationToken);

    public Task<TransportRoute?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(Route => Route.RouteCode == routeCode, cancellationToken);

    public Task<List<TransportRoute>> GetByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Where(Route => Route.TransportModeId == transportModeId && Route.IsActive)
            .OrderBy(Route => Route.RouteName)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Includes the Route's start Stop, end Stop, and transport mode.
    /// </summary>
    public Task<TransportRoute?> GetWithEndpointsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.StartStop)
            .Include(Route => Route.EndStop)
            .Include(Route => Route.TransportMode)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    /// <summary>
    /// Includes Route-Stop rows and each Stop. Use GetOrderedRouteStopsAsync when ordered sequence
    /// materialization is required.
    /// </summary>
    public Task<TransportRoute?> GetWithRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteStops)
                .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    /// <summary>
    /// Includes Route-Stop rows and stops ordered by StopOrder.
    /// </summary>
    public Task<TransportRoute?> GetWithOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteStops.OrderBy(routeStop => routeStop.StopOrder))
                .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<List<RouteStop>> GetOrderedRouteStopsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.RouteStops
            .AsNoTracking()
            .Include(routeStop => routeStop.Stop)
            .Where(routeStop => routeStop.RouteId == routeId)
            .OrderBy(routeStop => routeStop.StopOrder)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Includes active Route segments and their from/to stops ordered by SegmentOrder.
    /// </summary>
    public Task<TransportRoute?> GetWithRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteSegments.Where(segment => segment.IsActive).OrderBy(segment => segment.SegmentOrder))
                .ThenInclude(segment => segment.FromStop)
            .Include(Route => Route.RouteSegments.Where(segment => segment.IsActive).OrderBy(segment => segment.SegmentOrder))
                .ThenInclude(segment => segment.ToStop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    /// <summary>
    /// Returns active Route segments and their from/to stops ordered by SegmentOrder.
    /// </summary>
    public Task<List<RouteSegment>> GetOrderedRouteSegmentsAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.FromStop)
            .Include(segment => segment.ToStop)
            .Where(segment => segment.RouteId == routeId && segment.IsActive)
            .OrderBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public async Task<TransportRoute> AddAsync(TransportRoute Route, CancellationToken cancellationToken = default)
    {
        await _context.TransportRoutes.AddAsync(Route, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Route;
    }

    public async Task<TransportRoute> UpdateAsync(TransportRoute Route, CancellationToken cancellationToken = default)
    {
        _context.TransportRoutes.Update(Route);
        await _context.SaveChangesAsync(cancellationToken);
        return Route;
    }

    public async Task<bool> DeactivateAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var Route = await _context.TransportRoutes.FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);
        if (Route is null)
        {
            return false;
        }

        Route.IsActive = false;
        Route.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
