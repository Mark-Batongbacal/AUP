using backend.Models.Database;
using backend.Models.Destinations;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Navigation;
using backend.Services.Routing;
using backend.Models.Valhalla;
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

    [Fact]
    public async Task Prefetch_SelectsBoardAndOnlyPreAlightSemanticReferences()
    {
        var recommendations = new Mock<IRouteRecommendationRepository>();
        recommendations.Setup(item => item.GetOrderedLegsAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([new RecommendationLeg
            {
                LegOrder = 0, RouteId = 10,
                TransportMode = new TransportMode { Code = "JEEPNEY" },
                StartLatitude = 15, StartLongitude = 120,
                EndLatitude = 15, EndLongitude = 120.01
            }]);
        var points = new Mock<IRoutePointRepository>();
        points.Setup(item => item.GetOrderedByRouteAsync(10, default)).ReturnsAsync(
        [
            new RoutePoint { Latitude = 15, Longitude = 120 },
            new RoutePoint { Latitude = 15, Longitude = 120.02 }
        ]);
        var places = new Mock<IPlaceLandmarkDiscoveryService>();
        places.Setup(item => item.FindNearbyVenuesAsync(15, 120, default)).ReturnsAsync(
        [
            new("board", "McDonald's", 15, 120.0001, "fast_food", "pelias")
        ]);
        places.Setup(item => item.FindNearbyVenuesAsync(15, 120.01, default)).ReturnsAsync(
        [
            new("before", "Jollibee", 15, 120.009, "fast_food", "pelias"),
            new("after", "After Mall", 15, 120.011, "mall", "pelias")
        ]);
        IReadOnlyList<TripLandmarkCandidate>? saved = null;
        var cache = new Mock<ITripLandmarkCandidateRepository>();
        cache.Setup(item => item.ReplaceAsync(It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<TripLandmarkCandidate>>(), default))
            .Callback((Guid _, IReadOnlyList<TripLandmarkCandidate> items, CancellationToken _) => saved = items)
            .Returns(Task.CompletedTask);
        var options = new NavigationOptions
        {
            MinimumLandmarkSeparationMeters = 50,
            MaximumLandmarkProjectionMeters = 100,
            BoardReferenceMaximumDistanceMeters = 300,
            LandmarkLookbackFromAlightMeters = 1500,
            MinimumAlightReferenceLeadMeters = 15
        };
        var service = new LandmarkCorridorPrefetchService(
            recommendations.Object, points.Object, places.Object,
            new MapMatchingService(Options.Create(options)), cache.Object,
            Options.Create(options), NullLogger<LandmarkCorridorPrefetchService>.Instance);

        await service.PrefetchAsync(new TripSession
        {
            TripSessionId = Guid.NewGuid(), RecommendationId = Guid.NewGuid()
        });

        Assert.Contains(saved!, item => item.Name == "McDonald's" &&
            item.Role == LandmarkRole.BoardReference &&
            item.Relation == LandmarkRelation.NearBoardPoint);
        var alight = Assert.Single(saved!, item => item.Role == LandmarkRole.AlightReference);
        Assert.Equal("Jollibee", alight.Name);
        Assert.Equal(LandmarkRelation.BeforeAlight, alight.Relation);
        Assert.DoesNotContain(saved!, item => item.Name == "After Mall");
    }

    [Fact]
    public async Task Prefetch_UsesRoadProgressForTricycleAlightReference()
    {
        var recommendations = new Mock<IRouteRecommendationRepository>();
        recommendations.Setup(item => item.GetOrderedLegsAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync([new RecommendationLeg
            {
                LegOrder = 0, TransportMode = new TransportMode { Code = "TRICYCLE" },
                StartLatitude = 15, StartLongitude = 120,
                EndLatitude = 15, EndLongitude = 120.01
            }]);
        var places = new Mock<IPlaceLandmarkDiscoveryService>();
        places.Setup(item => item.FindNearbyVenuesAsync(15, 120, default)).ReturnsAsync([]);
        places.Setup(item => item.FindNearbyVenuesAsync(15, 120.01, default)).ReturnsAsync(
        [
            new("before", "Public Market", 15, 120.009, "commercial", "pelias"),
            new("after", "After Hospital", 15, 120.011, "hospital", "pelias")
        ]);
        var valhalla = new Mock<IValhallaService>();
        valhalla.Setup(item => item.GetRouteAsync(15, 120, 15, 120.01, "auto", default))
            .ReturnsAsync(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Legs = [new ValhallaLeg { Points = [[120, 15], [120.02, 15]] }]
                }
            });
        IReadOnlyList<TripLandmarkCandidate>? saved = null;
        var cache = new Mock<ITripLandmarkCandidateRepository>();
        cache.Setup(item => item.ReplaceAsync(It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<TripLandmarkCandidate>>(), default))
            .Callback((Guid _, IReadOnlyList<TripLandmarkCandidate> items, CancellationToken _) => saved = items)
            .Returns(Task.CompletedTask);
        var options = new NavigationOptions { MinimumLandmarkSeparationMeters = 50 };
        var service = new LandmarkCorridorPrefetchService(
            recommendations.Object, Mock.Of<IRoutePointRepository>(), places.Object,
            new MapMatchingService(Options.Create(options)), cache.Object,
            Options.Create(options), NullLogger<LandmarkCorridorPrefetchService>.Instance,
            valhalla.Object);

        await service.PrefetchAsync(new TripSession
        {
            TripSessionId = Guid.NewGuid(), RecommendationId = Guid.NewGuid()
        });

        var alight = Assert.Single(saved!, item => item.Role == LandmarkRole.AlightReference);
        Assert.Equal("Public Market", alight.Name);
        Assert.DoesNotContain(saved!, item => item.Name == "After Hospital");
    }
}
