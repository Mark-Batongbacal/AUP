using backend.Models.Database;
using backend.Services.Navigation;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Navigation;

public sealed class OffRouteDetectorTests
{
    private readonly DateTime _start = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
    private OffRouteDetector Detector() => new(Options.Create(new NavigationOptions
    {
        WalkingOffRouteMeters = 50, TransitOffRouteMeters = 120,
        MinimumOffRouteSamples = 3, OffRouteDurationSeconds = 10
    }));
    private static RecommendationLeg Walk() => new()
        { TransportMode = new TransportMode { Code = "WALK" } };

    [Fact]
    public void OneBadSample_DoesNotConfirmOffRoute()
    {
        var status = Detector().Evaluate(new(), Walk(), 100, 5, _start);
        Assert.Equal(OffRouteStatus.Suspected, status);
    }

    [Fact]
    public void SustainedDeviation_ConfirmsAfterSamplesAndDuration()
    {
        var session = new TripSession();
        var detector = Detector();
        detector.Evaluate(session, Walk(), 100, 5, _start);
        detector.Evaluate(session, Walk(), 100, 5, _start.AddSeconds(5));
        var status = detector.Evaluate(session, Walk(), 100, 5, _start.AddSeconds(11));
        Assert.Equal(OffRouteStatus.Confirmed, status);
    }

    [Fact]
    public void Recovery_CancelsPendingSuspicion()
    {
        var session = new TripSession();
        var detector = Detector();
        detector.Evaluate(session, Walk(), 100, 5, _start);
        Assert.Equal(OffRouteStatus.OnRoute,
            detector.Evaluate(session, Walk(), 20, 5, _start.AddSeconds(5)));
        Assert.Equal(0, session.ConsecutiveOffRouteSamples);
        Assert.Null(session.OffRouteSuspectedAt);
    }

    [Fact]
    public void AccuracyThatExplainsDeviation_IsUncertainNotOffRoute()
    {
        Assert.Equal(OffRouteStatus.UncertainGps,
            Detector().Evaluate(new(), Walk(), 100, 60, _start));
    }
}
