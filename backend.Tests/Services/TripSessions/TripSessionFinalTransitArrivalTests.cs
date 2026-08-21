using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.TripSessions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.TripSessions;

public sealed class TripSessionFinalTransitArrivalTests
{
    [Fact]
    public async Task ConfirmAlighting_FinalTransitLeg_CompletesTripAndRecordsFare()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var session = new TripSession
        {
            TripSessionId = sessionId,
            UserId = userId,
            RecommendationId = recommendationId,
            CurrentNavigationState = TripNavigationState.ApproachingAlightPoint,
            CurrentLegIndex = 0,
            CurrentProgressMeters = 950,
            StartedAt = DateTime.UtcNow.AddMinutes(-20)
        };

        var sessions = new Mock<ITripSessionRepository>();
        var recommendations = new Mock<IRouteRecommendationRepository>();
        sessions.Setup(x => x.GetOwnedAsync(sessionId, userId, default)).ReturnsAsync(session);
        sessions.Setup(x => x.UpdateAsync(session, default)).ReturnsAsync(session);
        recommendations.Setup(x => x.GetOrderedLegsAsync(recommendationId, default))
            .ReturnsAsync([
                new RecommendationLeg
                {
                    LegOrder = 0,
                    DistanceMeters = 1000,
                    EstimatedFare = 13,
                    TransportMode = new TransportMode { Code = "JEEPNEY" }
                }
            ]);

        var service = new TripSessionService(
            sessions.Object,
            recommendations.Object,
            Mock.Of<ITripSearchRepository>(),
            new TripSessionStateMachine(),
            Mock.Of<INavigationInstructionService>(),
            Mock.Of<ILandmarkCorridorPrefetchService>(),
            Options.Create(new NavigationOptions { ConfirmAlightDistanceMeters = 75 }));

        var result = await service.ConfirmAlightingAsync(userId, sessionId);

        Assert.True(result.Succeeded);
        Assert.Equal(TripNavigationState.Arrived, result.Session!.CurrentNavigationState);
        Assert.Equal(13, result.Session.ApproxFareSpent);
        Assert.NotNull(result.Session.CompletedAt);
        Assert.True(result.Session.CompletedAt >= result.Session.StartedAt);
    }
}
