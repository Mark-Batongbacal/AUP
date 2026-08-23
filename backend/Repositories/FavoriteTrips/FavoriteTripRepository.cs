using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for a user's favorited trips. Missing favorites return null.
/// </summary>
public sealed class FavoriteTripRepository(TukiDbContext context) : IFavoriteTripRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<FavoriteTrip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.FavoriteTrips
            .AsNoTracking()
            .Include(favorite => favorite.Recommendation)
                .ThenInclude(recommendation => recommendation.TripSearch)
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<FavoriteTrip?> GetByIdAsync(Guid favoriteTripId, CancellationToken cancellationToken = default) =>
        _context.FavoriteTrips
            .AsNoTracking()
            .Include(favorite => favorite.Recommendation)
                .ThenInclude(recommendation => recommendation.TripSearch)
            .FirstOrDefaultAsync(favorite => favorite.FavoriteTripId == favoriteTripId, cancellationToken);

    public Task<FavoriteTrip?> GetByUserAndRecommendationAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.FavoriteTrips
            .AsNoTracking()
            .Include(favorite => favorite.Recommendation)
                .ThenInclude(recommendation => recommendation.TripSearch)
            .FirstOrDefaultAsync(favorite => favorite.UserId == userId && favorite.RecommendationId == recommendationId, cancellationToken);

    public async Task<FavoriteTrip> AddAsync(FavoriteTrip favoriteTrip, CancellationToken cancellationToken = default)
    {
        await _context.FavoriteTrips.AddAsync(favoriteTrip, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return favoriteTrip;
    }

    public async Task<bool> RemoveAsync(Guid favoriteTripId, CancellationToken cancellationToken = default)
    {
        var favorite = await _context.FavoriteTrips.FirstOrDefaultAsync(favorite => favorite.FavoriteTripId == favoriteTripId, cancellationToken);
        if (favorite is null)
        {
            return false;
        }

        _context.FavoriteTrips.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
