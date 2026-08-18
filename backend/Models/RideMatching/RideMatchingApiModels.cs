using backend.Services;

namespace backend.Models.RideMatching;

public sealed record CreateRideRequestRequest(
    string? PickupName,
    double? PickupLatitude,
    double? PickupLongitude,
    string? DropoffName,
    double? DropoffLatitude,
    double? DropoffLongitude,
    int PassengerCount = 1,
    int? TransportModeId = null,
    decimal? MaxBudget = null,
    DateTime? RequestedAt = null,
    DateTime? ExpiresAt = null);

public sealed record CreateRideMatchRequest(
    Guid? DriverId,
    Guid? VehicleId = null,
    decimal? PickupDistanceMeters = null,
    decimal? DetourDistanceMeters = null,
    decimal? EstimatedPickupMinutes = null,
    decimal? EstimatedTripMinutes = null,
    decimal? EstimatedFare = null,
    decimal? MatchScore = null,
    DateTime? OfferedAt = null);

public sealed record AcceptRideMatchRequest(DateTime? AcceptedAt = null);

public sealed record RideMatchingErrorResponseDto(IReadOnlyList<string> Errors);
