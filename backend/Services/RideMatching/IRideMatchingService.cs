using backend.Models.Database;

namespace backend.Services;

public interface IRideMatchingService
{
    Task<PassengerRideRequest?> GetRideRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<PassengerRideRequest>> GetRideRequestsByPassengerAsync(
        Guid passengerUserId,
        CancellationToken cancellationToken = default);

    Task<List<PassengerRideRequest>> GetActiveRideRequestsAsync(CancellationToken cancellationToken = default);

    Task<PassengerRideRequest?> CreateRideRequestAsync(
        Guid passengerUserId,
        string pickupName,
        double pickupLatitude,
        double pickupLongitude,
        string dropoffName,
        double dropoffLatitude,
        double dropoffLongitude,
        int passengerCount = 1,
        int? transportModeId = null,
        decimal? maxBudget = null,
        DateTime? requestedAt = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default);

    Task<List<DriverCandidateDto>> GetAvailableDriversAsync(CancellationToken cancellationToken = default);

    Task<List<DriverCandidateDto>> GetCandidateDriversAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<RideMatch?> CreateRideMatchAsync(
        Guid requestId,
        Guid driverId,
        Guid? vehicleId = null,
        decimal? pickupDistanceMeters = null,
        decimal? detourDistanceMeters = null,
        decimal? estimatedPickupMinutes = null,
        decimal? estimatedTripMinutes = null,
        decimal? estimatedFare = null,
        decimal? matchScore = null,
        DateTime? offeredAt = null,
        CancellationToken cancellationToken = default);

    Task<RideMatch?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<RideMatchDetailsDto?> GetMatchDetailsAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<List<RideMatch>> GetMatchesForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<RideMatch>> GetMatchesForDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> AcceptMatchAsync(Guid matchId, DateTime? acceptedAt = null, CancellationToken cancellationToken = default);

    Task<bool> RejectMatchAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<bool> CancelMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public sealed record RideRequestDetailsDto(
    Guid RequestId,
    Guid PassengerUserId,
    int? TransportModeId,
    TransportModeSummaryDto? TransportMode,
    string? PickupName,
    double PickupLatitude,
    double PickupLongitude,
    string? DropoffName,
    double DropoffLatitude,
    double DropoffLongitude,
    int PassengerCount,
    decimal? MaxBudget,
    string Status,
    DateTime RequestedAt,
    DateTime? ExpiresAt,
    DateTime UpdatedAt);

public sealed record DriverCandidateDto(
    Guid DriverId,
    Guid UserId,
    string VerificationStatus,
    decimal? AverageRating,
    int RatingCount,
    bool IsAvailable,
    DriverLocationDto CurrentLocation,
    DriverAvailabilitySessionDto ActiveAvailabilitySession,
    DriverVehicleDto Vehicle);

public sealed record RideMatchDetailsDto(
    Guid MatchId,
    Guid RequestId,
    Guid DriverId,
    long? SessionId,
    Guid? VehicleId,
    decimal? PickupDistanceMeters,
    decimal? DetourDistanceMeters,
    decimal? EstimatedPickupMinutes,
    decimal? EstimatedTripMinutes,
    decimal? EstimatedFare,
    decimal? MatchScore,
    string Status,
    DateTime OfferedAt,
    DateTime? AcceptedAt,
    DateTime? CompletedAt,
    RideRequestDetailsDto? Request,
    DriverSummaryDto? Driver,
    DriverAvailabilitySessionDto? AvailabilitySession,
    DriverVehicleDto? Vehicle);

public sealed record DriverSummaryDto(
    Guid DriverId,
    Guid UserId,
    string? LicenseNumber,
    string VerificationStatus,
    long? HomeTerminalId,
    decimal? AverageRating,
    int RatingCount,
    bool IsAvailable,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
