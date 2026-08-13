using backend.Models.Database;

namespace backend.Repositories;

public interface ITripSearchRepository
{
    Task<trip_search> AddAsync(trip_search tripSearch, CancellationToken cancellationToken = default);

    Task<trip_search?> GetByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<List<trip_search>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
