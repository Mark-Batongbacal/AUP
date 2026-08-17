using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for vehicles registered to Drivers.
/// </summary>
public sealed class DriverVehicleRepository(TukiDbContext context) : IDriverVehicleRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<DriverVehicle>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.DriverVehicles
            .AsNoTracking()
            .Include(Vehicle => Vehicle.TransportMode)
            .Where(Vehicle => Vehicle.DriverId == driverId)
            .OrderBy(Vehicle => Vehicle.PlateNumber)
            .ToListAsync(cancellationToken);

    public Task<List<DriverVehicle>> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.DriverVehicles
            .AsNoTracking()
            .Include(Vehicle => Vehicle.TransportMode)
            .Where(Vehicle => Vehicle.DriverId == driverId && Vehicle.IsActive)
            .OrderBy(Vehicle => Vehicle.PlateNumber)
            .ToListAsync(cancellationToken);

    public Task<DriverVehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        _context.DriverVehicles
            .AsNoTracking()
            .Include(Vehicle => Vehicle.TransportMode)
            .FirstOrDefaultAsync(Vehicle => Vehicle.VehicleId == vehicleId, cancellationToken);

    public async Task<DriverVehicle> AddAsync(DriverVehicle Vehicle, CancellationToken cancellationToken = default)
    {
        await _context.DriverVehicles.AddAsync(Vehicle, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Vehicle;
    }

    public async Task<DriverVehicle> UpdateAsync(DriverVehicle Vehicle, CancellationToken cancellationToken = default)
    {
        _context.DriverVehicles.Update(Vehicle);
        await _context.SaveChangesAsync(cancellationToken);
        return Vehicle;
    }

    public async Task<bool> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var Vehicle = await _context.DriverVehicles.FirstOrDefaultAsync(Vehicle => Vehicle.VehicleId == vehicleId, cancellationToken);
        if (Vehicle is null)
        {
            return false;
        }

        Vehicle.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
