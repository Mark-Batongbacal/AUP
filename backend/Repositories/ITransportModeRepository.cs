using backend.Models.Database;

namespace backend.Repositories;

public interface ITransportModeRepository
{
    Task<List<transport_mode>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<transport_mode?> GetByIdAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<transport_mode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<transport_mode?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
