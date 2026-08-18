using backend.Models.Database;
using backend.Models.Destinations;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Navigation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class LandmarkCorridorPrefetchServiceTests
{
    [Fact]
    public async Task Prefetch_ProjectsFiltersRanksAndCachesSessionCandidates()
    {
        var recommendations = new Mock<IRouteRecommendationRepository>();
        recommendations.Setup(item => item.GetOrderedLegsAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([new RecommendationLeg
            {
                LegOrder = 0, RouteId = 10,
                StartLatitude = 15, StartLongitude = 120,
                EndLatitude = 15, EndLongitude = 120.01
            }]);
        var points = new Mock<IRoutePointRepository>();
        points.Setup(item => item.GetOrderedByRouteAsync(10, default)).ReturnsAsync(
        [
            new RoutePoint { Latitude = 15, Longitude = 120 },
            new RoutePoint { Latitude = 15, Longitude = 120.01 }
        ]);
        var places = new Mock<IPlaceLandmarkDiscoveryService>();
        places.Setup(item => item.FindNearbyVenuesAsync(
                15, 120.01, default))
            .ReturnsAsync(
            [
                new("openstreetmap:venue:hospital", "Hospital", 15, 120.005, "hospital", "pelias"),
                new("openstreetmap:venue:nearby", "Nearby shop", 15, 120.0051, "shop", "pelias"),
                new("openstreetmap:venue:off-route", "Unrelated", 15.01, 120.005, "mall", "pelias")
            ]);
        var cache = new Mock<ITripLandmarkCandidateRepository>();
        IReadOnlyList<TripLandmarkCandidate>? saved = null;
        cache.Setup(item => item.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TripLandmarkCandidate>>(), default))
            .Callback((Guid _, IReadOnlyList<TripLandmarkCandidate> items, CancellationToken _) => saved = items)
            .Returns(Task.CompletedTask);
        var navigation = new NavigationOptions
        {
            MaximumLandmarkProjectionMeters = 100,
            MinimumLandmarkSeparationMeters = 250,
            MaximumLandmarksPerLeg = 5
        };
        var service = new LandmarkCorridorPrefetchService(
            recommendations.Object, points.Object, places.Object,
            new MapMatchingService(Options.Create(navigation)), cache.Object,
            Options.Create(navigation), NullLogger<LandmarkCorridorPrefetchService>.Instance);
        var session = new TripSession { TripSessionId = Guid.NewGuid(), RecommendationId = Guid.NewGuid() };

        await service.PrefetchAsync(session);

        var candidate = Assert.Single(saved!);
        Assert.Equal("Hospital", candidate.Name);
        Assert.Equal(session.TripSessionId, candidate.TripSessionId);
        Assert.InRange(candidate.DistanceFromRouteStartMeters, 500, 620);
    }
}
