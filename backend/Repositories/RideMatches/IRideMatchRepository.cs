using backend.Models.Database;

namespace backend.Repositories;

public interface IRideMatchRepository
{
    Task<List<RideMatch>> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<RideMatch>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<RideMatch?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<RideMatch> AddAsync(RideMatch match, CancellationToken cancellationToken = default);

    Task<RideMatch> UpdateAsync(RideMatch match, CancellationToken cancellationToken = default);
}
