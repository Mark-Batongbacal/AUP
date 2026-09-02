using backend.Models.Database;
using backend.Models.Destinations;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Assistant;
using backend.Services.Destinations;
using backend.Services.Routing;
using backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace backend.Tests.Services.Assistant;

public sealed class TukiAssistantServiceTests
{
    private readonly Mock<IAssistantIntentExtractor> _extractor = new();
    private readonly Mock<IDestinationSearchService> _destinations = new();
    private readonly Mock<IAssistantPlaceResolver> _assistantPlaces = new();
    private readonly Mock<IRoutingService> _routing = new();
    private readonly Mock<ITripSessionRepository> _sessions = new();
    private readonly Mock<INavigationInstructionRepository> _instructions = new();
    private readonly Mock<IJourneyPlanPersistenceService> _persistence = new();
    private readonly Mock<IChatService> _chat = new();

    [Fact]
    public async Task Planning_AmbiguousDestination_RequestsClarificationWithoutRouting()
    {
        PlanIntent("SM");
        _assistantPlaces.Setup(service => service.SearchAsync("SM",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([Place("1", "SM Clark"), Place("2", "SM Pampanga")]);

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
        _assistantPlaces.Setup(service => service.SearchAsync("SM City Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([Place("mall", "SM City Clark")]);
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
        _routing.Verify(service => service.PlanTripsAsync(
            15.1, 120.5, 15.2, 120.6, default), Times.Once);
    }

    [Fact]
    public async Task Planning_DestinationSelection_UsesStoredCandidateWithoutAnotherAiOrSearch()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation(userId);
        SetupConversation(conversation);
        PlanIntent("SM Clark", 30);
        _assistantPlaces.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([
                Place("mall", "SM City Clark", 15.2, 120.6),
                Place("bus", "SM City Clark Bus Station", 15.3, 120.7)
            ]);
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.3, 120.7,
                It.Is<JourneyPlanningPreferences>(preferences =>
                    preferences.MaxFarePesos == 30), default))
            .ReturnsAsync([Plan(26)]);
        _persistence.Setup(item => item.PersistAsync(
                userId, 15.1, 120.5, "SM City Clark Bus Station", 15.3, 120.7,
                30, null, It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = Guid.NewGuid() }, Plan(26))]);

        var ambiguous = await Service(chat: _chat.Object).RespondPlanningAsync(
            userId, new("Take me to SM Clark", 15.1, 120.5,
                ConversationId: conversation.ConversationId));
        var selected = ambiguous.Destinations!.Single(item =>
            item.Name == "SM City Clark Bus Station");

        var result = await Service(chat: _chat.Object).RespondPlanningAsync(
            userId, new(null,
                ConversationId: conversation.ConversationId,
                DestinationSelectionToken: ambiguous.DestinationSelectionToken,
                SelectedDestinationCandidateId: selected.CandidateId));

        Assert.Equal("JOURNEYS_AVAILABLE", result.Status);
        Assert.Equal(30, result.Action!.BudgetPesos);
        _extractor.Verify(item => item.ExtractAsync(
            It.IsAny<AssistantContext>(), default), Times.Once);
        _assistantPlaces.Verify(item => item.SearchAsync(
            "SM Clark", It.IsAny<DestinationSearchContext>(), default), Times.Once);
        _routing.Verify(service => service.PlanTripsAsync(
            15.1, 120.5, 15.3, 120.7,
            It.IsAny<JourneyPlanningPreferences>(), default), Times.Once);
    }

    [Fact]
    public async Task Planning_StateSurvivesDestinationSelectionAndFollowUps()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation(userId);
        SetupConversation(conversation);
        _extractor.SetupSequence(item => item.ExtractAsync(
                It.IsAny<AssistantContext>(), default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.PlanRoute,
                DestinationQuery = "SM Clark",
                BudgetPesos = 30
            })
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.UpdateTripConstraints,
                WalkingPreference = AssistantWalkingPreference.More
            })
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.UpdateTripConstraints,
                BudgetPesos = 40
            });
        _assistantPlaces.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([
                Place("sm", "SM City Clark"),
                Place("station", "SM City Clark Bus Station", 15.3, 120.7)
            ]);
        var routedPreferences = new List<JourneyPlanningPreferences>();
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.2, 120.6,
                It.IsAny<JourneyPlanningPreferences>(), default))
            .Callback<double, double, double, double, JourneyPlanningPreferences?, CancellationToken>(
                (_, _, _, _, preferences, _) => routedPreferences.Add(preferences!))
            .ReturnsAsync([Plan(26)]);
        _persistence.Setup(item => item.PersistAsync(
                It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<decimal?>(), It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = Guid.NewGuid() }, Plan(26))]);
        var service = Service(chat: _chat.Object);

        var first = await service.RespondPlanningAsync(
            userId, new("SM Clark, ₱30 budget", 15.1, 120.5,
                ConversationId: conversation.ConversationId));
        Assert.Equal("DESTINATION_AMBIGUOUS", first.Status);
        var selected = await service.RespondPlanningAsync(
            userId, new(null,
                ConversationId: conversation.ConversationId,
                DestinationSelectionToken: first.DestinationSelectionToken,
                SelectedDestinationCandidateId: first.Destinations![0].CandidateId));

        var walking = await service.RespondPlanningAsync(
            userId, new("Okay lang mas madaming lakad",
                ConversationId: conversation.ConversationId));
        var budget = await service.RespondPlanningAsync(
            userId, new("Actually ₱40 max",
                ConversationId: conversation.ConversationId));

        Assert.Equal("JOURNEYS_AVAILABLE", selected.Status);
        Assert.Equal("JOURNEYS_AVAILABLE", walking.Status);
        Assert.Equal("JOURNEYS_AVAILABLE", budget.Status);
        Assert.Collection(routedPreferences,
            initial =>
            {
                Assert.Equal(30, initial.MaxFarePesos);
                Assert.Equal(JourneyWalkingPreference.Normal, initial.WalkingPreference);
            },
            moreWalking =>
            {
                Assert.Equal(30, moreWalking.MaxFarePesos);
                Assert.Equal(JourneyWalkingPreference.More, moreWalking.WalkingPreference);
            },
            updatedBudget =>
            {
                Assert.Equal(40, updatedBudget.MaxFarePesos);
                Assert.Equal(JourneyWalkingPreference.More, updatedBudget.WalkingPreference);
            });
        _assistantPlaces.Verify(service => service.SearchAsync(
            "SM Clark", It.IsAny<DestinationSearchContext>(), default), Times.Once);
    }

    [Fact]
    public async Task Planning_BudgetFiltering_IsDeterministic()
    {
        PlanIntent("SM Clark", 80);
        _assistantPlaces.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([Place("1", "SM Clark")]);
        _routing.Setup(service => service.PlanTripsAsync(15.1, 120.5, 15.2, 120.6,
                It.Is<JourneyPlanningPreferences>(preferences => preferences.MaxFarePesos == 80), default))
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

    [Theory]
    [InlineData("I'm kinda broke", "cheapest", null, null, AssistantWalkingPreference.Normal)]
    [InlineData("I only have ₱30", null, 30d, null, AssistantWalkingPreference.Normal)]
    [InlineData("I can walk up to 2km", null, null, 2000d, AssistantWalkingPreference.Normal)]
    [InlineData("Okay lang mas madaming lakad", null, null, null, AssistantWalkingPreference.More)]
    [InlineData("Pagod ako", null, null, null, AssistantWalkingPreference.Less)]
    public async Task Planning_IntentConstraints_ArePassedToRoutingBeforeSelection(
        string message,
        string? preference,
        double? budget,
        double? maxWalking,
        AssistantWalkingPreference walkingPreference)
    {
        _extractor.Setup(item => item.ExtractAsync(
                It.IsAny<AssistantContext>(), default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.PlanRoute,
                DestinationQuery = "SM Clark",
                Preference = preference,
                BudgetPesos = budget is null ? null : (decimal)budget.Value,
                MaxWalkingMeters = maxWalking,
                WalkingPreference = walkingPreference
            });
        _assistantPlaces.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([Place("sm", "SM Clark")]);
        JourneyPlanningPreferences? captured = null;
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.2, 120.6,
                It.IsAny<JourneyPlanningPreferences>(), default))
            .Callback<double, double, double, double, JourneyPlanningPreferences?, CancellationToken>(
                (_, _, _, _, preferences, _) => captured = preferences)
            .ReturnsAsync([Plan(26)]);
        _persistence.Setup(item => item.PersistAsync(
                It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<decimal?>(), It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = Guid.NewGuid() }, Plan(26))]);

        var result = await Service().RespondPlanningAsync(
            Guid.NewGuid(), new(message, 15.1, 120.5));

        Assert.Equal("JOURNEYS_AVAILABLE", result.Status);
        Assert.NotNull(captured);
        Assert.Equal(budget is null ? null : (decimal?)budget.Value, captured!.MaxFarePesos);
        Assert.Equal(maxWalking, captured.MaxWalkingMeters);
        Assert.Equal(walkingPreference switch
        {
            AssistantWalkingPreference.Less => JourneyWalkingPreference.Less,
            AssistantWalkingPreference.More => JourneyWalkingPreference.More,
            _ => JourneyWalkingPreference.Normal
        }, captured.WalkingPreference);
        Assert.Equal(preference?.ToLowerInvariant() switch
        {
            "cheapest" => JourneyOptimizationPreference.Cheapest,
            _ => null
        }, captured.OptimizationPreference);
    }

    [Fact]
    public async Task Planning_NoTricycle_AvoidanceReachesRoutingBeforeFinalSelection()
    {
        _extractor.Setup(item => item.ExtractAsync(
                It.IsAny<AssistantContext>(), default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.PlanRoute,
                DestinationQuery = "SM Clark",
                AvoidTransportModes = ["TRICYCLE"]
            });
        _assistantPlaces.Setup(service => service.SearchAsync("SM Clark",
                It.IsAny<DestinationSearchContext>(), default))
            .ReturnsAsync([Place("sm", "SM Clark")]);
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.2, 120.6,
                It.Is<JourneyPlanningPreferences>(preferences =>
                    preferences.AvoidTransportModes!.Contains(AccessMode.Trike)), default))
            .ReturnsAsync([Plan(26)]);
        _persistence.Setup(item => item.PersistAsync(
                It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<decimal?>(), It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<JeepneyTripPlan>>(), default))
            .ReturnsAsync([new PersistedJourney(
                new RouteRecommendation { RecommendationId = Guid.NewGuid() }, Plan(26))]);

        await Service().RespondPlanningAsync(
            Guid.NewGuid(), new("No tricycle to SM Clark", 15.1, 120.5));

        _routing.VerifyAll();
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
        session.LastAccuracyMeters = 20;
        session.LastLocationAt = DateTime.UtcNow;
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
                    context.ActiveTrip.TripSessionId == sessionId &&
                    context.ActiveTrip.LocationReliability == AssistantLocationPolicy.Current &&
                    context.ActiveTrip.CanUseLocationForReroute),
                default))
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.UpdateTripConstraints,
                BudgetPesos = 30
            });
        _routing.Setup(service => service.PlanTripsAsync(
                15.1, 120.5, 15.2, 120.6,
                It.Is<JourneyPlanningPreferences>(preferences =>
                    preferences.MaxFarePesos == 30), default))
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
    public async Task ActiveTrip_StaleGps_RemainsInAiContextButCannotStartReplan()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = ActiveSession(
            userId, sessionId, TripNavigationState.OnJeepney, "ON_ROUTE");
        session.LastLatitude = 15.1;
        session.LastLongitude = 120.5;
        session.LastAccuracyMeters = 20;
        session.LastLocationAt = DateTime.UtcNow.AddSeconds(-90);

        _sessions.Setup(item => item.GetOwnedAsync(sessionId, userId, default))
            .ReturnsAsync(session);
        _instructions.Setup(item => item.GetForOwnedSessionAsync(sessionId, userId, default))
            .ReturnsAsync([]);

        AssistantContext? capturedContext = null;
        _extractor.Setup(item => item.ExtractAsync(
                It.IsAny<AssistantContext>(), default))
            .Callback<AssistantContext, CancellationToken>((context, _) => capturedContext = context)
            .ReturnsAsync(new AssistantIntent
            {
                Intent = AssistantIntentType.UpdateTripConstraints,
                Preference = "cheapest"
            });

        var result = await Service().RespondActiveTripAsync(
            userId, sessionId, new("Find me a cheaper route"));

        Assert.Equal("NO_RELIABLE_LOCATION", result.Status);
        Assert.NotNull(capturedContext?.ActiveTrip);
        Assert.Equal(15.1, capturedContext!.ActiveTrip!.LastLatitude);
        Assert.Equal(120.5, capturedContext.ActiveTrip.LastLongitude);
        Assert.Equal(AssistantLocationPolicy.Stale, capturedContext.ActiveTrip.LocationReliability);
        Assert.False(capturedContext.ActiveTrip.CanUseLocationForReroute);
        Assert.InRange(capturedContext.ActiveTrip.LocationAgeSeconds ?? 0, 89, 100);
        _routing.Verify(service => service.PlanTripsAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<JourneyPlanningPreferences>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private TukiAssistantService Service(IChatService? chat = null) => new(
        _extractor.Object, _destinations.Object, _routing.Object,
        _sessions.Object, _instructions.Object,
        _persistence.Object,
        NullLogger<TukiAssistantService>.Instance,
        chat: chat,
        assistantPlaces: _assistantPlaces.Object);

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

    private static DestinationSearchResult Place(
        string id,
        string name,
        double latitude = 15.2,
        double longitude = 120.6) =>
        new(id, name, latitude, longitude, "venue", "test");

    private static ChatConversation Conversation(Guid userId) => new()
    {
        ConversationId = Guid.NewGuid(),
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void SetupConversation(ChatConversation conversation)
    {
        _chat.Setup(service => service.GetConversationByIdAsync(
                conversation.ConversationId, default))
            .ReturnsAsync(conversation);
        _chat.Setup(service => service.GetMessagesAsync(conversation.ConversationId, default))
            .ReturnsAsync([]);
        _chat.Setup(service => service.UpdatePlanningStateAsync(
                conversation.ConversationId, It.IsAny<string>(), default))
            .Callback<Guid, string?, CancellationToken>((_, json, _) =>
                conversation.PlanningStateJson = json)
            .ReturnsAsync(true);
    }

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
