using backend.Models.Database;

namespace backend.Repositories;

public interface ITransferConnectionRepository
{
    Task<List<TransferConnection>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TransferConnection?> GetByIdAsync(long transferConnectionId, CancellationToken cancellationToken = default);

    Task<List<TransferConnection>> GetActiveForStopAsync(long stopId, CancellationToken cancellationToken = default);

    Task<TransferConnection?> GetActiveByStopsAsync(
        long fromStopId,
        long toStopId,
        CancellationToken cancellationToken = default);

    Task<TransferConnection> AddAsync(
        TransferConnection transferConnection,
        CancellationToken cancellationToken = default);

    Task<TransferConnection> UpdateAsync(
        TransferConnection transferConnection,
        CancellationToken cancellationToken = default);
}
