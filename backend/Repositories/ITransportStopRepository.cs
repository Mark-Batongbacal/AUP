using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportStopRepository
{
    Task<List<transport_stop>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<transport_stop?> GetByIdAsync(Guid stopId, CancellationToken cancellationToken = default);

    Task<transport_stop?> GetByStopCodeAsync(string stopCode, CancellationToken cancellationToken = default);

    Task<List<transport_stop>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<transport_stop> AddAsync(transport_stop stop, CancellationToken cancellationToken = default);

    Task<transport_stop> UpdateAsync(transport_stop stop, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid stopId, CancellationToken cancellationToken = default);
}
