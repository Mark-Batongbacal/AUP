using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport modes. Missing mode lookups return null.
/// </summary>
public sealed class TransportModeRepository(SupabaseDbContext context) : ITransportModeRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<transport_mode>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.transport_modes
            .AsNoTracking()
            .Where(mode => mode.is_active)
            .OrderBy(mode => mode.name)
            .ToListAsync(cancellationToken);

    public Task<transport_mode?> GetByIdAsync(short transportModeId, CancellationToken cancellationToken = default) =>
        _context.transport_modes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.transport_mode_id == transportModeId, cancellationToken);

    public Task<transport_mode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.transport_modes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.code == code, cancellationToken);

    public Task<transport_mode?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        _context.transport_modes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.name == name, cancellationToken);
}
