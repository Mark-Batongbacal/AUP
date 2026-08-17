using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportModeRepository
{
    Task<List<TransportMode>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TransportMode?> GetByIdAsync(int transportModeId, CancellationToken cancellationToken = default);

    Task<TransportMode?> GetByCodeAsync(string Code, CancellationToken cancellationToken = default);

    Task<TransportMode?> GetByNameAsync(string Name, CancellationToken cancellationToken = default);
}
