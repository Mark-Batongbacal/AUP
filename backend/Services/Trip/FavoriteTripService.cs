using backend.Models.Database;
using backend.Repositories;

namespace backend.Services;

public sealed class FavoriteTripService(
    IFavoriteTripRepository favoriteTripRepository,
    IRouteRecommendationRepository routeRecommendationRepository,
    IPassengerTripRepository passengerTripRepository) : IFavoriteTripService
{
    public async Task<List<FavoriteTripDto>> GetFavoritesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var favorites = await favoriteTripRepository.GetByUserAsync(userId, cancellationToken);
        var dtos = new List<FavoriteTripDto>(favorites.Count);
        foreach (var favorite in favorites)
        {
            dtos.Add(await MapAsync(favorite, cancellationToken));
        }

        return dtos;
    }

    public async Task<FavoriteTripDto?> GetFavoriteByIdAsync(Guid userId, Guid favoriteTripId, CancellationToken cancellationToken = default)
    {
        var favorite = await favoriteTripRepository.GetByIdAsync(favoriteTripId, cancellationToken);
        if (favorite is null || favorite.UserId != userId)
        {
            return null;
        }

        return await MapAsync(favorite, cancellationToken);
    }

    public async Task<FavoriteTripAddResult> AddFavoriteAsync(
        Guid userId,
        Guid recommendationId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new FavoriteTripAddResult(FavoriteTripAddStatus.PersistenceNotAllowed, null);
        }

        var recommendation = await routeRecommendationRepository.GetByIdAsync(recommendationId, cancellationToken);
        if (recommendation is null)
        {
            return new FavoriteTripAddResult(FavoriteTripAddStatus.RecommendationNotFound, null);
        }

        var existing = await favoriteTripRepository.GetByUserAndRecommendationAsync(userId, recommendationId, cancellationToken);
        if (existing is not null)
        {
            return new FavoriteTripAddResult(FavoriteTripAddStatus.AlreadyFavorited, await MapAsync(existing, cancellationToken));
        }

        var favorite = new FavoriteTrip
        {
            UserId = userId,
            RecommendationId = recommendationId,
            Note = note,
        };

        var added = await favoriteTripRepository.AddAsync(favorite, cancellationToken);
        var details = await favoriteTripRepository.GetByIdAsync(added.FavoriteTripId, cancellationToken);
        return new FavoriteTripAddResult(FavoriteTripAddStatus.Created, await MapAsync(details ?? added, cancellationToken));
    }

    public async Task<bool> RemoveFavoriteAsync(Guid userId, Guid favoriteTripId, CancellationToken cancellationToken = default)
    {
        var favorite = await favoriteTripRepository.GetByIdAsync(favoriteTripId, cancellationToken);
        if (favorite is null || favorite.UserId != userId)
        {
            return false;
        }

        return await favoriteTripRepository.RemoveAsync(favoriteTripId, cancellationToken);
    }

    private async Task<FavoriteTripDto> MapAsync(FavoriteTrip favorite, CancellationToken cancellationToken)
    {
        var timesUsed = await passengerTripRepository.CountByUserAndRecommendationAsync(
            favorite.UserId,
            favorite.RecommendationId,
            cancellationToken);

        return new FavoriteTripDto(
            favorite.FavoriteTripId,
            favorite.UserId,
            favorite.RecommendationId,
            favorite.Recommendation?.TripSearch?.OriginName,
            favorite.Recommendation?.TripSearch?.DestinationName,
            timesUsed,
            favorite.Note,
            favorite.CreatedAt);
    }
}
