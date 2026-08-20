using backend.Models.Database;
using backend.Repositories;
using backend.Services.TripSessions;
using backend.Services.Navigation;
using Moq;

namespace backend.Tests.Services.TripSessions;

public sealed class TripSessionServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _recommendationId = Guid.NewGuid();
    private readonly Guid _searchId = Guid.NewGuid();
    private readonly Mock<ITripSessionRepository> _sessions = new();
    private readonly Mock<IRouteRecommendationRepository> _recommendations = new();
    private readonly Mock<ITripSearchRepository> _searches = new();
    private readonly Mock<INavigationInstructionService> _navigation = new();
    private readonly Mock<ILandmarkCorridorPrefetchService> _landmarkPrefetch = new();

    [Fact]
    public async Task Create_PreservesJourneyCoordinatesAndConstraints()
    {
        SetupOwnedJourney();
        _sessions.Setup(repository => repository.AddAsync(It.IsAny<TripSession>(), default))
            .ReturnsAsync((TripSession session, CancellationToken _) => session);

        var result = await Service().CreateAsync(
            _userId, new CreateTripSessionRequest(_recommendationId));

        Assert.True(result.Succeeded);
        Assert.Equal(TripNavigationState.Planned, result.Session!.CurrentNavigationState);
        Assert.Equal(80, result.Session.OriginalBudget);
        Assert.Equal("cheapest", result.Session.OriginalPreference);
        Assert.Equal("SM Clark", result.Session.DestinationName);
    }

    [Fact]
    public async Task Create_RejectsRecommendationOwnedByAnotherUser()
    {
        SetupOwnedJourney(Guid.NewGuid());
        var result = await Service().CreateAsync(
            _userId, new CreateTripSessionRequest(_recommendationId));
        Assert.False(result.Succeeded);
        Assert.Equal("JOURNEY_NOT_FOUND", result.Error);
        _sessions.Verify(repository => repository.AddAsync(
            It.IsAny<TripSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_UsesOwnedRepositoryLookup()
    {
        var sessionId = Guid.NewGuid();
        await Service().GetAsync(_userId, sessionId);
        _sessions.Verify(repository => repository.GetOwnedAsync(sessionId, _userId, default));
    }

    [Fact]
    public async Task ActiveLookup_ReturnsCurrentOwnedSession()
    {
        var session = Session(TripNavigationState.OnJeepney);
        _sessions.Setup(repository => repository.GetActiveOwnedAsync(_userId, default))
            .ReturnsAsync(session);
        var result = await Service().GetActiveAsync(_userId);
        Assert.Same(session, result.Session);
    }

    [Fact]
    public async Task Start_TransitionsPlannedSessionToFirstLegState()
    {
        SetupSession(TripNavigationState.Planned);
        var result = await Service().StartAsync(_userId, Guid.Parse("10000000-0000-0000-0000-000000000001"));
        Assert.Equal(TripNavigationState.WalkingToDestination, result.Session!.CurrentNavigationState);
        Assert.NotNull(result.Session.StartedAt);
    }

    [Fact]
    public async Task InvalidTransition_DoesNotPersist()
    {
        SetupSession(TripNavigationState.OnJeepney);
        var result = await Service().StartAsync(_userId, Guid.Parse("10000000-0000-0000-0000-000000000001"));
        Assert.Equal("INVALID_STATE_TRANSITION", result.Error);
        _sessions.Verify(repository => repository.UpdateAsync(
            It.IsAny<TripSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_IsTerminal()
    {
        SetupSession(TripNavigationState.Planned);
        var service = Service();
        var cancelled = await service.CancelAsync(_userId, Guid.Parse("10000000-0000-0000-0000-000000000001"));
        Assert.Equal(TripNavigationState.Cancelled, cancelled.Session!.CurrentNavigationState);
        Assert.NotNull(cancelled.Session.CancelledAt);
        var restart = await service.StartAsync(_userId, cancelled.Session.TripSessionId);
        Assert.Equal("INVALID_STATE_TRANSITION", restart.Error);
    }

    private TripSessionService Service()
    {
        _navigation.Setup(service => service.GenerateAsync(
                It.IsAny<TripSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NavigationInstruction>());
        _landmarkPrefetch.Setup(service => service.PrefetchAsync(
                It.IsAny<TripSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new(_sessions.Object, _recommendations.Object, _searches.Object,
            new TripSessionStateMachine(), _navigation.Object, _landmarkPrefetch.Object);
    }

    private void SetupOwnedJourney(Guid? owner = null)
    {
        _recommendations.Setup(repository => repository.GetByIdAsync(_recommendationId, default))
            .ReturnsAsync(new RouteRecommendation { RecommendationId = _recommendationId, TripSearchId = _searchId });
        _searches.Setup(repository => repository.GetByIdAsync(_searchId, default))
            .ReturnsAsync(new TripSearch
            {
                TripSearchId = _searchId, UserId = owner ?? _userId,
                OriginLatitude = 15.1, OriginLongitude = 120.5,
                DestinationLatitude = 15.2, DestinationLongitude = 120.6,
                DestinationName = "SM Clark", Budget = 80, Preference = "cheapest"
            });
    }

    private void SetupSession(TripNavigationState state)
    {
        var session = Session(state);
        _sessions.Setup(repository => repository.GetOwnedAsync(session.TripSessionId, _userId, default))
            .ReturnsAsync(session);
        _sessions.Setup(repository => repository.UpdateAsync(session, default)).ReturnsAsync(session);
        _recommendations.Setup(repository => repository.GetOrderedLegsAsync(
                session.RecommendationId, default))
            .ReturnsAsync([new RecommendationLeg
            {
                LegOrder = 0,
                TransportMode = new TransportMode { Code = "WALK" }
            }]);
    }

    private TripSession Session(TripNavigationState state) => new()
    {
        TripSessionId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        UserId = _userId, RecommendationId = _recommendationId,
        CurrentNavigationState = state
    };
}
