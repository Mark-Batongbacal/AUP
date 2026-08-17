using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for Driver profiles and Driver Status fields.
/// </summary>
public sealed class DriverRepository(TukiDbContext context) : IDriverRepository
{
    private readonly TukiDbContext _context = context;

    public Task<Driver?> GetByIdAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.Drivers
            .AsNoTracking()
            .Include(Driver => Driver.User)
            .FirstOrDefaultAsync(Driver => Driver.DriverId == driverId, cancellationToken);

    public Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(Driver => Driver.UserId == userId, cancellationToken);

    public Task<List<Driver>> GetAvailableDriversAsync(CancellationToken cancellationToken = default) =>
        _context.Drivers
            .AsNoTracking()
            .Include(Driver => Driver.User)
            .Where(Driver => Driver.IsAvailable)
            .OrderBy(Driver => Driver.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Driver?> GetWithHomeTerminalAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.Drivers
            .AsNoTracking()
            .Include(Driver => Driver.HomeTerminal)
            .FirstOrDefaultAsync(Driver => Driver.DriverId == driverId, cancellationToken);

    public Task<Driver?> GetWithVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.Drivers
            .AsNoTracking()
            .Include(Driver => Driver.DriverVehicles)
                .ThenInclude(Vehicle => Vehicle.TransportMode)
            .FirstOrDefaultAsync(Driver => Driver.DriverId == driverId, cancellationToken);

    public async Task<Driver> AddAsync(Driver Driver, CancellationToken cancellationToken = default)
    {
        await _context.Drivers.AddAsync(Driver, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Driver;
    }

    public async Task<Driver> UpdateAsync(Driver Driver, CancellationToken cancellationToken = default)
    {
        _context.Drivers.Update(Driver);
        await _context.SaveChangesAsync(cancellationToken);
        return Driver;
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default)
    {
        var Driver = await _context.Drivers.FirstOrDefaultAsync(Driver => Driver.DriverId == driverId, cancellationToken);
        if (Driver is null)
        {
            return false;
        }

        Driver.IsAvailable = isAvailable;
        Driver.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateVerificationStatusAsync(Guid driverId, string verificationStatus, CancellationToken cancellationToken = default)
    {
        var Driver = await _context.Drivers.FirstOrDefaultAsync(Driver => Driver.DriverId == driverId, cancellationToken);
        if (Driver is null)
        {
            return false;
        }

        Driver.VerificationStatus = verificationStatus;
        Driver.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
