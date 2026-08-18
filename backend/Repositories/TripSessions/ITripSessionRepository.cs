using backend.Models.Database;

namespace backend.Repositories;

public interface ITripSessionRepository
{
    Task<TripSession> AddAsync(TripSession session, CancellationToken cancellationToken = default);
    Task<TripSession?> GetOwnedAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<TripSession?> GetActiveOwnedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TripSession> UpdateAsync(TripSession session, CancellationToken cancellationToken = default);
}
