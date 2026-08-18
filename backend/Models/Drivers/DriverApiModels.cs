using backend.Services;

namespace backend.Models.Drivers;

public sealed record StartDriverAvailabilityRequest(
    Guid? VehicleId = null,
    long? DestinationStopId = null,
    string? DestinationName = null,
    double? DestinationLatitude = null,
    double? DestinationLongitude = null,
    int AvailableSeats = 1,
    decimal MaximumDetourMeters = 1000,
    DateTime? StartedAt = null);

public sealed record StopDriverAvailabilityRequest(DateTime? EndedAt = null);

public sealed record UpdateDriverLocationRequest(
    double? Latitude,
    double? Longitude,
    double? HeadingDegrees = null,
    double? SpeedKph = null,
    double? AccuracyMeters = null,
    DateTime? UpdatedAt = null);

public sealed record DriverAvailabilityResponseDto(
    Guid DriverId,
    bool IsAvailable,
    DriverAvailabilitySessionDto? CurrentAvailabilitySession);

public sealed record DriverErrorResponseDto(IReadOnlyList<string> Errors);
