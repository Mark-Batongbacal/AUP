using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class LandmarkServiceTests
{
    private readonly NavigationOptions _options = new();
    private readonly Mock<ITripLandmarkCandidateRepository> _cache = new();

    [Fact]
    public async Task Landmark_TriggersOnceWhenProgressCrossesOccurrence()
    {
        _cache.Setup(repository => repository.GetCrossedAsync(It.IsAny<Guid>(), 0, 500, 600, default))
            .ReturnsAsync([Candidate(1, 550, "Jollibee")]);
        var first = await Service().EvaluateAsync(Session(), Leg(), 500, 600);
        Assert.Single(first);
        Assert.Equal(NavigationInstructionType.LandmarkNotice, first[0].Type);
        _cache.Setup(repository => repository.GetCrossedAsync(It.IsAny<Guid>(), 0, 500, 600, default))
            .ReturnsAsync([]);
        var second = await Service().EvaluateAsync(Session(), Leg(), 500, 600);
        Assert.Empty(second);
    }

    [Fact]
    public async Task LandmarkBehindUser_DoesNotTrigger()
    {
        _cache.Setup(repository => repository.GetCrossedAsync(It.IsAny<Guid>(), 0, 600, 700, default)).ReturnsAsync([]);
        Assert.Empty(await Service().EvaluateAsync(Session(), Leg(), 600, 700));
    }

    [Fact]
    public async Task CloselySpacedLandmarks_AreLimitedToPreventSpam()
    {
        _cache.Setup(repository => repository.GetCrossedAsync(It.IsAny<Guid>(), 0, 400, 800, default))
            .ReturnsAsync([Candidate(1, 500, "First")]);
        var result = await Service().EvaluateAsync(Session(), Leg(), 400, 800);
        Assert.Single(result);
        _cache.Verify(repository => repository.MarkTriggeredAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Once);
    }

    private LandmarkService Service() => new(Options.Create(_options), _cache.Object);
    private static TripSession Session() => new() { TripSessionId = Guid.NewGuid() };
    private static RecommendationLeg Leg() => new() { LegOrder = 0, RouteId = 10 };
    private static TripLandmarkCandidate Candidate(int id, double progress, string name) => new()
    {
        TripLandmarkCandidateId = Guid.NewGuid(), LegIndex = 0,
        ExternalPlaceId = $"openstreetmap:venue:{id}", Name = name, Category = "place",
        DistanceFromRouteStartMeters = progress
    };
}
