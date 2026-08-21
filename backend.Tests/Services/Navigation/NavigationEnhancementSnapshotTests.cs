using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.TripSessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class NavigationEnhancementSnapshotTests
{
    [Fact]
    public async Task ActiveSnapshot_ExposesInstructionAfterCurrentAsFollowingGuide()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var session = Session(userId, sessionId, recommendationId, TripNavigationState.OnJeepney);

        var tripSessions = new Mock<ITripSessionService>();
        var sessions = new Mock<ITripSessionRepository>();
        var recommendations = new Mock<IRouteRecommendationRepository>();
        var instructions = new Mock<INavigationInstructionRepository>();
        var landmarks = new Mock<ITripLandmarkCandidateRepository>();
        var speech = new Mock<INavigationSpeechService>();

        tripSessions.Setup(x => x.GetActiveAsync(userId, default))
            .ReturnsAsync(new TripSessionOperation(session));
        sessions.Setup(x => x.UpdateAsync(It.IsAny<TripSession>(), default))
            .ReturnsAsync((TripSession value, CancellationToken _) => value);
        recommendations.Setup(x => x.GetOrderedLegsAsync(recommendationId, default))
            .ReturnsAsync([JeepneyLeg()]);
        instructions.Setup(x => x.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([
                Instruction(sessionId, 0, NavigationInstructionType.BoardJeepney, 0, "Board Marisol."),
                Instruction(sessionId, 0, NavigationInstructionType.Continue, 1, "Stay on Marisol."),
                Instruction(sessionId, 0, NavigationInstructionType.PrepareToAlight, 2, "Prepare to get off."),
                Instruction(sessionId, 0, NavigationInstructionType.AlightJeepney, 3, "Get off here.")
            ]);
        landmarks.Setup(x => x.GetForLegAsync(sessionId, 0, default)).ReturnsAsync([]);
        speech.Setup(x => x.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .ReturnsAsync("Stay on Marisol.");

        var service = Service(tripSessions, sessions, recommendations, instructions, landmarks, speech);
        var result = await service.GetActiveAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Equal("Continue", result.Snapshot!.NextInstruction!.Type);
        Assert.Equal("PrepareToAlight", result.Snapshot.FollowingInstruction!.Type);
        Assert.Equal("Prepare to get off.", result.Snapshot.FollowingInstruction.Text);
    }

    [Fact]
    public async Task CompletedSession_ReadReturnsArrivalSummaryAndNoFollowingGuide()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var started = DateTime.UtcNow.AddMinutes(-42);
        var session = Session(userId, sessionId, recommendationId, TripNavigationState.Arrived);
        session.StartedAt = started;
        session.CompletedAt = started.AddMinutes(42);
        session.DestinationName = "SM City Clark";
        session.ApproxFareSpent = 39;

        var tripSessions = new Mock<ITripSessionService>();
        var sessions = new Mock<ITripSessionRepository>();
        var recommendations = new Mock<IRouteRecommendationRepository>();
        var instructions = new Mock<INavigationInstructionRepository>();
        var landmarks = new Mock<ITripLandmarkCandidateRepository>();
        var speech = new Mock<INavigationSpeechService>();

        tripSessions.Setup(x => x.GetAsync(userId, sessionId, default))
            .ReturnsAsync(new TripSessionOperation(session));
        sessions.Setup(x => x.UpdateAsync(It.IsAny<TripSession>(), default))
            .ReturnsAsync((TripSession value, CancellationToken _) => value);
        recommendations.Setup(x => x.GetOrderedLegsAsync(recommendationId, default))
            .ReturnsAsync([
                JeepneyLeg(0),
                new RecommendationLeg
                {
                    LegOrder = 1,
                    TransportMode = new TransportMode { Code = "TRICYCLE" },
                    EstimatedFare = 26,
                    DistanceMeters = 1000
                }
            ]);
        instructions.Setup(x => x.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([
                Instruction(sessionId, 1, NavigationInstructionType.Arrived, 4, "You have arrived.")
            ]);
        landmarks.Setup(x => x.GetForLegAsync(sessionId, 0, default)).ReturnsAsync([]);
        speech.Setup(x => x.PhraseAsync(It.IsAny<NavigationSpeechContext>(), default))
            .ReturnsAsync("You have arrived.");

        var service = Service(tripSessions, sessions, recommendations, instructions, landmarks, speech);
        var result = await service.GetAsync(userId, sessionId);

        Assert.True(result.Succeeded);
        Assert.Null(result.Snapshot!.FollowingInstruction);
        Assert.NotNull(result.Snapshot.TripSummary);
        Assert.Equal("SM City Clark", result.Snapshot.TripSummary!.DestinationName);
        Assert.Equal(42, result.Snapshot.TripSummary.DurationMinutes);
        Assert.Equal(39, result.Snapshot.TripSummary.ApproxFareSpent);
        Assert.Equal(2, result.Snapshot.TripSummary.TransitLegs);
        Assert.Equal(1, result.Snapshot.TripSummary.Transfers);
    }

    private static NavigationFacadeService Service(
        Mock<ITripSessionService> tripSessions,
        Mock<ITripSessionRepository> sessions,
        Mock<IRouteRecommendationRepository> recommendations,
        Mock<INavigationInstructionRepository> instructions,
        Mock<ITripLandmarkCandidateRepository> landmarks,
        Mock<INavigationSpeechService> speech) =>
        new(
            tripSessions.Object,
            sessions.Object,
            recommendations.Object,
            instructions.Object,
            landmarks.Object,
            Mock.Of<ILocationTrackingService>(),
            Mock.Of<IReroutingService>(),
            speech.Object,
            Options.Create(new NavigationOptions()),
            NullLogger<NavigationFacadeService>.Instance);

    private static TripSession Session(Guid userId, Guid sessionId, Guid recommendationId, TripNavigationState state) => new()
    {
        TripSessionId = sessionId,
        UserId = userId,
        RecommendationId = recommendationId,
        CurrentNavigationState = state,
        CurrentLegIndex = 0,
        DestinationName = "Destination"
    };

    private static RecommendationLeg JeepneyLeg(int order = 0) => new()
    {
        LegOrder = order,
        TransportMode = new TransportMode { Code = "JEEPNEY" },
        Route = new TransportRoute { RouteName = "Marisol" },
        DistanceMeters = 1000,
        EstimatedFare = 13
    };

    private static NavigationInstruction Instruction(
        Guid sessionId,
        int legIndex,
        NavigationInstructionType type,
        int sequence,
        string text) => new()
    {
        TripSessionId = sessionId,
        LegIndex = legIndex,
        Type = type,
        Sequence = sequence,
        Text = text,
        Audience = NavigationInstructionAudience.Passenger
    };
}
