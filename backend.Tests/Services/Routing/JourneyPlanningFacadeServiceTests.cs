using backend.Models.Database;
using backend.Models.Routing;
using backend.Services.Assistant;
using backend.Services.Routing;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class JourneyPlanningFacadeServiceTests
{
    [Fact]
    public async Task Plan_PersistsDeterministicPlansAndReturnsStartableRecommendationIds()
    {
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var plan = new JeepneyTripPlan
        {
            RecommendationType = "fastest",
            TotalFarePesos = 13,
            OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
            DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
            Legs = [new JeepneyTripLeg { Mode = AccessMode.Jeepney }]
        };
        var routing = new Mock<IRoutingService>();
        routing.Setup(item => item.PlanTripsAsync(15, 120, 15.1, 120.1, default))
            .ReturnsAsync([plan]);
        var persistence = new Mock<IJourneyPlanPersistenceService>();
        persistence.Setup(item => item.PersistAsync(userId, 15, 120, "Market",
                15.1, 120.1, null, null, It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(new RouteRecommendation
            {
                RecommendationId = recommendationId
            }, plan)]);
        var service = new JourneyPlanningFacadeService(routing.Object, persistence.Object);

        var result = await service.PlanAsync(userId,
            new JourneyPlanRequest(15, 120, "Market", 15.1, 120.1));

        var recommendation = Assert.Single(result);
        Assert.Equal(recommendationId, recommendation.RecommendationId);
        Assert.Same(plan, recommendation.Plan);
    }
}
