using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for tricycle service areas represented by a center point and radius.
/// </summary>
public sealed class TricyclePointRepository(TukiDbContext context) : ITricyclePointRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<TricyclePoint>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        QueryWithStop()
            .Where(point => point.IsActive)
            .OrderBy(point => point.PointName)
            .ToListAsync(cancellationToken);

    public Task<TricyclePoint?> GetByIdAsync(
        long tricyclePointId,
        CancellationToken cancellationToken = default) =>
        QueryWithStop()
            .FirstOrDefaultAsync(
                point => point.TricyclePointId == tricyclePointId,
                cancellationToken);

    public Task<TricyclePoint?> GetByPointCodeAsync(
        string pointCode,
        CancellationToken cancellationToken = default) =>
        QueryWithStop()
            .FirstOrDefaultAsync(point => point.PointCode == pointCode, cancellationToken);

    public Task<TricyclePoint?> GetByStopIdAsync(
        long stopId,
        CancellationToken cancellationToken = default) =>
        QueryWithStop()
            .FirstOrDefaultAsync(point => point.StopId == stopId, cancellationToken);

    public async Task<TricyclePoint> AddAsync(
        TricyclePoint tricyclePoint,
        CancellationToken cancellationToken = default)
    {
        await _context.TricyclePoints.AddAsync(tricyclePoint, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tricyclePoint;
    }

    public async Task<TricyclePoint> UpdateAsync(
        TricyclePoint tricyclePoint,
        CancellationToken cancellationToken = default)
    {
        _context.TricyclePoints.Update(tricyclePoint);
        await _context.SaveChangesAsync(cancellationToken);
        return tricyclePoint;
    }

    private IQueryable<TricyclePoint> QueryWithStop() =>
        _context.TricyclePoints
            .AsNoTracking()
            .Include(point => point.Stop);
}
