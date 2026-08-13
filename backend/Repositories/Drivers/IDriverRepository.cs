using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverRepository
{
    Task<driver?> GetByIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<driver>> GetAvailableDriversAsync(CancellationToken cancellationToken = default);

    Task<driver?> GetWithHomeTerminalAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<driver?> GetWithVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<driver> AddAsync(driver driver, CancellationToken cancellationToken = default);

    Task<driver> UpdateAsync(driver driver, CancellationToken cancellationToken = default);

    Task<bool> UpdateAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default);

    Task<bool> UpdateVerificationStatusAsync(Guid driverId, string verificationStatus, CancellationToken cancellationToken = default);
}
