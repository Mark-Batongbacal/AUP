using backend.Models.Database;
using backend.Models.Routing;
using backend.Services.Assistant;
using backend.Services.Routing;
using backend.Services.Telemetry;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class JourneyPlanningFacadeServiceTests
{
    [Fact]
    public async Task Plan_WhenAdmissionIsRejected_PreservesOverloadOutcome()
    {
        var routing = new Mock<IRoutingService>();
        routing.Setup(item => item.PlanTripsAsync(
                15, 120, 15.1, 120.1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RoutingAdmissionRejectedException(
                RoutingAdmissionRejectionReason.QueueFull,
                retryAfterSeconds: 5));
        var telemetryScope = new Mock<IRoutingTelemetryScope>();
        var telemetry = new Mock<ITukiTelemetry>();
        telemetry.Setup(item => item.BeginRoutingPlan(
                "JourneyPlanningFacadeService",
                It.IsAny<CancellationToken>()))
            .Returns(telemetryScope.Object);
        telemetry.Setup(item => item.MeasureRouting("routing_service_ms"))
            .Returns(Mock.Of<IDisposable>());
        var service = new JourneyPlanningFacadeService(
            routing.Object,
            Mock.Of<IJourneyPlanPersistenceService>(),
            telemetry: telemetry.Object);

        await Assert.ThrowsAsync<RoutingAdmissionRejectedException>(() =>
            service.PlanAsync(
                Guid.Empty,
                new JourneyPlanRequest(
                    15, 120, "Market", 15.1, 120.1)));

        telemetryScope.Verify(
            item => item.Complete("admission_rejected"),
            Times.Once);
    }

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

    [Fact]
    public async Task Plan_WhenGuest_ReturnsTransientRecommendationsWithoutPersistence()
    {
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
        var persistence = new Mock<IJourneyPlanPersistenceService>(MockBehavior.Strict);
        var service = new JourneyPlanningFacadeService(routing.Object, persistence.Object);

        var result = await service.PlanAsync(Guid.Empty,
            new JourneyPlanRequest(15, 120, "Market", 15.1, 120.1));

        var recommendation = Assert.Single(result);
        Assert.NotEqual(Guid.Empty, recommendation.RecommendationId);
        Assert.Same(plan, recommendation.Plan);
        persistence.Verify(
            item => item.PersistAsync(
                It.IsAny<Guid>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<JeepneyTripPlan>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
