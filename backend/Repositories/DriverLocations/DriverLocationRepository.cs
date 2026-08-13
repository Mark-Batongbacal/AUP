using backend.Models.Database;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Repositories;

/// <summary>
/// Data access for the current Location row per Driver. This repository does not perform Route
/// matching.
/// </summary>
public sealed class DriverLocationRepository(SupabaseDbContext context) : IDriverLocationRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<DriverLocation?> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.DriverLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(Location => Location.DriverId == driverId, cancellationToken);

    public async Task<DriverLocation> AddOrUpdateAsync(DriverLocation Location, CancellationToken cancellationToken = default)
    {
        var existing = await _context.DriverLocations.FirstOrDefaultAsync(
            currentLocation => currentLocation.DriverId == Location.DriverId,
            cancellationToken);

        if (existing is null)
        {
            await _context.DriverLocations.AddAsync(Location, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Location;
        }

        existing.Latitude = Location.Latitude;
        existing.Longitude = Location.Longitude;
        existing.Location = Location.Location;
        existing.HeadingDegrees = Location.HeadingDegrees;
        existing.SpeedKph = Location.SpeedKph;
        existing.AccuracyMeters = Location.AccuracyMeters;
        existing.UpdatedAt = Location.UpdatedAt;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> UpdateLocationAsync(
        Guid driverId,
        double Latitude,
        double Longitude,
        Point? Location = null,
        decimal? headingDegrees = null,
        decimal? speedKph = null,
        decimal? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default)
    {
        var driverLocation = await _context.DriverLocations.FirstOrDefaultAsync(
            currentLocation => currentLocation.DriverId == driverId,
            cancellationToken);

        if (driverLocation is null)
        {
            return false;
        }

        driverLocation.Latitude = Latitude;
        driverLocation.Longitude = Longitude;
        driverLocation.Location = Location;
        driverLocation.HeadingDegrees = headingDegrees;
        driverLocation.SpeedKph = speedKph;
        driverLocation.AccuracyMeters = accuracyMeters;
        driverLocation.UpdatedAt = updatedAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
