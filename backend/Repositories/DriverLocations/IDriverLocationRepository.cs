using backend.Models.Database;

namespace backend.Repositories;

public interface IDriverLocationRepository
{
    Task<DriverLocation?> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverLocation> AddOrUpdateAsync(DriverLocation Location, CancellationToken cancellationToken = default);

    Task<bool> UpdateLocationAsync(
        Guid driverId,
        double Latitude,
        double Longitude,
        double? headingDegrees = null,
        double? speedKph = null,
        double? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default);
}
