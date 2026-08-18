using backend.Services.Navigation;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Navigation;

public sealed class MapMatchingServiceTests
{
    private static MapMatchingService Matcher(double backwards = 75) => new(
        Options.Create(new NavigationOptions
        {
            MaxBackwardProgressMeters = backwards,
            MaxForwardProgressMetersPerUpdate = 2_000
        }));

    [Fact]
    public void NormalProgress_MatchesCurrentLeg()
    {
        var geometry = new List<(double, double)> { (15.0, 120.0), (15.0, 120.01) };
        var result = Matcher().Match(
            new(15.0, 120.005, 5, DateTime.UtcNow), geometry, 0, 2_000, 400);
        Assert.NotNull(result);
        Assert.InRange(result.DistanceFromRouteStartMeters, 500, 620);
    }

    [Fact]
    public void SmallBackwardNoise_IsAllowed()
    {
        var geometry = new List<(double, double)> { (15.0, 120.0), (15.0, 120.01) };
        var result = Matcher().Match(
            new(15.0, 120.0048, 5, DateTime.UtcNow), geometry, 0, 2_000, 550);
        Assert.NotNull(result);
        Assert.True(result.DistanceFromRouteStartMeters < 550);
    }

    [Fact]
    public void LoopCrossing_CannotJumpBackToEarlierPass()
    {
        var geometry = new List<(double, double)>
        {
            (15.0, 120.0), (15.001, 120.001), (15.0, 120.002),
            (14.999, 120.001), (15.0, 120.0), (15.001, 119.999)
        };
        var result = Matcher(30).Match(
            new(15.0, 120.0, 5, DateTime.UtcNow), geometry, 0, 5_000, 600);
        Assert.NotNull(result);
        Assert.True(result.DistanceFromRouteStartMeters > 500);
    }

    [Fact]
    public void LargeBackwardRegression_IsNotMatched()
    {
        var geometry = new List<(double, double)> { (15.0, 120.0), (15.0, 120.02) };
        var result = Matcher(30).Match(
            new(15.0, 120.001, 5, DateTime.UtcNow), geometry, 0, 3_000, 1_500);
        Assert.Null(result);
    }
}
