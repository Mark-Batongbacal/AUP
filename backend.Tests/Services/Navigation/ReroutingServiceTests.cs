using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class ReroutingServiceTests
{
    [Fact]
    public async Task Cooldown_PreventsReroutingLoop()
    {
        var sessions = new Mock<ITripSessionRepository>();
        var session = OffRouteSession();
        session.LastRerouteAt = DateTime.UtcNow;
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default)).ReturnsAsync(session);
        var (service, routing) = Create(sessions);
        var result = await service.RerouteAsync(session.UserId, session.TripSessionId,
            new NavigationRerouteRequest("OFF_ROUTE"));
        Assert.Equal("REROUTE_COOLDOWN", result.Status);
        routing.Verify(item => item.PlanTripsAsync(It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static (ReroutingService Service, Mock<IRoutingService> Routing) Create(
        Mock<ITripSessionRepository> sessions)
    {
        var routing = new Mock<IRoutingService>();
        return (new ReroutingService(sessions.Object, routing.Object,
            Mock.Of<ITripSearchRepository>(), Mock.Of<IRouteRecommendationRepository>(),
            Mock.Of<IRecommendationLegRepository>(), Mock.Of<ITransportModeRepository>(),
            Mock.Of<ITransportRouteRepository>(), Mock.Of<INavigationInstructionService>(),
            Mock.Of<ILandmarkCorridorPrefetchService>(),
            new backend.Services.TripSessions.TripSessionStateMachine(),
            Options.Create(new NavigationOptions { RerouteCooldownSeconds = 120 })), routing);
    }

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
