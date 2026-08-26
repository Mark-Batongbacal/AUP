using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class ReroutingServiceTests
{
    [Fact]
    public void OnboardRecovery_RejectsBackwardSameRouteBoarding()
    {
        var context = new OnboardTransitPlanningContext("X", 5_000, 75);
        var plan = SameRoutePlan(boardProgress: 3_500);

        Assert.False(ReroutingService.IsValidForOnboardRecovery(plan, context));
    }

    [Fact]
    public void OnboardRecovery_AllowsForwardSameRouteOccurrence()
    {
        var context = new OnboardTransitPlanningContext("X", 5_000, 75);
        var plan = SameRoutePlan(boardProgress: 5_100);

        Assert.True(ReroutingService.IsValidForOnboardRecovery(plan, context));
    }

    [Fact]
    public void OnboardContinuation_HasNoDuplicateBaseFare()
    {
        var plan = SameRoutePlan(boardProgress: 5_000, startsAlreadyOnboard: true);

        Assert.Equal(0, plan.Legs[0].FarePesos);
        Assert.Equal(0, plan.TotalFarePesos);
    }

    [Fact]
    public async Task MissedAlight_PassesOnboardContext_RecordsOldFareOnceAndResumesOnVehicle()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var searches = new Mock<ITripSearchRepository>();
        var recommendations = new Mock<IRouteRecommendationRepository>();
        var recommendationLegs = new Mock<IRecommendationLegRepository>();
        var modes = new Mock<ITransportModeRepository>();
        var routes = new Mock<ITransportRouteRepository>();
        var instructionService = new Mock<INavigationInstructionService>();
        var landmarkPrefetch = new Mock<ILandmarkCorridorPrefetchService>();
        var routing = new Mock<IRoutingService>();
        var route = new TransportRoute
        {
            RouteId = 10,
            RouteCode = "X",
            RouteName = "Route X"
        };
        var jeepneyMode = new TransportMode
        {
            TransportModeId = 2,
            Code = "JEEPNEY",
            Name = "Jeepney"
        };
        var oldRecommendationId = Guid.NewGuid();
        var newRecommendationId = Guid.NewGuid();
        var session = new TripSession
        {
            TripSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RecommendationId = oldRecommendationId,
            CurrentNavigationState = TripNavigationState.ApproachingAlightPoint,
            CurrentRouteProgressMeters = 5_000,
            LastNavigationStatus = "MISSED_ALIGHT",
            LastLatitude = 15.2,
            LastLongitude = 120.6,
            LastAccuracyMeters = 8,
            LastLocationAt = DateTime.UtcNow.AddSeconds(-5),
            DestinationLatitude = 15.3,
            DestinationLongitude = 120.7,
            DestinationName = "Destination"
        };
        var oldLeg = new RecommendationLeg
        {
            LegId = Guid.NewGuid(),
            LegOrder = 0,
            RouteId = route.RouteId,
            Route = route,
            TransportMode = jeepneyMode,
            EstimatedFare = 13
        };
        var plan = SameRoutePlan(5_000, startsAlreadyOnboard: true);
        plan.Legs[0] = new JeepneyTripLeg
        {
            Mode = AccessMode.Jeepney,
            RouteId = "X",
            RouteName = "Route X",
            OriginLatitude = 15.2,
            OriginLongitude = 120.6,
            DestinationLatitude = 15.25,
            DestinationLongitude = 120.65,
            BoardRouteProgressMeters = 5_000,
            AlightRouteProgressMeters = 6_000,
            StartsAlreadyOnboard = true,
            DistanceMeters = 1_000,
            DurationSeconds = 180,
            FarePesos = 0
        };
        var persistedLegs = new List<RecommendationLeg>();

        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        recommendations.Setup(item => item.GetOrderedLegsAsync(oldRecommendationId, default))
            .ReturnsAsync([oldLeg]);
        recommendations.Setup(item => item.GetOrderedLegsAsync(newRecommendationId, default))
            .ReturnsAsync(() => persistedLegs);
        recommendations.Setup(item => item.AddAsync(It.IsAny<RouteRecommendation>(), default))
            .ReturnsAsync((RouteRecommendation recommendation, CancellationToken _) =>
            {
                recommendation.RecommendationId = newRecommendationId;
                return recommendation;
            });
        searches.Setup(item => item.AddAsync(It.IsAny<TripSearch>(), default))
            .ReturnsAsync((TripSearch search, CancellationToken _) =>
            {
                search.TripSearchId = Guid.NewGuid();
                return search;
            });
        recommendationLegs.Setup(item => item.AddAsync(It.IsAny<RecommendationLeg>(), default))
            .ReturnsAsync((RecommendationLeg leg, CancellationToken _) =>
            {
                leg.TransportMode = jeepneyMode;
                leg.Route = route;
                persistedLegs.Add(leg);
                return leg;
            });
        modes.Setup(item => item.GetByCodeAsync("JEEPNEY", default)).ReturnsAsync(jeepneyMode);
        routes.Setup(item => item.GetByRouteCodeAsync("X", default)).ReturnsAsync(route);
        instructionService.Setup(item => item.GenerateAsync(session, default)).ReturnsAsync([]);
        landmarkPrefetch.Setup(item => item.PrefetchAsync(session, default)).Returns(Task.CompletedTask);
        OnboardTransitPlanningContext? receivedContext = null;
        routing.Setup(item => item.PlanTripsAsync(
                15.2, 120.6, session.DestinationLatitude, session.DestinationLongitude,
                It.IsAny<JourneyPlanningPreferences>(), default))
            .Callback((double _, double _, double _, double _, JourneyPlanningPreferences preferences,
                CancellationToken _) => receivedContext = preferences.OnboardTransit)
            .ReturnsAsync([plan]);
        var options = Options.Create(new NavigationOptions { RerouteCooldownSeconds = 120 });
        var service = new ReroutingService(
            sessions.Object, routing.Object, searches.Object, recommendations.Object,
            recommendationLegs.Object, modes.Object, routes.Object,
            instructionService.Object, landmarkPrefetch.Object,
            new backend.Services.TripSessions.TripSessionStateMachine(),
            new GpsQualityValidator(options), options);

        var request = new NavigationRerouteRequest(
            Reason: "MISSED_ALIGHT",
            Latitude: 15.2,
            Longitude: 120.6,
            AccuracyMeters: 8,
            Timestamp: DateTime.UtcNow);
        var first = await service.RerouteAsync(
            session.UserId, session.TripSessionId, request);
        var second = await service.RerouteAsync(
            session.UserId, session.TripSessionId, request);

        Assert.True(first.Succeeded);
        Assert.Equal("X", receivedContext?.RouteId);
        Assert.Equal(5_000, receivedContext?.CurrentRouteProgressMeters);
        Assert.Equal(13, session.ApproxFareSpent);
        Assert.Equal(TripNavigationState.OnJeepney, session.CurrentNavigationState);
        Assert.True(Assert.Single(persistedLegs).StartsAlreadyOnboard);
        Assert.Equal(0, persistedLegs[0].EstimatedFare);
        Assert.Equal("REROUTE_COOLDOWN", second.Status);
        Assert.Equal(13, session.ApproxFareSpent);
    }
    [Fact]
    public async Task FreshGps_OverridesStaleSessionLocation_AndPersistsSuccessfulReroute()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var searches = new Mock<ITripSearchRepository>();
        var recommendations = new Mock<IRouteRecommendationRepository>();
        var recommendationLegs = new Mock<IRecommendationLegRepository>();
        var modes = new Mock<ITransportModeRepository>();
        var instructionService = new Mock<INavigationInstructionService>();
        var landmarkPrefetch = new Mock<ILandmarkCorridorPrefetchService>();
        var session = OffRouteSession();
        var oldRecommendationId = Guid.NewGuid();
        session.RecommendationId = oldRecommendationId;
        session.CurrentLegIndex = 3;
        session.CurrentProgressMeters = 825;
        session.CurrentRouteProgressMeters = 1_250;
        session.ConsecutiveOffRouteSamples = 4;
        session.OffRouteSuspectedAt = DateTime.UtcNow.AddMinutes(-2);
        session.LastLocationAt = DateTime.UtcNow.AddHours(-1);
        var currentFixAt = DateTime.UtcNow;
        const double newLatitude = 15.25;
        const double newLongitude = 120.75;
        var newRecommendationId = Guid.NewGuid();
        var searchId = Guid.NewGuid();
        var walkingMode = new TransportMode { TransportModeId = 1, Code = "WALK", Name = "Walk" };
        var persistedLeg = new RecommendationLeg
        {
            LegOrder = 0,
            TransportMode = walkingMode,
            StartLatitude = newLatitude,
            StartLongitude = newLongitude,
            EndLatitude = session.DestinationLatitude,
            EndLongitude = session.DestinationLongitude
        };

        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        searches.Setup(item => item.AddAsync(It.IsAny<TripSearch>(), default))
            .ReturnsAsync((TripSearch search, CancellationToken _) =>
            {
                search.TripSearchId = searchId;
                return search;
            });
        recommendations.Setup(item => item.AddAsync(It.IsAny<RouteRecommendation>(), default))
            .ReturnsAsync((RouteRecommendation recommendation, CancellationToken _) =>
            {
                recommendation.RecommendationId = newRecommendationId;
                return recommendation;
            });
        recommendations.Setup(item => item.GetOrderedLegsAsync(newRecommendationId, default))
            .ReturnsAsync([persistedLeg]);
        recommendationLegs.Setup(item => item.AddAsync(It.IsAny<RecommendationLeg>(), default))
            .ReturnsAsync((RecommendationLeg leg, CancellationToken _) => leg);
        modes.Setup(item => item.GetByCodeAsync("WALK", default)).ReturnsAsync(walkingMode);
        instructionService.Setup(item => item.GenerateAsync(session, default))
            .ReturnsAsync([]);
        landmarkPrefetch.Setup(item => item.PrefetchAsync(session, default))
            .Returns(Task.CompletedTask);

        var routing = new Mock<IRoutingService>();
        routing.Setup(item => item.PlanTripsAsync(newLatitude, newLongitude,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([WalkOnlyPlan(newLatitude, newLongitude,
                session.DestinationLatitude, session.DestinationLongitude)]);
        var options = Options.Create(new NavigationOptions { RerouteCooldownSeconds = 120 });
        var service = new ReroutingService(
            sessions.Object, routing.Object, searches.Object, recommendations.Object,
            recommendationLegs.Object, modes.Object, Mock.Of<ITransportRouteRepository>(),
            instructionService.Object, landmarkPrefetch.Object,
            new backend.Services.TripSessions.TripSessionStateMachine(),
            new GpsQualityValidator(options), options);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest(
                Reason: "OFF_ROUTE",
                Latitude: newLatitude,
                Longitude: newLongitude,
                AccuracyMeters: 8,
                Timestamp: currentFixAt,
                SpeedMetersPerSecond: 2,
                BearingDegrees: 90));

        Assert.True(result.Succeeded);
        Assert.Equal("REROUTE_SUCCEEDED", result.Status);
        Assert.Equal(newRecommendationId, session.RecommendationId);
        Assert.Equal(newLatitude, session.LastLatitude);
        Assert.Equal(newLongitude, session.LastLongitude);
        Assert.Equal(8, session.LastAccuracyMeters);
        Assert.Equal(currentFixAt, session.LastLocationAt);
        Assert.Equal(0, session.CurrentLegIndex);
        Assert.Equal(0, session.CurrentProgressMeters);
        Assert.Null(session.CurrentRouteProgressMeters);
        Assert.Equal(0, session.ConsecutiveOffRouteSamples);
        Assert.Null(session.OffRouteSuspectedAt);
        Assert.Equal(1, session.RerouteCount);
        Assert.Equal("OFF_ROUTE", session.LastRerouteReason);
        Assert.NotNull(session.LastRerouteAt);
        Assert.Equal(TripNavigationState.WalkingToDestination, session.CurrentNavigationState);
        routing.Verify(item => item.PlanTripsAsync(newLatitude, newLongitude,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
        searches.Verify(item => item.AddAsync(It.Is<TripSearch>(search =>
            search.OriginLatitude == newLatitude && search.OriginLongitude == newLongitude), default), Times.Once);
        instructionService.Verify(item => item.GenerateAsync(session, default), Times.Once);
        landmarkPrefetch.Verify(item => item.PrefetchAsync(session, default), Times.Once);
    }

    [Fact]
    public async Task ValidOffRouteGps_DoesNotRequireMatchingTheOldRoute()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.LastLocationAt = DateTime.UtcNow.AddDays(-1);
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        const double farLatitude = 14.5;
        const double farLongitude = 121.1;
        routing.Setup(item => item.PlanTripsAsync(farLatitude, farLongitude,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([]);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest(
                Reason: "MANUAL",
                Latitude: farLatitude,
                Longitude: farLongitude,
                AccuracyMeters: 10,
                Timestamp: DateTime.UtcNow));

        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(farLatitude, farLongitude,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
        Assert.Equal(farLatitude, session.LastLatitude);
        Assert.Equal(farLongitude, session.LastLongitude);
    }

    [Theory]
    [InlineData(91, 120.5, 10, "INVALID_LOCATION")]
    [InlineData(15.1, -181, 10, "INVALID_LOCATION")]
    [InlineData(15.1, 120.5, 100, "POOR_ACCURACY")]
    public async Task InvalidOrUnreliableSuppliedGps_IsRejected(
        double latitude, double longitude, double accuracy, string expectedStatus)
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest(
                Latitude: latitude,
                Longitude: longitude,
                AccuracyMeters: accuracy,
                Timestamp: DateTime.UtcNow));

        Assert.Equal(expectedStatus, result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingSuppliedGps_FallsBackToSessionLocation()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        routing.Setup(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([]);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("PREFERENCE_CHANGED", "fastest"));

        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
    }

    [Theory]
    [InlineData("OFF_ROUTE")]
    [InlineData("MISSED_ALIGHT")]
    [InlineData("MISSED_LEG_TARGET")]
    public async Task Cooldown_PreventsAutomaticReroutingLoop(string reason)
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.LastRerouteAt = DateTime.UtcNow;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest(reason));
        Assert.Equal("REROUTE_COOLDOWN", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManualReroute_IgnoresAutomaticRecoveryCooldown()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        session.LastRerouteAt = DateTime.UtcNow;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        routing.Setup(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([]);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("MANUAL"));

        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
    }

    [Fact]
    public async Task NoReplacementRoute_PreservesOffRouteSessionAndConstraints()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        routing.Setup(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([]);
        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("WRONG_JEEP"));
        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        Assert.Equal(TripNavigationState.OffRoute, session.CurrentNavigationState);
        Assert.Equal(80, session.OriginalBudget);
        Assert.Equal("cheapest", session.OriginalPreference);
    }

    [Fact]
    public async Task ManualReroute_FromActiveTrip_ReachesReliableLocationValidation()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        session.LastLatitude = null;
        session.LastLongitude = null;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("MANUAL"));

        Assert.Equal("NO_RELIABLE_LOCATION", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("balanced")]
    [InlineData("efficient")]
    [InlineData("fastest")]
    [InlineData("cheapest")]
    public async Task SupportedPreferences_ReachRoutePlanning(string preference)
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        routing.Setup(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([]);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("PREFERENCE_CHANGED", preference));

        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
    }

    [Fact]
    public async Task InvalidPreference_IsRejectedBeforeRoutePlanning()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("PREFERENCE_CHANGED", "random"));

        Assert.Equal("INVALID_PREFERENCE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidBudget_IsRejectedBeforeRoutePlanning()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("BUDGET_CHANGED", Budget: 0));

        Assert.Equal("INVALID_BUDGET", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidAvoidTransportMode_IsRejectedBeforeRoutePlanning()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("TRANSPORT_UNAVAILABLE", AvoidTransportMode: "AIRPLANE"));

        Assert.Equal("INVALID_AVOID_TRANSPORT_MODE", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnavailableToda_FiltersTricycleOnlyReplacement()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        routing.Setup(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
                session.DestinationLatitude, session.DestinationLongitude, default))
            .ReturnsAsync([TricycleOnlyPlan()]);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest(
                Reason: "TRANSPORT_UNAVAILABLE",
                AvoidTransportMode: "TRICYCLE"));

        Assert.Equal("NO_REROUTE_AVAILABLE", result.Status);
        Assert.Equal(TripNavigationState.OnJeepney, session.CurrentNavigationState);
        routing.Verify(item => item.PlanTripsAsync(session.LastLatitude!.Value, session.LastLongitude!.Value,
            session.DestinationLatitude, session.DestinationLongitude, default), Times.Once);
    }

    [Fact]
    public async Task PartialDestination_IsRejectedBeforeRoutePlanning()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.CurrentNavigationState = TripNavigationState.OnJeepney;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);

        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("DESTINATION_CHANGED", DestinationName: "SM Clark"));

        Assert.Equal("INVALID_DESTINATION", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ReroutingService Service, Mock<IRoutingService> Routing) Create(
        Mock<ITripSessionRepository> sessions)
    {
        var routing = new Mock<IRoutingService>();
        var options = Options.Create(new NavigationOptions { RerouteCooldownSeconds = 120 });
        return (new ReroutingService(sessions.Object, routing.Object,
            Mock.Of<ITripSearchRepository>(), Mock.Of<IRouteRecommendationRepository>(),
            Mock.Of<IRecommendationLegRepository>(), Mock.Of<ITransportModeRepository>(),
            Mock.Of<ITransportRouteRepository>(), Mock.Of<INavigationInstructionService>(),
            Mock.Of<ILandmarkCorridorPrefetchService>(),
            new backend.Services.TripSessions.TripSessionStateMachine(),
            new GpsQualityValidator(options), options), routing);
    }

    private static JeepneyTripPlan WalkOnlyPlan(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude) => new()
    {
        OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        Legs =
        [
            new JeepneyTripLeg
            {
                Mode = AccessMode.Walk,
                OriginLatitude = originLatitude,
                OriginLongitude = originLongitude,
                DestinationLatitude = destinationLatitude,
                DestinationLongitude = destinationLongitude,
                DistanceMeters = 1_000,
                DurationSeconds = 720,
                FarePesos = 0
            }
        ],
        TotalTimeSeconds = 720,
        TotalFarePesos = 0,
        GeneralizedCostPesos = 12
    };

    private static JeepneyTripPlan TricycleOnlyPlan() => new()
    {
        OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        Legs =
        [
            new JeepneyTripLeg
            {
                Mode = AccessMode.Trike,
                DistanceMeters = 1_000,
                DurationSeconds = 300,
                FarePesos = 35
            }
        ],
        TotalTimeSeconds = 300,
        TotalFarePesos = 35,
        GeneralizedCostPesos = 35
    };

    private static JeepneyTripPlan SameRoutePlan(
        double boardProgress,
        bool startsAlreadyOnboard = false) => new()
    {
        OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
        Legs =
        [
            new JeepneyTripLeg
            {
                Mode = AccessMode.Jeepney,
                RouteId = "X",
                BoardRouteProgressMeters = boardProgress,
                AlightRouteProgressMeters = boardProgress + 1_000,
                StartsAlreadyOnboard = startsAlreadyOnboard,
                FarePesos = startsAlreadyOnboard ? 0 : 13
            }
        ],
        TotalFarePesos = startsAlreadyOnboard ? 0 : 13
    };

    private static TripSession OffRouteSession() => new()
    {
        TripSessionId = Guid.NewGuid(), UserId = Guid.NewGuid(),
        CurrentNavigationState = TripNavigationState.OffRoute,
        LastLatitude = 15.1, LastLongitude = 120.5,
        DestinationLatitude = 15.2, DestinationLongitude = 120.6,
        DestinationName = "Existing destination",
        OriginalBudget = 80, OriginalPreference = "cheapest"
    };
}
