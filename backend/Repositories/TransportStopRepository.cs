using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport stops. Missing stop lookups return null.
/// </summary>
public sealed class TransportStopRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<transport_stop>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.transport_stops
            .AsNoTracking()
            .Where(stop => stop.is_active)
            .OrderBy(stop => stop.name)
            .ToListAsync(cancellationToken);

    public Task<transport_stop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default) =>
        _context.transport_stops
            .AsNoTracking()
            .FirstOrDefaultAsync(stop => stop.stop_id == stopId, cancellationToken);

    public Task<transport_stop?> GetByStopCodeAsync(string stopCode, CancellationToken cancellationToken = default) =>
        _context.transport_stops
            .AsNoTracking()
            .FirstOrDefaultAsync(stop => stop.stop_code == stopCode, cancellationToken);

    public Task<List<transport_stop>> SearchByNameAsync(string name, CancellationToken cancellationToken = default) =>
        _context.transport_stops
            .AsNoTracking()
            .Where(stop => stop.is_active && EF.Functions.ILike(stop.name, $"%{name}%"))
            .OrderBy(stop => stop.name)
            .ToListAsync(cancellationToken);

    public async Task<transport_stop> AddAsync(transport_stop stop, CancellationToken cancellationToken = default)
    {
        await _context.transport_stops.AddAsync(stop, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return stop;
    }

    public async Task<transport_stop> UpdateAsync(transport_stop stop, CancellationToken cancellationToken = default)
    {
        _context.transport_stops.Update(stop);
        await _context.SaveChangesAsync(cancellationToken);
        return stop;
    }

    public async Task<bool> DeactivateAsync(Guid stopId, CancellationToken cancellationToken = default)
    {
        var stop = await _context.transport_stops.FirstOrDefaultAsync(stop => stop.stop_id == stopId, cancellationToken);
        if (stop is null)
        {
            return false;
        }

        stop.is_active = false;
        stop.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
