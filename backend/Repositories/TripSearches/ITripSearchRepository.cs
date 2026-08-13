using backend.Models.Database;

namespace backend.Repositories;

public interface ITripSearchRepository
{
    Task<TripSearch> AddAsync(TripSearch tripSearch, CancellationToken cancellationToken = default);

    Task<TripSearch?> GetByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<List<TripSearch>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
