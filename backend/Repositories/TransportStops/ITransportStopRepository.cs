using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportStopRepository
{
    Task<List<TransportStop>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TransportStop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<TransportStop?> GetByStopCodeAsync(string stopCode, CancellationToken cancellationToken = default);

    Task<List<TransportStop>> SearchByNameAsync(string Name, CancellationToken cancellationToken = default);

    Task<TransportStop> AddAsync(TransportStop Stop, CancellationToken cancellationToken = default);

    Task<TransportStop> UpdateAsync(TransportStop Stop, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid stopId, CancellationToken cancellationToken = default);
}
