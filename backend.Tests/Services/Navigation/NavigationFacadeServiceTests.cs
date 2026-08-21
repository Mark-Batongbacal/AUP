using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.TripSessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class NavigationFacadeServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _recommendationId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Mock<ITripSessionService> _tripSessions = new();
    private readonly Mock<ITripSessionRepository> _sessions = new();
    private readonly Mock<IRouteRecommendationRepository> _recommendations = new();
    private readonly Mock<INavigationInstructionRepository> _instructions = new();
    private readonly Mock<ITripLandmarkCandidateRepository> _landmarks = new();
    private readonly Mock<ILocationTrackingService> _location = new();
    private readonly Mock<IReroutingService> _rerouting = new();
    private readonly Mock<INavigationSpeechService> _speech = new();

    [Fact]
    public async Task Start_IsOneFacadeOperationAndReturnsCompleteSnapshot()
    {
        var planned = Session(TripNavigationState.Planned);
        var started = Session(TripNavigationState.WaitingToBoard);
        _tripSessions.Setup(item => item.CreateAsync(_userId,
                new CreateTripSessionRequest(_recommendationId), default))
            .ReturnsAsync(new TripSessionOperation(planned));
        _tripSessions.Setup(item => item.StartAsync(_userId, _sessionId, default))
            .ReturnsAsync(new TripSessionOperation(started));
        SetupSnapshot(started);

        var result = await Service().StartAsync(_userId, _recommendationId);

        Assert.True(result.Succeeded);
        Assert.Equal(_sessionId, result.Snapshot!.SessionId);
        Assert.Equal("WaitingToBoard", result.Snapshot.State);
        Assert.Equal("BoardJeepney", result.Snapshot.NextInstruction!.Type);
        Assert.True(result.Snapshot.RequiresBoardingConfirmation);
        Assert.Equal(13, result.Snapshot.EstimatedRemainingFare);
        _tripSessions.VerifyAll();
    }

    [Fact]
    public async Task PrepareToAlight_At200Meters_DoesNotExposeConfirmation()
    {
        var session = Session(TripNavigationState.ApproachingAlightPoint);
        session.CurrentProgressMeters = 800;
        SetupSnapshot(session, includeAlightLandmark: true);
        _sessions.Setup(item => item.GetOwnedAsync(_sessionId, _userId, default)).ReturnsAsync(session);
        _location.Setup(item => item.ProcessAsync(_userId, _sessionId,
                It.IsAny<LocationUpdate>(), default))
            .ReturnsAsync(new LocationUpdateResult(true, "ApproachingAlightPoint", 800, 800, 3));

        var result = await Service().UpdateLocationAsync(_userId, _sessionId,
            new LocationUpdate(15, 120, 5, DateTime.UtcNow));

        Assert.Equal("ApproachingAlightPoint", result.Snapshot!.State);
        Assert.Equal("PrepareToAlight", result.Snapshot.NextInstruction!.Type);
        Assert.Equal("ALIGHT_REFERENCE", result.Snapshot.Landmark!.Role);
        Assert.Equal(200, result.Snapshot.RemainingDistanceMeters);
        Assert.False(result.Snapshot.RequiresAlightingConfirmation);
    }

    [Fact]
    public async Task PrepareToAlight_Within75Meters_ExposesConfirmationAndFareState()
    {
        var session = Session(TripNavigationState.ApproachingAlightPoint);
        session.CurrentProgressMeters = 940;
        session.ApproxFareSpent = 13;
        SetupSnapshot(session, includeAlightLandmark: true);
        _sessions.Setup(item => item.GetOwnedAsync(_sessionId, _userId, default)).ReturnsAsync(session);
        _location.Setup(item => item.ProcessAsync(_userId, _sessionId,
                It.IsAny<LocationUpdate>(), default))
            .ReturnsAsync(new LocationUpdateResult(true, "ApproachingAlightPoint", 940, 940, 3));

        var result = await Service().UpdateLocationAsync(_userId, _sessionId,
            new LocationUpdate(15, 120, 5, DateTime.UtcNow));

        Assert.True(result.Snapshot!.RequiresAlightingConfirmation);
        Assert.Equal(60, result.Snapshot.RemainingDistanceMeters);
        Assert.Equal(13, result.Snapshot.ApproxFareSpent);
        Assert.Equal(13, result.Snapshot.EstimatedRemainingFare);
    }

    [Fact]
    public async Task BoardLandmark_IsPassedToSpeechAsTrustedStructuredFact()
    {
        var session = Session(TripNavigationState.WaitingToBoard);
        SetupSnapshot(session, includeBoardLandmark: true);
        NavigationSpeechContext? received = null;
        _speech.Setup(item => item.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .Callback((NavigationSpeechContext context, CancellationToken _) => received = context)
            .ReturnsAsync("Sumakay ka ng Marisol jeep sa may McDonald's.");
        _tripSessions.Setup(item => item.GetActiveAsync(_userId, default))
            .ReturnsAsync(new TripSessionOperation(session));

        var result = await Service().GetActiveAsync(_userId);

        Assert.Equal("Marisol", received!.RouteName);
        Assert.Equal("McDonald's", received.LandmarkName);
        Assert.Equal("BOARD_REFERENCE", received.LandmarkRole);
        Assert.Equal("WaitingToBoard", result.Snapshot!.State);
    }

    [Fact]
    public async Task AiFailure_UsesFallbackAndNavigationStillSucceeds()
    {
        var session = Session(TripNavigationState.WaitingToBoard);
        SetupSnapshot(session, includeBoardLandmark: true);
        _speech.Setup(item => item.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .ThrowsAsync(new HttpRequestException("provider unavailable"));
        _tripSessions.Setup(item => item.GetActiveAsync(_userId, default))
            .ReturnsAsync(new TripSessionOperation(session));

        var result = await Service().GetActiveAsync(_userId);

        Assert.True(result.Succeeded);
        Assert.Contains("McDonald's", result.Snapshot!.SpokenInstruction);
        Assert.Equal("WaitingToBoard", result.Snapshot.State);
    }

    [Fact]
    public async Task RepeatedUnchangedLocation_DoesNotCallSpeechAgain()
    {
        var session = Session(TripNavigationState.OnJeepney);
        SetupSnapshot(session);
        _sessions.Setup(item => item.GetOwnedAsync(_sessionId, _userId, default)).ReturnsAsync(session);
        _location.Setup(item => item.ProcessAsync(_userId, _sessionId,
                It.IsAny<LocationUpdate>(), default))
            .ReturnsAsync(new LocationUpdateResult(true, "OnJeepney", 400, 400, 2));
        var service = Service();
        var update = new LocationUpdate(15, 120, 5, DateTime.UtcNow);

        await service.UpdateLocationAsync(_userId, _sessionId, update);
        await service.UpdateLocationAsync(_userId, _sessionId, update);

        _speech.Verify(item => item.PhraseAsync(
            It.IsAny<NavigationSpeechContext>(), default), Times.Once);
    }

    [Fact]
    public async Task PingAfterLandmark_DoesNotGenerateASecondNonEventSpeech()
    {
        var session = Session(TripNavigationState.OnJeepney);
        SetupSnapshot(session);
        _sessions.Setup(item => item.GetOwnedAsync(_sessionId, _userId, default)).ReturnsAsync(session);
        var landmark = new NavigationInstruction
        {
            TripSessionId = _sessionId, LegIndex = 0,
            Type = NavigationInstructionType.LandmarkNotice,
            Text = "You just passed Jollibee.", Latitude = 15, Longitude = 120
        };
        _location.SetupSequence(item => item.ProcessAsync(_userId, _sessionId,
                It.IsAny<LocationUpdate>(), default))
            .ReturnsAsync(new LocationUpdateResult(true, "OnJeepney",
                TriggeredInstructions: [landmark]))
            .ReturnsAsync(new LocationUpdateResult(true, "OnJeepney"));
        var service = Service();
        var update = new LocationUpdate(15, 120, 5, DateTime.UtcNow);

        await service.UpdateLocationAsync(_userId, _sessionId, update);
        await service.UpdateLocationAsync(_userId, _sessionId, update);

        _speech.Verify(item => item.PhraseAsync(
            It.IsAny<NavigationSpeechContext>(), default), Times.Once);
        Assert.Contains("LandmarkNotice", session.LastSpeechEventKey);
    }

    [Fact]
    public async Task AiText_CannotChangeDeterministicOffRouteState()
    {
        var session = Session(TripNavigationState.OffRoute);
        SetupSnapshot(session);
        _sessions.Setup(item => item.GetOwnedAsync(_sessionId, _userId, default)).ReturnsAsync(session);
        _location.Setup(item => item.ProcessAsync(_userId, _sessionId,
                It.IsAny<LocationUpdate>(), default))
            .ReturnsAsync(new LocationUpdateResult(true, "OFF_ROUTE"));
        _rerouting.Setup(item => item.RerouteAsync(_userId, _sessionId,
                It.Is<NavigationRerouteRequest>(request => request.Reason == "OFF_ROUTE"), default))
            .ReturnsAsync(new RerouteResult(false, "NO_REROUTE_AVAILABLE"));
        _speech.Setup(item => item.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .ReturnsAsync("You have arrived.");

        var result = await Service().UpdateLocationAsync(_userId, _sessionId,
            new LocationUpdate(15, 120, 5, DateTime.UtcNow));

        Assert.Equal("OffRoute", result.Snapshot!.State);
        Assert.True(result.Snapshot.RerouteRequired);
        Assert.Equal("OffRoute", result.Snapshot.NextInstruction!.Type);
    }

    [Fact]
    public async Task ActiveResume_ReusesPersistedSpeechForSameEvent()
    {
        var session = Session(TripNavigationState.WaitingToBoard);
        SetupSnapshot(session);
        _tripSessions.Setup(item => item.GetActiveAsync(_userId, default))
            .ReturnsAsync(new TripSessionOperation(session));
        var service = Service();

        var first = await service.GetActiveAsync(_userId);
        var second = await service.GetActiveAsync(_userId);

        Assert.Equal(first.Snapshot!.SpokenInstruction, second.Snapshot!.SpokenInstruction);
        _speech.Verify(item => item.PhraseAsync(
            It.IsAny<NavigationSpeechContext>(), default), Times.Once);
    }

    private NavigationFacadeService Service()
    {
        _sessions.Setup(item => item.UpdateAsync(It.IsAny<TripSession>(), default))
            .ReturnsAsync((TripSession session, CancellationToken _) => session);
        return new(_tripSessions.Object, _sessions.Object, _recommendations.Object,
            _instructions.Object, _landmarks.Object, _location.Object, _rerouting.Object,
            _speech.Object, Options.Create(new NavigationOptions { ConfirmAlightDistanceMeters = 75 }),
            NullLogger<NavigationFacadeService>.Instance);
    }

    private void SetupSnapshot(TripSession session,
        bool includeBoardLandmark = false, bool includeAlightLandmark = false)
    {
        _speech.Setup(item => item.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .ReturnsAsync("Tuki instruction");
        _recommendations.Setup(item => item.GetOrderedLegsAsync(_recommendationId, default))
            .ReturnsAsync([new RecommendationLeg
            {
                LegOrder = 0,
                TransportMode = new TransportMode { Code = "JEEPNEY" },
                Route = new TransportRoute { RouteName = "Marisol" },
                StartLatitude = 15, StartLongitude = 120,
                EndLatitude = 15, EndLongitude = 120.01,
                DistanceMeters = 1000, EstimatedFare = 13
            }]);
        _instructions.Setup(item => item.GetForOwnedSessionAsync(_sessionId, _userId, default))
            .ReturnsAsync([
                Instruction(NavigationInstructionType.BoardJeepney, true),
                Instruction(NavigationInstructionType.Continue),
                Instruction(NavigationInstructionType.PrepareToAlight),
                Instruction(NavigationInstructionType.AlightJeepney, true)
            ]);
        var values = new List<TripLandmarkCandidate>();
        if (includeBoardLandmark) values.Add(new TripLandmarkCandidate
        {
            Name = "McDonald's", Category = "fast_food", Role = LandmarkRole.BoardReference,
            Relation = LandmarkRelation.NearBoardPoint, Latitude = 15, Longitude = 120,
            DistanceFromTargetMeters = 20
        });
        if (includeAlightLandmark) values.Add(new TripLandmarkCandidate
        {
            Name = "Jollibee", Category = "fast_food", Role = LandmarkRole.AlightReference,
            Relation = LandmarkRelation.BeforeAlight, Latitude = 15, Longitude = 120.009,
            DistanceFromTargetMeters = 100
        });
        _landmarks.Setup(item => item.GetForLegAsync(_sessionId, 0, default)).ReturnsAsync(values);
    }

    private NavigationInstruction Instruction(NavigationInstructionType type, bool confirmation = false) => new()
    {
        TripSessionId = _sessionId, LegIndex = 0, Type = type,
        Audience = NavigationInstructionAudience.Passenger,
        RequiresConfirmation = confirmation
    };

    private TripSession Session(TripNavigationState state) => new()
    {
        TripSessionId = _sessionId, UserId = _userId,
        RecommendationId = _recommendationId,
        CurrentNavigationState = state
    };
}
