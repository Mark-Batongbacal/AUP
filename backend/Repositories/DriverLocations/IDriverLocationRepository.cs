using backend.Models.Database;
using NetTopologySuite.Geometries;

namespace backend.Repositories;

public interface IDriverLocationRepository
{
    Task<DriverLocation?> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverLocation> AddOrUpdateAsync(DriverLocation Location, CancellationToken cancellationToken = default);

    Task<bool> UpdateLocationAsync(
        Guid driverId,
        double Latitude,
        double Longitude,
        Point? Location = null,
        decimal? headingDegrees = null,
        decimal? speedKph = null,
        decimal? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default);
}
