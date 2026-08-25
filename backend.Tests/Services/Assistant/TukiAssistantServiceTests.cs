using backend.Models.Database;
using backend.Models.Destinations;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Assistant;
using backend.Services.Destinations;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace backend.Tests.Services.Assistant;

public sealed class TukiAssistantServiceTests
{
    private readonly Mock<IAssistantIntentExtractor> _extractor = new();
    private readonly Mock<IDestinationSearchService> _destinations = new();
    private readonly Mock<IRoutingService> _routing = new();
    private readonly Mock<ITripSessionRepository> _sessions = new();
    private readonly Mock<INavigationInstructionRepository> _instructions = new();
    private readonly Mock<IJourneyPlanPersistenceService> _persistence = new();

    [Fact]
    public async Task Planning_AmbiguousDestination_RequestsClarificationWithoutRouting()
    {
        PlanIntent("SM");
        _destinations.Setup(service => service.SearchAsync("SM",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse(
            [
                Place("1", "SM Clark"), Place("2", "SM Pampanga")
            ]));

        var result = await Service().RespondPlanningAsync(
            Guid.NewGuid(), new("Take me to SM", 15.1, 120.5));

        Assert.Equal("DESTINATION_AMBIGUOUS", result.Status);
        Assert.Equal("PLANNING", result.Surface);
        Assert.Equal(2, result.Destinations!.Count);
        _routing.Verify(service => service.PlanTripsAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Planning_UniqueExactDestination_IsSelectedFromRelatedResults()
    {
        PlanIntent("SM City Clark");
        _destinations.Setup(service => service.SearchAsync("SM City Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse([
                Place("mall", "SM City Clark"), Place("bus", "SM City Clark Bus Station")
            ]));
        _routing.Setup(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6, default))
            .ReturnsAsync([Plan(52)]);
        var recommendationId = Guid.NewGuid();
        _persistence.Setup(item => item.PersistAsync(
                It.IsAny<Guid>(), 15.1, 120.5, "SM City Clark", 15.2, 120.6,
                null, null, It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = recommendationId }, Plan(52))]);

        var result = await Service().RespondPlanningAsync(
            Guid.NewGuid(), new("Take me to SM City Clark", 15.1, 120.5));

        Assert.Equal("JOURNEYS_AVAILABLE", result.Status);
        Assert.Equal("PLANNING", result.Surface);
        Assert.Equal(recommendationId, Assert.Single(result.Journeys!).JourneyId);
        Assert.Equal("SELECT_ROUTE", result.Action?.Type);
        Assert.True(result.Action?.RequiresConfirmation);
    }

    [Fact]
    public async Task Planning_ExplicitDestinationId_SelectsRequestedResult()
    {
        PlanIntent("SM Clark");
        _destinations.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse([
                Place("mall", "SM City Clark"), Place("bus", "SM City Clark Bus Station")
            ]));
        _routing.Setup(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6, default))
            .ReturnsAsync([]);

        var result = await Service().RespondPlanningAsync(
            Guid.NewGuid(),
            new("Take me to SM Clark", 15.1, 120.5, DestinationId: "bus"));

        Assert.Equal("NO_JOURNEY_WITHIN_CONSTRAINTS", result.Status);
        _routing.Verify(service => service.PlanTripsAsync(
            15.1, 120.5, 15.2, 120.6, default), Times.Once);
    }

    [Fact]
    public async Task Planning_BudgetFiltering_IsDeterministic()
    {
        PlanIntent("SM Clark", 80);
        _destinations.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse([Place("1", "SM Clark")]));
        _routing.Setup(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6, default))
            .ReturnsAsync([Plan(52), Plan(85)]);
        var userId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        _persistence.Setup(item => item.PersistAsync(
                userId, 15.1, 120.5, "SM Clark", 15.2, 120.6, 80, null,
                It.Is<IReadOnlyList<JeepneyTripPlan>>(plans =>
                    plans.Count == 1 && plans[0].TotalFarePesos == 52),
                default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = recommendationId }, Plan(52))]);

        var result = await Service().RespondPlanningAsync(
            userId, new("SM Clark under 80", 15.1, 120.5));

        var journey = Assert.Single(result.Journeys!);
        Assert.Equal(52, journey.FarePesos);
        Assert.Equal(recommendationId, journey.JourneyId);
    }

    [Fact]
    public async Task ActiveTrip_OffRouteQuestion_UsesOwnedSessionState()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        _sessions.Setup(item => item.GetOwnedAsync(sessionId, userId, default)).ReturnsAsync(
            ActiveSession(userId, sessionId, TripNavigationState.OffRoute, "OFF_ROUTE"));
        _instructions.Setup(item => item.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([]);

        var result = await Service().RespondActiveTripAsync(
            userId, sessionId, new("Am I still going the right way?"));

        Assert.Equal("OFF_ROUTE", result.Status);
        Assert.Equal("ACTIVE_TRIP", result.Surface);
        Assert.Equal(sessionId, result.Navigation?.TripSessionId);
        _extractor.Verify(
            item => item.ExtractAsync(It.IsAny<AssistantContext>(), default),
            Times.Never);
    }

    [Fact]
    public async Task ActiveTrip_NavigationStatus_DoesNotDependOnExternalAi()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<AssistantContext>(), default))
            .ThrowsAsync(new HttpRequestException());
        _sessions.Setup(item => item.GetOwnedAsync(sessionId, userId, default)).ReturnsAsync(
            ActiveSession(userId, sessionId, TripNavigationState.OnJeepney, "ON_ROUTE"));
        _instructions.Setup(item => item.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([]);

        var result = await Service().RespondActiveTripAsync(
            userId, sessionId, new("Am I still going the right way?"));

        Assert.Equal("ON_ROUTE", result.Status);
        _extractor.Verify(
            item => item.ExtractAsync(It.IsAny<AssistantContext>(), default),
            Times.Never);
    }

    [Fact]
    public async Task ActiveTrip_ConstraintChange_ProducesProposalWithoutMutatingSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = ActiveSession(
            userId, sessionId, TripNavigationState.OnJeepney, "ON_ROUTE");
        session.LastLatitude = 15.1;
        session.LastLongitude = 120.5;
        session.OriginalBudget = 100;
        session.ApproxFareSpent = 20;

        _sessions.Setup(item => item.GetOwnedAsync(sessionId, userId, default))
            .ReturnsAsync(session);
        _instructions.Setup(item => item.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([]);
        _extractor.Setup(item => item.ExtractAsync(
                It.Is<AssistantContext>(context =>
                    context.Surface == AssistantSurface.ActiveTrip &&
                    context.ActiveTrip != null &&
                    context.ActiveTrip.TripSessionId == sessionId),
                default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.UpdateTripConstraints,
                BudgetPesos = 30
            });
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.2, 120.6, default))
            .ReturnsAsync([Plan(26)]);
        var proposedRecommendationId = Guid.NewGuid();
        _persistence.Setup(item => item.PersistAsync(
                userId, 15.1, 120.5, "SM Clark", 15.2, 120.6,
                30, null, It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = proposedRecommendationId },
                Plan(26))]);

        var result = await Service().RespondActiveTripAsync(
            userId, sessionId, new("I only have 30 pesos left"));

        Assert.Equal("REPLAN_PROPOSAL", result.Status);
        Assert.Equal("CONFIRM_REPLAN_ROUTE", result.Action?.Type);
        Assert.True(result.Action?.RequiresConfirmation);
        Assert.Equal(sessionId, result.Action?.TripSessionId);
        Assert.Equal(proposedRecommendationId, Assert.Single(result.Journeys!).JourneyId);
        _sessions.Verify(item => item.UpdateAsync(
            It.IsAny<TripSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Planning_AiOutage_DoesNotCallRoutingOrSessionMutation()
    {
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<AssistantContext>(), default))
            .ThrowsAsync(new HttpRequestException());

        var result = await Service().RespondPlanningAsync(
            Guid.NewGuid(), new("Take me somewhere"));

        Assert.Equal("AI_UNAVAILABLE", result.Status);
        _routing.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
    }

    private TukiAssistantService Service() => new(
        _extractor.Object, _destinations.Object, _routing.Object,
        _sessions.Object, _instructions.Object,
        _persistence.Object,
        NullLogger<TukiAssistantService>.Instance);

    private void PlanIntent(string destination, decimal? budget = null) =>
        _extractor.Setup(item => item.ExtractAsync(
                It.Is<AssistantContext>(context => context.Surface == AssistantSurface.Planning),
                default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.PlanRoute,
                DestinationQuery = destination,
                BudgetPesos = budget
            });

    private static TripSession ActiveSession(
        Guid userId,
        Guid sessionId,
        TripNavigationState state,
        string status) =>
        new()
        {
            TripSessionId = sessionId,
            UserId = userId,
            RecommendationId = Guid.NewGuid(),
            CurrentNavigationState = state,
            LastNavigationStatus = status,
            DestinationName = "SM Clark",
            DestinationLatitude = 15.2,
            DestinationLongitude = 120.6
        };

    private static DestinationSearchResult Place(string id, string name) =>
        new(id, name, 15.2, 120.6, "venue", "pelias");

    private static JeepneyTripPlan Plan(double fare) => new()
    {
        RecommendationType = "cheapest",
        OriginAccess = Access(),
        DestinationAccess = Access(),
        TotalFarePesos = fare,
        TotalTimeSeconds = 1_200,
        GeneralizedCostPesos = fare + 60,
        Legs =
        [
            new JeepneyTripLeg
            {
                Mode = AccessMode.Jeepney,
                RouteId = "R1",
                RouteName = "Route 1",
                BoardLatitude = 15.1,
                BoardLongitude = 120.5,
                AlightLatitude = 15.2,
                AlightLongitude = 120.6,
                DistanceMeters = 5_000,
                DurationSeconds = 1_200,
                FarePesos = fare
            }
        ]
    };

    private static JeepneyAccessSegment Access() => new()
    {
        Mode = AccessMode.Walk
    };
}
