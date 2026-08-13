using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport stops. Missing Stop lookups return null.
/// </summary>
public sealed class TransportStopRepository(SupabaseDbContext context) : ITransportStopRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<TransportStop>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.TransportStops
            .AsNoTracking()
            .Where(Stop => Stop.IsActive)
            .OrderBy(Stop => Stop.Name)
            .ToListAsync(cancellationToken);

    public Task<TransportStop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.TransportStops
            .AsNoTracking()
            .FirstOrDefaultAsync(Stop => Stop.StopId == stopId, cancellationToken);

    public Task<TransportStop?> GetByStopCodeAsync(string stopCode, CancellationToken cancellationToken = default) =>
        _context.TransportStops
            .AsNoTracking()
            .FirstOrDefaultAsync(Stop => Stop.StopCode == stopCode, cancellationToken);

    public Task<List<TransportStop>> SearchByNameAsync(string Name, CancellationToken cancellationToken = default) =>
        _context.TransportStops
            .AsNoTracking()
            .Where(Stop => Stop.IsActive && EF.Functions.ILike(Stop.Name, $"%{Name}%"))
            .OrderBy(Stop => Stop.Name)
            .ToListAsync(cancellationToken);

    public async Task<TransportStop> AddAsync(TransportStop Stop, CancellationToken cancellationToken = default)
    {
        await _context.TransportStops.AddAsync(Stop, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Stop;
    }

    public async Task<TransportStop> UpdateAsync(TransportStop Stop, CancellationToken cancellationToken = default)
    {
        _context.TransportStops.Update(Stop);
        await _context.SaveChangesAsync(cancellationToken);
        return Stop;
    }

    public async Task<bool> DeactivateAsync(Guid stopId, CancellationToken cancellationToken = default)
    {
        var Stop = await _context.TransportStops.FirstOrDefaultAsync(Stop => Stop.StopId == stopId, cancellationToken);
        if (Stop is null)
        {
            return false;
        }

        Stop.IsActive = false;
        Stop.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
