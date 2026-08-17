using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportStopRepository
{
    Task<List<TransportStop>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TransportStop?> GetByIdAsync(long stopId, CancellationToken cancellationToken = default);

    Task<TransportStop?> GetByStopCodeAsync(string stopCode, CancellationToken cancellationToken = default);

    Task<List<TransportStop>> SearchByNameAsync(string Name, CancellationToken cancellationToken = default);

    Task<TransportStop> AddAsync(TransportStop Stop, CancellationToken cancellationToken = default);

    Task<TransportStop> UpdateAsync(TransportStop Stop, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(long stopId, CancellationToken cancellationToken = default);
}
