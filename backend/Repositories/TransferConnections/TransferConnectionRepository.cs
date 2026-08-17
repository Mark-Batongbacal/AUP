using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for verified transfer links between transport stops.
/// </summary>
public sealed class TransferConnectionRepository(TukiDbContext context) : ITransferConnectionRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<TransferConnection>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        QueryWithStops()
            .Where(connection => connection.IsActive)
            .OrderBy(connection => connection.FromStop.Name)
            .ThenBy(connection => connection.ToStop.Name)
            .ToListAsync(cancellationToken);

    public Task<TransferConnection?> GetByIdAsync(
        long transferConnectionId,
        CancellationToken cancellationToken = default) =>
        QueryWithStops()
            .FirstOrDefaultAsync(
                connection => connection.TransferConnectionId == transferConnectionId,
                cancellationToken);

    public Task<List<TransferConnection>> GetActiveForStopAsync(
        long stopId,
        CancellationToken cancellationToken = default) =>
        QueryWithStops()
            .Where(connection =>
                connection.IsActive &&
                (connection.FromStopId == stopId ||
                 (connection.ToStopId == stopId && connection.IsBidirectional)))
            .OrderBy(connection => connection.FromStopId == stopId ? 0 : 1)
            .ThenBy(connection => connection.ToStop.Name)
            .ToListAsync(cancellationToken);

    public Task<TransferConnection?> GetActiveByStopsAsync(
        long fromStopId,
        long toStopId,
        CancellationToken cancellationToken = default) =>
        QueryWithStops()
            .FirstOrDefaultAsync(
                connection =>
                    connection.IsActive &&
                    connection.FromStopId == fromStopId &&
                    connection.ToStopId == toStopId,
                cancellationToken);

    public async Task<TransferConnection> AddAsync(
        TransferConnection transferConnection,
        CancellationToken cancellationToken = default)
    {
        await _context.TransferConnections.AddAsync(transferConnection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return transferConnection;
    }

    public async Task<TransferConnection> UpdateAsync(
        TransferConnection transferConnection,
        CancellationToken cancellationToken = default)
    {
        _context.TransferConnections.Update(transferConnection);
        await _context.SaveChangesAsync(cancellationToken);
        return transferConnection;
    }

    private IQueryable<TransferConnection> QueryWithStops() =>
        _context.TransferConnections
            .AsNoTracking()
            .Include(connection => connection.FromStop)
            .Include(connection => connection.ToStop);
}
