using backend.Models.Database;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Repositories;

/// <summary>
/// Data access for the current location row per driver. This repository does not perform route
/// matching.
/// </summary>
public sealed class DriverLocationRepository(SupabaseDbContext context) : IDriverLocationRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<driver_location?> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.driver_locations
            .AsNoTracking()
            .FirstOrDefaultAsync(location => location.driver_id == driverId, cancellationToken);

    public async Task<driver_location> AddOrUpdateAsync(driver_location location, CancellationToken cancellationToken = default)
    {
        var existing = await _context.driver_locations.FirstOrDefaultAsync(
            currentLocation => currentLocation.driver_id == location.driver_id,
            cancellationToken);

        if (existing is null)
        {
            await _context.driver_locations.AddAsync(location, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return location;
        }

        existing.latitude = location.latitude;
        existing.longitude = location.longitude;
        existing.location = location.location;
        existing.heading_degrees = location.heading_degrees;
        existing.speed_kph = location.speed_kph;
        existing.accuracy_meters = location.accuracy_meters;
        existing.updated_at = location.updated_at;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> UpdateLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        Point? location = null,
        decimal? headingDegrees = null,
        decimal? speedKph = null,
        decimal? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default)
    {
        var driverLocation = await _context.driver_locations.FirstOrDefaultAsync(
            currentLocation => currentLocation.driver_id == driverId,
            cancellationToken);

        if (driverLocation is null)
        {
            return false;
        }

        driverLocation.latitude = latitude;
        driverLocation.longitude = longitude;
        driverLocation.location = location;
        driverLocation.heading_degrees = headingDegrees;
        driverLocation.speed_kph = speedKph;
        driverLocation.accuracy_meters = accuracyMeters;
        driverLocation.updated_at = updatedAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
