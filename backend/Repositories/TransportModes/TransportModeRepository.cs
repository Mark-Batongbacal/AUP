using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for transport modes. Missing mode lookups return null.
/// </summary>
public sealed class TransportModeRepository(TukiDbContext context) : ITransportModeRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<TransportMode>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        _context.TransportModes
            .AsNoTracking()
            .Where(mode => mode.IsActive)
            .OrderBy(mode => mode.Name)
            .ToListAsync(cancellationToken);

    public Task<TransportMode?> GetByIdAsync(int transportModeId, CancellationToken cancellationToken = default) =>
        _context.TransportModes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.TransportModeId == transportModeId, cancellationToken);

    public Task<TransportMode?> GetByCodeAsync(string Code, CancellationToken cancellationToken = default) =>
        _context.TransportModes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.Code == Code, cancellationToken);

    public Task<TransportMode?> GetByNameAsync(string Name, CancellationToken cancellationToken = default) =>
        _context.TransportModes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.Name == Name, cancellationToken);
}
