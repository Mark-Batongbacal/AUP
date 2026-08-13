using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverVehicleRepository
{
    Task<List<DriverVehicle>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<DriverVehicle>> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverVehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<DriverVehicle> AddAsync(DriverVehicle Vehicle, CancellationToken cancellationToken = default);

    Task<DriverVehicle> UpdateAsync(DriverVehicle Vehicle, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
