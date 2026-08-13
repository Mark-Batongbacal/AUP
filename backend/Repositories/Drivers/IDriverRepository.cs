using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<Driver>> GetAvailableDriversAsync(CancellationToken cancellationToken = default);

    Task<Driver?> GetWithHomeTerminalAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Driver?> GetWithVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Driver> AddAsync(Driver Driver, CancellationToken cancellationToken = default);

    Task<Driver> UpdateAsync(Driver Driver, CancellationToken cancellationToken = default);

    Task<bool> UpdateAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default);

    Task<bool> UpdateVerificationStatusAsync(Guid driverId, string verificationStatus, CancellationToken cancellationToken = default);
}
