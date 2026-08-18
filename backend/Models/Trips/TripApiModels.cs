namespace backend.Models.Trips;

public sealed record StartTripRequest(
    Guid? RecommendationId,
    DateTime? StartedAt = null);

public sealed record TripErrorResponseDto(IReadOnlyList<string> Errors);
