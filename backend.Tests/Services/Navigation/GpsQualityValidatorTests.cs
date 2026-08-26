using backend.Models.Database;
using backend.Services.Navigation;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Navigation;

public sealed class GpsQualityValidatorTests
{
    private readonly DateTime _now = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
    private GpsQualityValidator Validator() => new(Options.Create(new NavigationOptions()));

    [Fact]
    public void ValidFix_IsAccepted() =>
        Assert.Null(Validator().Validate(new(15.1, 120.5, 10, _now), new(), _now));

    [Fact]
    public void StaleFix_IsRejected() =>
        Assert.Equal("STALE_LOCATION", Validator().Validate(
            new(15.1, 120.5, 10, _now.AddMinutes(-3)), new(), _now));

    [Fact]
    public void PoorAccuracy_IsRejected() =>
        Assert.Equal("POOR_ACCURACY", Validator().Validate(
            new(15.1, 120.5, 100, _now), new(), _now));

    [Fact]
    public void OutOfOrderFix_IsRejected()
    {
        var session = new TripSession { LastLocationAt = _now };
        Assert.Equal("OUT_OF_ORDER_LOCATION", Validator().Validate(
            new(15.1, 120.5, 10, _now), session, _now));
    }

    [Fact]
    public void Reroute_CanReuseTheExactFixJustAcceptedByLocation()
    {
        var session = new TripSession
        {
            LastLatitude = 15.1,
            LastLongitude = 120.5,
            LastAccuracyMeters = 10,
            LastLocationAt = _now
        };

        Assert.Null(Validator().ValidateForReroute(
            new(15.1, 120.5, 10, _now), session, _now));
    }

    [Fact]
    public void ImpossibleJump_IsRejected()
    {
        var session = new TripSession
        {
            LastLatitude = 15.1, LastLongitude = 120.5,
            LastLocationAt = _now.AddSeconds(-1)
        };
        Assert.Equal("IMPOSSIBLE_JUMP", Validator().Validate(
            new(15.101, 120.5, 10, _now), session, _now));
    }
}
