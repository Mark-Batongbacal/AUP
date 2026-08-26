using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport routes. Methods that include navigation properties return null when
/// the requested Route does not exist.
/// </summary>
public sealed class TransportRouteRepository(TukiDbContext context) : ITransportRouteRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<TransportRoute>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Where(Route => Route.IsActive)
            .OrderBy(Route => Route.RouteName)
            .ToListAsync(cancellationToken);

    public Task<List<TransportRoute>> GetAllActiveWithOrderedPointsAsync(
        CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(route => route.TransportMode)
            .Include(route => route.RoutePoints.OrderBy(point => point.PointOrder))
            .Where(route => route.IsActive)
            .OrderBy(route => route.RouteName)
            .ToListAsync(cancellationToken);

    public Task<List<TransportRoute>> GetAllByTransportModeCodeForAdminAsync(
        string transportModeCode,
        bool includeActive,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TransportRoutes
            .AsNoTracking()
            .Include(route => route.TransportMode)
            .Include(route => route.RoutePoints.OrderBy(point => point.PointOrder))
            .Include(route => route.RouteWaypoints.OrderBy(point => point.WaypointOrder))
            .Where(route => route.TransportMode.Code == transportModeCode);

        if (!includeActive)
            query = query.Where(route => !route.IsActive);
        else if (!includeInactive)
            query = query.Where(route => route.IsActive);

        return query
            .OrderBy(route => route.RouteName)
            .ThenBy(route => route.RouteCode)
            .ToListAsync(cancellationToken);
    }

    public Task<TransportRoute?> GetByIdAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetByIdWithPointsForAdminAsync(
        long routeId,
        CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(route => route.TransportMode)
            .Include(route => route.RoutePoints.OrderBy(point => point.PointOrder))
            .Include(route => route.RouteWaypoints.OrderBy(point => point.WaypointOrder))
            .FirstOrDefaultAsync(route => route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetTrackedByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .Include(route => route.TransportMode)
            .FirstOrDefaultAsync(route => route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetByRouteCodeAsync(string routeCode, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(Route => Route.RouteCode == routeCode, cancellationToken);

    public Task<TransportRoute?> GetLatestWithPolylineAsync(CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Where(route => route.EncodedPolyline != null)
            .OrderByDescending(route => route.CreatedAt)
            .ThenByDescending(route => route.RouteId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<TransportRoute>> GetByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Where(Route => Route.TransportModeId == transportModeId && Route.IsActive)
            .OrderBy(Route => Route.RouteName)
            .ToListAsync(cancellationToken);

    public Task<TransportRoute?> GetWithEndpointsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.TransportMode)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetWithRouteStopsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteStops)
                .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<TransportRoute?> GetWithOrderedRouteStopsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteStops.OrderBy(routeStop => routeStop.StopOrder))
                .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<List<RouteStop>> GetOrderedRouteStopsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.RouteStops
            .AsNoTracking()
            .Include(routeStop => routeStop.Stop)
            .Where(routeStop => routeStop.RouteId == routeId)
            .OrderBy(routeStop => routeStop.StopOrder)
            .ToListAsync(cancellationToken);

    public Task<TransportRoute?> GetWithRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.TransportRoutes
            .AsNoTracking()
            .Include(Route => Route.RouteSegments.Where(segment => segment.IsActive).OrderBy(segment => segment.SegmentOrder))
                .ThenInclude(segment => segment.FromRouteStop)
                    .ThenInclude(routeStop => routeStop.Stop)
            .Include(Route => Route.RouteSegments.Where(segment => segment.IsActive).OrderBy(segment => segment.SegmentOrder))
                .ThenInclude(segment => segment.ToRouteStop)
                    .ThenInclude(routeStop => routeStop.Stop)
            .FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);

    public Task<List<RouteSegment>> GetOrderedRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.RouteSegments
            .AsNoTracking()
            .Include(segment => segment.FromRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Include(segment => segment.ToRouteStop)
                .ThenInclude(routeStop => routeStop.Stop)
            .Where(segment => segment.RouteId == routeId && segment.IsActive)
            .OrderBy(segment => segment.SegmentOrder)
            .ToListAsync(cancellationToken);

    public async Task<TransportRoute> AddAsync(TransportRoute Route, CancellationToken cancellationToken = default)
    {
        await _context.TransportRoutes.AddAsync(Route, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Route;
    }

    public async Task<TransportRoute> ReplaceAsync(
        long routeId,
        TransportRoute replacement,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.TransportRoutes
            .Include(route => route.RoutePoints)
            .Include(route => route.RouteWaypoints)
            .SingleAsync(route => route.RouteId == routeId, cancellationToken);

        _context.RoutePoints.RemoveRange(existing.RoutePoints);
        _context.RouteWaypoints.RemoveRange(existing.RouteWaypoints);

        existing.RouteName = replacement.RouteName;
        existing.TransportModeId = replacement.TransportModeId;
        existing.OriginName = replacement.OriginName;
        existing.DestinationName = replacement.DestinationName;
        existing.RouteDescription = replacement.RouteDescription;
        existing.EncodedPolyline = replacement.EncodedPolyline;
        existing.BaseFare = replacement.BaseFare;
        existing.IsActive = replacement.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.RoutePoints = replacement.RoutePoints;
        existing.RouteWaypoints = replacement.RouteWaypoints;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<TransportRoute?> ReplaceDraftGeometryAsync(
        long routeId,
        IReadOnlyList<RoutePoint> routePoints,
        IReadOnlyList<RouteWaypoint> routeWaypoints,
        string encodedPolyline,
        CancellationToken cancellationToken = default)
    {
        var route = await _context.TransportRoutes
            .Include(item => item.TransportMode)
            .Include(item => item.RoutePoints)
            .Include(item => item.RouteWaypoints)
            .SingleOrDefaultAsync(
                item => item.RouteId == routeId &&
                        !item.IsActive &&
                        item.TransportMode.Code == "JEEPNEY",
                cancellationToken);

        if (route is null)
            return null;

        _context.RoutePoints.RemoveRange(route.RoutePoints);
        _context.RouteWaypoints.RemoveRange(route.RouteWaypoints);

        route.RoutePoints = routePoints.ToList();
        route.RouteWaypoints = routeWaypoints.ToList();
        route.EncodedPolyline = encodedPolyline;
        route.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return route;
    }

    public async Task<TransportRoute?> UpdateJeepneyDraftMetadataAsync(
        long routeId,
        string routeCode,
        string routeName,
        string originName,
        string destinationName,
        string? directionName,
        string? operatorName,
        string? description,
        decimal? baseFare,
        CancellationToken cancellationToken = default)
    {
        var updatedAt = DateTime.UtcNow;
        var affected = await _context.TransportRoutes
            .Where(route =>
                route.RouteId == routeId &&
                !route.IsActive &&
                route.TransportMode.Code == "JEEPNEY")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(route => route.RouteCode, routeCode)
                .SetProperty(route => route.RouteName, routeName)
                .SetProperty(route => route.OriginName, originName)
                .SetProperty(route => route.DestinationName, destinationName)
                .SetProperty(route => route.DirectionName, directionName)
                .SetProperty(route => route.OperatorName, operatorName)
                .SetProperty(route => route.RouteDescription, description)
                .SetProperty(route => route.BaseFare, baseFare)
                .SetProperty(route => route.UpdatedAt, updatedAt),
                cancellationToken);

        if (affected != 1)
            return null;

        return await GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
    }

    public async Task<TransportRoute?> PublishReadyJeepneyDraftAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var publishedAt = DateTime.UtcNow;
        var affected = await _context.TransportRoutes
            .Where(route =>
                route.RouteId == routeId &&
                !route.IsActive &&
                route.TransportMode.Code == "JEEPNEY" &&
                route.RouteCode != "" &&
                route.RouteName != "" &&
                route.OriginName != "" &&
                route.DestinationName != "" &&
                route.EncodedPolyline != null &&
                route.EncodedPolyline != "" &&
                route.RoutePoints.Count >= 2 &&
                route.RouteWaypoints.Count >= 2)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(route => route.IsActive, true)
                .SetProperty(route => route.UpdatedAt, publishedAt),
                cancellationToken);

        if (affected != 1)
            return null;

        return await GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
    }

    public async Task<TransportRoute> UpdateAsync(TransportRoute Route, CancellationToken cancellationToken = default)
    {
        _context.TransportRoutes.Update(Route);
        await _context.SaveChangesAsync(cancellationToken);
        return Route;
    }

    public async Task<bool> DeactivateAsync(long routeId, CancellationToken cancellationToken = default)
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

    public async Task<bool> ActivateAsync(long routeId, CancellationToken cancellationToken = default)
    {
        var Route = await _context.TransportRoutes.FirstOrDefaultAsync(Route => Route.RouteId == routeId, cancellationToken);
        if (Route is null)
        {
            return false;
        }

        Route.IsActive = true;
        Route.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
