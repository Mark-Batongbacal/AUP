using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverAvailabilitySessionRepository
{
    Task<DriverAvailabilitySession?> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<DriverAvailabilitySession>> GetAvailableSessionsAsync(CancellationToken cancellationToken = default);

    Task<DriverAvailabilitySession?> GetByIdAsync(long sessionId, CancellationToken cancellationToken = default);

    Task<DriverAvailabilitySession> AddAsync(DriverAvailabilitySession Session, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(long sessionId, string Status, CancellationToken cancellationToken = default);

    Task<bool> EndSessionAsync(long sessionId, DateTime? endedAt = null, CancellationToken cancellationToken = default);
}
