using backend.Models.Database;
using backend.Services.Transportation;

namespace backend.Services;

public interface IDriverService
{
    Task<Driver?> GetDriverByIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Driver?> GetDriverByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DriverDetailsDto?> GetDriverDetailsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<DriverVehicle>> GetDriverVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<List<DriverVehicle>> GetActiveDriverVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> SetDriverAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default);

    Task<DriverLocation?> GetDriverLocationAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverLocation?> UpdateDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        double? headingDegrees = null,
        double? speedKph = null,
        double? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default);

    Task<DriverAvailabilitySession?> GetActiveAvailabilitySessionAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<DriverAvailabilitySession?> StartAvailabilitySessionAsync(
        Guid driverId,
        Guid? vehicleId = null,
        long? destinationStopId = null,
        string? destinationName = null,
        double? destinationLatitude = null,
        double? destinationLongitude = null,
        int availableSeats = 1,
        decimal maximumDetourMeters = 1000,
        DateTime? startedAt = null,
        CancellationToken cancellationToken = default);

    Task<bool> EndAvailabilitySessionAsync(Guid driverId, DateTime? endedAt = null, CancellationToken cancellationToken = default);
}

public sealed record DriverDetailsDto(
    Guid DriverId,
    Guid UserId,
    DriverUserProfileDto? User,
    string? LicenseNumber,
    string VerificationStatus,
    long? HomeTerminalId,
    TransportStopSummaryDto? HomeTerminal,
    decimal? AverageRating,
    int RatingCount,
    bool IsAvailable,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DriverVehicleDto> ActiveVehicles,
    DriverLocationDto? CurrentLocation,
    DriverAvailabilitySessionDto? CurrentAvailabilitySession);

public sealed record DriverUserProfileDto(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string Role,
    string? ProfileImageUrl,
    bool IsActive);

public sealed record DriverVehicleDto(
    Guid VehicleId,
    Guid DriverId,
    int TransportModeId,
    TransportModeSummaryDto? TransportMode,
    string? PlateNumber,
    string? BodyNumber,
    string? Color,
    int Capacity,
    bool IsActive,
    DateTime CreatedAt);

public sealed record DriverLocationDto(
    Guid DriverId,
    double Latitude,
    double Longitude,
    double? HeadingDegrees,
    double? SpeedKph,
    double? AccuracyMeters,
    DateTime UpdatedAt);

public sealed record DriverAvailabilitySessionDto(
    long SessionId,
    Guid DriverId,
    Guid? VehicleId,
    DriverVehicleDto? Vehicle,
    long? DestinationStopId,
    TransportStopSummaryDto? DestinationStop,
    string? DestinationName,
    double? DestinationLatitude,
    double? DestinationLongitude,
    int AvailableSeats,
    decimal MaximumDetourMeters,
    string Status,
    DateTime StartedAt,
    DateTime? EndedAt);
