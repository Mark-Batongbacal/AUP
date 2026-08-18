using backend.Services.Routing;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Routing;

public sealed class TripAreaValidatorTests
{
    private static TripAreaValidator Create(double maxDistance = 75_000)
    {
        var options = Options.Create(new RoutingOptions
        {
            ServiceAreaMinLatitude = 14.8,
            ServiceAreaMaxLatitude = 15.35,
            ServiceAreaMinLongitude = 120.35,
            ServiceAreaMaxLongitude = 120.9,
            MaxSupportedTripStraightLineMeters = maxDistance
        });
        return new TripAreaValidator(options);
    }

    [Fact]
    public void ValidLocalJourney_IsAccepted() =>
        Assert.True(Create().ValidateTrip(15.10, 120.58, 15.17, 120.59).IsValid);

    [Fact]
    public void UnsupportedDistantDestination_IsRejectedBeforeRouting()
    {
        var result = Create().ValidateTrip(15.10, 120.58, 14.60, 121.00);
        Assert.False(result.IsValid);
        Assert.Equal("OUTSIDE_SERVICE_AREA", result.ErrorCode);
    }

    [Fact]
    public void SanityDistance_IsSecondaryToServiceArea()
    {
        var result = Create(1_000).ValidateTrip(15.10, 120.58, 15.12, 120.58);
        Assert.False(result.IsValid);
        Assert.Equal("TRIP_DISTANCE_EXCEEDED", result.ErrorCode);
    }

    [Theory]
    [InlineData(double.NaN, 120.58)]
    [InlineData(91, 120.58)]
    [InlineData(15.1, 181)]
    public void MalformedCoordinates_AreRejected(double latitude, double longitude)
    {
        var result = Create().ValidateCoordinate(latitude, longitude);
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_COORDINATES", result.ErrorCode);
    }
}
