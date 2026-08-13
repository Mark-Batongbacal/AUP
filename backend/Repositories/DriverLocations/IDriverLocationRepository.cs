using backend.Models.Database;
using NetTopologySuite.Geometries;

namespace backend.Repositories;

public interface IDriverLocationRepository
{
    Task<driver_location?> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<driver_location> AddOrUpdateAsync(driver_location location, CancellationToken cancellationToken = default);

    Task<bool> UpdateLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        Point? location = null,
        decimal? headingDegrees = null,
        decimal? speedKph = null,
        decimal? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default);
}
