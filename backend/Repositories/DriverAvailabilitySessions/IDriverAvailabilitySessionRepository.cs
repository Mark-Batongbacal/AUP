using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverAvailabilitySessionRepository
{
    Task<driver_availability_session?> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<driver_availability_session>> GetAvailableSessionsAsync(CancellationToken cancellationToken = default);

    Task<driver_availability_session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<driver_availability_session> AddAsync(driver_availability_session session, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(Guid sessionId, string status, CancellationToken cancellationToken = default);

    Task<bool> EndSessionAsync(Guid sessionId, DateTime? endedAt = null, CancellationToken cancellationToken = default);
}
