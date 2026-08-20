using backend.Services;

namespace backend.Models.Trips;

public sealed record StartTripRequest(
    Guid? RecommendationId,
    DateTime? StartedAt = null);

public sealed record TripErrorResponseDto(IReadOnlyList<string> Errors);

public sealed record PassengerTripHistoryItemDto(
    Guid PassengerTripId,
    string Status,
    string OriginName,
    string DestinationName,
    double OriginLatitude,
    double OriginLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    RecommendationDetailsDto? Recommendation,
    bool Rerouted,
    int RerouteCount,
    string? LastRerouteReason,
    DateTime? LastRerouteAt);
