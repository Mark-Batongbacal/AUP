using backend.Models.Database;

namespace backend.Repositories;

public interface IFavoriteTripRepository
{
    Task<List<FavoriteTrip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<FavoriteTrip?> GetByIdAsync(Guid favoriteTripId, CancellationToken cancellationToken = default);

    Task<FavoriteTrip?> GetByUserAndRecommendationAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default);

    Task<FavoriteTrip> AddAsync(FavoriteTrip favoriteTrip, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid favoriteTripId, CancellationToken cancellationToken = default);
}
