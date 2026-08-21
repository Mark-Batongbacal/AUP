using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Trip;

public sealed class FavoriteTripServiceTests
{
    [Fact]
    public async Task AddFavoriteAsync_WhenUserIsGuest_DoesNotPersistFavorite()
    {
        var favorites = new Mock<IFavoriteTripRepository>(MockBehavior.Strict);
        var recommendations = new Mock<IRouteRecommendationRepository>(MockBehavior.Strict);
        var passengerTrips = new Mock<IPassengerTripRepository>(MockBehavior.Strict);
        var service = new FavoriteTripService(
            favorites.Object,
            recommendations.Object,
            passengerTrips.Object);

        var result = await service.AddFavoriteAsync(Guid.Empty, Guid.NewGuid(), null);

        Assert.Equal(FavoriteTripAddStatus.PersistenceNotAllowed, result.Status);
        Assert.Null(result.Favorite);
        favorites.Verify(
            repository => repository.AddAsync(
                It.IsAny<FavoriteTrip>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
