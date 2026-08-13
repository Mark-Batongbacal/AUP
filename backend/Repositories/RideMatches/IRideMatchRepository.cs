using backend.Models.Database;

namespace backend.Repositories;

public interface IRideMatchRepository
{
    Task<List<ride_match>> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<ride_match>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<ride_match?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<ride_match> AddAsync(ride_match match, CancellationToken cancellationToken = default);

    Task<ride_match> UpdateAsync(ride_match match, CancellationToken cancellationToken = default);
}
