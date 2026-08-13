using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverVehicleRepository
{
    Task<List<driver_vehicle>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<driver_vehicle>> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<driver_vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<driver_vehicle> AddAsync(driver_vehicle vehicle, CancellationToken cancellationToken = default);

    Task<driver_vehicle> UpdateAsync(driver_vehicle vehicle, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
