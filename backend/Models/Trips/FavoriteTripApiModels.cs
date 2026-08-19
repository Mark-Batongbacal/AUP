namespace backend.Models.Trips;

public sealed record AddFavoriteTripRequest(Guid? RecommendationId, string? Note = null);
