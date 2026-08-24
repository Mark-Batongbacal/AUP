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
    public async Task AmbiguousDestination_RequestsClarificationWithoutRouting()
    {
        PlanIntent("SM");
        _destinations.Setup(service => service.SearchAsync("SM",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse(
            [
                Place("1", "SM Clark"), Place("2", "SM Pampanga")
            ]));
        var result = await Service().RespondAsync(Guid.NewGuid(), new("Take me to SM", 15.1, 120.5));
        Assert.Equal("DESTINATION_AMBIGUOUS", result.Status);
        Assert.Equal(2, result.Destinations!.Count);
        _routing.Verify(service => service.PlanTripsAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UniqueExactDestination_IsSelectedFromRelatedPeliasResults()
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

        var result = await Service().RespondAsync(Guid.NewGuid(),
            new("Take me to SM City Clark", 15.1, 120.5));

        Assert.Equal("JOURNEYS_AVAILABLE", result.Status);
        Assert.Equal(recommendationId, Assert.Single(result.Journeys!).JourneyId);
    }

    [Fact]
    public async Task ExplicitDestinationId_SelectsRequestedPeliasResult()
    {
        PlanIntent("SM Clark");
        _destinations.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync(new DestinationSearchResponse([
                Place("mall", "SM City Clark"), Place("bus", "SM City Clark Bus Station")
            ]));
        _routing.Setup(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6, default))
            .ReturnsAsync([]);

        var result = await Service().RespondAsync(Guid.NewGuid(),
            new("Take me to SM Clark", 15.1, 120.5, DestinationId: "bus"));

        Assert.Equal("NO_JOURNEY_WITHIN_CONSTRAINTS", result.Status);
        _routing.Verify(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6, default), Times.Once);
    }

    [Fact]
    public async Task BudgetFiltering_IsDeterministicAndUsesPeliasBackedService()
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
                It.Is<IReadOnlyList<JeepneyTripPlan>>(plans => plans.Count == 1 && plans[0].TotalFarePesos == 52),
                default))
            .ReturnsAsync([new PersistedJourney(new RouteRecommendation { RecommendationId = recommendationId }, Plan(52))]);
        var result = await Service().RespondAsync(userId, new("SM Clark under 80", 15.1, 120.5));
        var journey = Assert.Single(result.Journeys!);
        Assert.Equal(52, journey.FarePesos);
        Assert.Equal(recommendationId, journey.JourneyId);
    }

    [Fact]
    public async Task LostWithoutActiveTrip_IsDeterministic()
    {
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new AssistantIntent { Intent = AssistantIntentType.Lost });
        var result = await Service().RespondAsync(Guid.NewGuid(), new("Where am I?"));
        Assert.Equal("NO_ACTIVE_TRIP", result.Status);
    }

    [Fact]
    public async Task LostWhileOffRoute_UsesTripSessionNotModelDecision()
    {
        var userId = Guid.NewGuid();
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new AssistantIntent { Intent = AssistantIntentType.Lost });
        _sessions.Setup(item => item.GetActiveOwnedAsync(userId, default)).ReturnsAsync(
            new TripSession
            {
                TripSessionId = Guid.NewGuid(), UserId = userId,
                CurrentNavigationState = TripNavigationState.OffRoute,
                LastNavigationStatus = "OFF_ROUTE"
            });
        _instructions.Setup(item => item.GetForOwnedSessionAsync(
                It.IsAny<Guid>(), userId, default)).ReturnsAsync([]);
        var result = await Service().RespondAsync(userId, new("Am I still going right?"));

        // The model reported intent Lost; the status must come from the trip
        // session's own OFF_ROUTE state instead. Asserting the status rather
        // than the phrasing keeps this pinned to the deterministic behaviour
        // -- the wording is presentation copy and may be reworded freely.
        Assert.Equal("OFF_ROUTE", result.Status);
        Assert.False(
            string.IsNullOrWhiteSpace(result.Message),
            "An off-route answer must still carry guidance for the passenger.");

        // Evidence that the answer was derived from the session that was read,
        // not from the model's intent classification.
        _sessions.Verify(
            item => item.GetActiveOwnedAsync(userId, default),
            Times.Once);
    }

    [Fact]
    public async Task AiOutage_DoesNotCallRoutingOrSessionMutation()
    {
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException());
        var result = await Service().RespondAsync(Guid.NewGuid(), new("Take me somewhere"));
        Assert.Equal("AI_UNAVAILABLE", result.Status);
        _routing.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NavigationStatus_DoesNotDependOnExternalAi()
    {
        var userId = Guid.NewGuid();
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException());
        _sessions.Setup(item => item.GetActiveOwnedAsync(userId, default)).ReturnsAsync(
            new TripSession
            {
                TripSessionId = Guid.NewGuid(), UserId = userId,
                CurrentNavigationState = TripNavigationState.OnJeepney,
                LastNavigationStatus = "ON_ROUTE"
            });
        _instructions.Setup(item => item.GetForOwnedSessionAsync(
                It.IsAny<Guid>(), userId, default)).ReturnsAsync([]);

        var result = await Service().RespondAsync(userId, new("Am I still going the right way?"));

        Assert.Equal("ON_ROUTE", result.Status);
        _extractor.Verify(item => item.ExtractAsync(It.IsAny<string>(), default), Times.Never);
    }

    private TukiAssistantService Service() => new(
        _extractor.Object, _destinations.Object, _routing.Object,
        _sessions.Object, _instructions.Object,
        _persistence.Object,
        NullLogger<TukiAssistantService>.Instance);

    private void PlanIntent(string destination, decimal? budget = null) =>
        _extractor.Setup(item => item.ExtractAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.PlanRoute,
                DestinationQuery = destination, BudgetPesos = budget
            });

    private static DestinationSearchResult Place(string id, string name) =>
        new(id, name, 15.2, 120.6, "venue", "pelias");

    private static JeepneyTripPlan Plan(double fare) => new()
    {
        RecommendationType = "cheapest",
        OriginAccess = Access(), DestinationAccess = Access(),
        TotalFarePesos = fare, TotalTimeSeconds = 1_200,
        GeneralizedCostPesos = fare + 60,
        Legs = [new JeepneyTripLeg
        {
            Mode = AccessMode.Jeepney, RouteId = "R1", RouteName = "Route 1",
            BoardLatitude = 15.1, BoardLongitude = 120.5,
            AlightLatitude = 15.2, AlightLongitude = 120.6,
            DistanceMeters = 5_000, DurationSeconds = 1_200, FarePesos = fare
        }]
    };

    private static JeepneyAccessSegment Access() => new()
    {
        Mode = AccessMode.Walk
    };
}
