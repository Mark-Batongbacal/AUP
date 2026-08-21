namespace backend.Services;

public interface IFavoriteTripService
{
    Task<List<FavoriteTripDto>> GetFavoritesByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<FavoriteTripDto?> GetFavoriteByIdAsync(Guid userId, Guid favoriteTripId, CancellationToken cancellationToken = default);

    Task<FavoriteTripAddResult> AddFavoriteAsync(
        Guid userId,
        Guid recommendationId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveFavoriteAsync(Guid userId, Guid favoriteTripId, CancellationToken cancellationToken = default);
}

public enum FavoriteTripAddStatus
{
    Created,
    PersistenceNotAllowed,
    RecommendationNotFound,
    AlreadyFavorited
}

public sealed record FavoriteTripAddResult(FavoriteTripAddStatus Status, FavoriteTripDto? Favorite);

public sealed record FavoriteTripDto(
    Guid FavoriteTripId,
    Guid UserId,
    Guid RecommendationId,
    string? Origin,
    string? Destination,
    string RecommendationType,
    decimal TotalMinutes,
    decimal TotalFare,
    decimal WalkingDistanceMeters,
    int TransferCount,
    int TimesUsed,
    string? Note,
    DateTime CreatedAt);
