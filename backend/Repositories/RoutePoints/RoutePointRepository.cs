using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for ordered route geometry points.
/// </summary>
public sealed class RoutePointRepository(TukiDbContext context) : IRoutePointRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<RoutePoint>> GetOrderedByRouteAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.RoutePoints
            .AsNoTracking()
            .Where(routePoint => routePoint.RouteId == routeId)
            .OrderBy(routePoint => routePoint.PointOrder)
            .ToListAsync(cancellationToken);

    public async Task<List<RoutePoint>> ReplaceForRouteAsync(
        long routeId,
        IReadOnlyList<RoutePoint> routePoints,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var existingRoutePoints = await _context.RoutePoints
                .Where(routePoint => routePoint.RouteId == routeId)
                .ToListAsync(cancellationToken);

            _context.RoutePoints.RemoveRange(existingRoutePoints);
            await _context.SaveChangesAsync(cancellationToken);

            if (routePoints.Count > 0)
            {
                await _context.RoutePoints.AddRangeAsync(routePoints, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return routePoints
                .OrderBy(routePoint => routePoint.PointOrder)
                .ToList();
        });
    }
}
