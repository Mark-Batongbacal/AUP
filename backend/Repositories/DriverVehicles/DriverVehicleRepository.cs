using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for vehicles registered to drivers.
/// </summary>
public sealed class DriverVehicleRepository(SupabaseDbContext context) : IDriverVehicleRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<driver_vehicle>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.driver_vehicles
            .AsNoTracking()
            .Include(vehicle => vehicle.transport_mode)
            .Where(vehicle => vehicle.driver_id == driverId)
            .OrderBy(vehicle => vehicle.plate_number)
            .ToListAsync(cancellationToken);

    public Task<List<driver_vehicle>> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.driver_vehicles
            .AsNoTracking()
            .Include(vehicle => vehicle.transport_mode)
            .Where(vehicle => vehicle.driver_id == driverId && vehicle.is_active)
            .OrderBy(vehicle => vehicle.plate_number)
            .ToListAsync(cancellationToken);

    public Task<driver_vehicle?> GetByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        _context.driver_vehicles
            .AsNoTracking()
            .Include(vehicle => vehicle.transport_mode)
            .FirstOrDefaultAsync(vehicle => vehicle.vehicle_id == vehicleId, cancellationToken);

    public async Task<driver_vehicle> AddAsync(driver_vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await _context.driver_vehicles.AddAsync(vehicle, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return vehicle;
    }

    public async Task<driver_vehicle> UpdateAsync(driver_vehicle vehicle, CancellationToken cancellationToken = default)
    {
        _context.driver_vehicles.Update(vehicle);
        await _context.SaveChangesAsync(cancellationToken);
        return vehicle;
    }

    public async Task<bool> DeactivateAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await _context.driver_vehicles.FirstOrDefaultAsync(vehicle => vehicle.vehicle_id == vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return false;
        }

        vehicle.is_active = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
