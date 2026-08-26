using backend.Models.Database;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.Routing;
using backend.Services.TripSessions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class LocationTrackingRecoveryTests
{
    [Fact]
    public async Task MatchedBeyondAlight_ReturnsMissedAlightAndSavesAuthoritativeProgress()
    {
        var fixture = Fixture();
        fixture.Session.CurrentNavigationState = TripNavigationState.ApproachingAlightPoint;
        fixture.Matcher.SetupSequence(item => item.Match(
                It.IsAny<LocationUpdate>(), It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double?>()))
            .Returns((RouteMatch?)null)
            .Returns(new RouteMatch(15, 120.012, 5, 1_200, 1_200, 1, 0.2));

        var result = await fixture.Service.ProcessAsync(
            fixture.Session.UserId,
            fixture.Session.TripSessionId,
            new LocationUpdate(15, 120.012, 5, DateTime.UtcNow));

        Assert.True(result.Accepted);
        Assert.Equal("MISSED_ALIGHT", result.Status);
        Assert.Equal(1_200, fixture.Session.CurrentRouteProgressMeters);
        Assert.Equal(1_200, fixture.Session.CurrentProgressMeters);
        Assert.Equal(TripNavigationState.ApproachingAlightPoint,
            fixture.Session.CurrentNavigationState);
    }

    [Fact]
    public async Task UnmatchedAfterApproach_IsAmbiguousAndDoesNotInferAlightingOrMissedStop()
    {
        var fixture = Fixture();
        fixture.Session.CurrentNavigationState = TripNavigationState.ApproachingAlightPoint;
        fixture.OffRoute.Setup(item => item.Evaluate(
                fixture.Session, fixture.Leg, It.IsAny<double>(), 5, It.IsAny<DateTime>()))
            .Returns(OffRouteStatus.Confirmed);
        fixture.Matcher.Setup(item => item.Match(
                It.IsAny<LocationUpdate>(), It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double?>()))
            .Returns((RouteMatch?)null);

        var result = await fixture.Service.ProcessAsync(
            fixture.Session.UserId,
            fixture.Session.TripSessionId,
            new LocationUpdate(15.01, 120.02, 5, DateTime.UtcNow));

        Assert.True(result.Accepted);
        Assert.Equal("ALIGHT_STATUS_UNKNOWN", result.Status);
        Assert.Equal("ALIGHT_STATUS_UNKNOWN", fixture.Session.LastNavigationStatus);
        Assert.Equal(0, fixture.Session.CurrentLegIndex);
        Assert.Equal(0, fixture.Session.ApproxFareSpent);
        Assert.Equal(TripNavigationState.ApproachingAlightPoint,
            fixture.Session.CurrentNavigationState);
    }

    [Fact]
    public async Task StaleServerProgress_NearLegEnd_ReacquiresAndEnablesAlightConfirmationState()
    {
        var fixture = Fixture();
        fixture.Session.CurrentRouteProgressMeters = 0;
        fixture.Matcher.Setup(item => item.Match(
                It.IsAny<LocationUpdate>(), It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double?>()))
            .Returns((RouteMatch?)null);
        fixture.Matcher.Setup(item => item.MatchWithinRange(
                It.IsAny<LocationUpdate>(), It.IsAny<IReadOnlyList<(double, double)>>(),
                0, 600, 1_000))
            .Returns(new RouteMatch(15, 120.0095, 5, 950, 950, 0, 0.95));

        var result = await fixture.Service.ProcessAsync(
            fixture.Session.UserId,
            fixture.Session.TripSessionId,
            new LocationUpdate(15, 120.0095, 5, DateTime.UtcNow));

        Assert.True(result.Accepted);
        Assert.Equal("ApproachingAlightPoint", result.Status);
        Assert.Equal(950, fixture.Session.CurrentProgressMeters);
        Assert.Equal(950, fixture.Session.CurrentRouteProgressMeters);
        Assert.Equal(TripNavigationState.ApproachingAlightPoint,
            fixture.Session.CurrentNavigationState);
        fixture.Matcher.Verify(item => item.MatchWithinRange(
            It.IsAny<LocationUpdate>(), It.IsAny<IReadOnlyList<(double, double)>>(),
            0, 600, 1_000), Times.Once);
    }

    private static RecoveryFixture Fixture()
    {
        var session = new TripSession
        {
            TripSessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RecommendationId = Guid.NewGuid(),
            CurrentNavigationState = TripNavigationState.OnJeepney,
            CurrentRouteProgressMeters = 900
        };
        var route = new TransportRoute { RouteId = 10, RouteCode = "X", RouteName = "Route X" };
        var leg = new RecommendationLeg
        {
            LegOrder = 0,
            RouteId = route.RouteId,
            Route = route,
            TransportMode = new TransportMode { Code = "JEEPNEY" },
            StartRouteProgressMeters = 0,
            EndRouteProgressMeters = 1_000,
            DistanceMeters = 1_000
        };
        var sessions = new Mock<ITripSessionRepository>();
        sessions.Setup(item => item.GetOwnedAsync(session.TripSessionId, session.UserId, default))
            .ReturnsAsync(session);
        sessions.Setup(item => item.UpdateAsync(session, default)).ReturnsAsync(session);
        var recommendations = new Mock<IRouteRecommendationRepository>();
        recommendations.Setup(item => item.GetOrderedLegsAsync(session.RecommendationId, default))
            .ReturnsAsync([leg]);
        var routePoints = new Mock<IRoutePointRepository>();
        routePoints.Setup(item => item.GetOrderedByRouteAsync(route.RouteId, default))
            .ReturnsAsync([
                new RoutePoint { PointOrder = 0, Latitude = 15, Longitude = 120 },
                new RoutePoint { PointOrder = 1, Latitude = 15, Longitude = 120.1 }
            ]);
        var gps = new Mock<IGpsQualityValidator>();
        gps.Setup(item => item.Validate(It.IsAny<LocationUpdate>(), session, It.IsAny<DateTime>()))
            .Returns((string?)null);
        var matcher = new Mock<IMapMatchingService>();
        matcher.Setup(item => item.ProjectProgress(
                It.IsAny<IReadOnlyList<(double, double)>>(), It.IsAny<double>(), It.IsAny<double>()))
            .Returns(10_000);
        var offRoute = new Mock<IOffRouteDetector>();
        offRoute.Setup(item => item.Evaluate(
                session, leg, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTime>()))
            .Returns(OffRouteStatus.OnRoute);
        var options = Options.Create(new NavigationOptions());
        var service = new LocationTrackingService(
            sessions.Object,
            recommendations.Object,
            routePoints.Object,
            Mock.Of<IValhallaService>(),
            gps.Object,
            matcher.Object,
            Mock.Of<ILandmarkService>(),
            offRoute.Object,
            new TripSessionStateMachine(),
            options);
        return new RecoveryFixture(service, session, leg, matcher, offRoute);
    }

    private sealed record RecoveryFixture(
        LocationTrackingService Service,
        TripSession Session,
        RecommendationLeg Leg,
        Mock<IMapMatchingService> Matcher,
        Mock<IOffRouteDetector> OffRoute);
}
