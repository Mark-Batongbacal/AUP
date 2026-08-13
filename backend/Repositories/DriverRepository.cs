using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for driver profiles and driver status fields.
/// </summary>
public sealed class DriverRepository(SupabaseDbContext context) : IDriverRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<driver?> GetByIdAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.drivers
            .AsNoTracking()
            .Include(driver => driver.user)
            .FirstOrDefaultAsync(driver => driver.driver_id == driverId, cancellationToken);

    public Task<driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(driver => driver.user_id == userId, cancellationToken);

    public Task<List<driver>> GetAvailableDriversAsync(CancellationToken cancellationToken = default) =>
        _context.drivers
            .AsNoTracking()
            .Include(driver => driver.user)
            .Where(driver => driver.is_available)
            .OrderBy(driver => driver.created_at)
            .ToListAsync(cancellationToken);

    public Task<driver?> GetWithHomeTerminalAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.drivers
            .AsNoTracking()
            .Include(driver => driver.home_terminal)
            .FirstOrDefaultAsync(driver => driver.driver_id == driverId, cancellationToken);

    public Task<driver?> GetWithVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.drivers
            .AsNoTracking()
            .Include(driver => driver.driver_vehicles)
                .ThenInclude(vehicle => vehicle.transport_mode)
            .FirstOrDefaultAsync(driver => driver.driver_id == driverId, cancellationToken);

    public async Task<driver> AddAsync(driver driver, CancellationToken cancellationToken = default)
    {
        await _context.drivers.AddAsync(driver, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return driver;
    }

    public async Task<driver> UpdateAsync(driver driver, CancellationToken cancellationToken = default)
    {
        _context.drivers.Update(driver);
        await _context.SaveChangesAsync(cancellationToken);
        return driver;
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default)
    {
        var driver = await _context.drivers.FirstOrDefaultAsync(driver => driver.driver_id == driverId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        driver.is_available = isAvailable;
        driver.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateVerificationStatusAsync(Guid driverId, string verificationStatus, CancellationToken cancellationToken = default)
    {
        var driver = await _context.drivers.FirstOrDefaultAsync(driver => driver.driver_id == driverId, cancellationToken);
        if (driver is null)
        {
            return false;
        }

        driver.verification_status = verificationStatus;
        driver.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
