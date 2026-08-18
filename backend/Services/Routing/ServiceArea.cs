using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

public interface IServiceArea
{
    bool Contains(double latitude, double longitude);
}

public sealed class BoundingBoxServiceArea(IOptions<RoutingOptions> options) : IServiceArea
{
    private readonly RoutingOptions _options = options.Value;

    public bool Contains(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 &&
        latitude >= _options.ServiceAreaMinLatitude &&
        latitude <= _options.ServiceAreaMaxLatitude &&
        longitude >= _options.ServiceAreaMinLongitude &&
        longitude <= _options.ServiceAreaMaxLongitude;
}

public interface ITripAreaValidator
{
    TripAreaValidationResult ValidateCoordinate(double latitude, double longitude);
    TripAreaValidationResult ValidateTrip(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude);
}

public sealed record TripAreaValidationResult(bool IsValid, string? ErrorCode, string? Message)
{
    public static TripAreaValidationResult Valid { get; } = new(true, null, null);
}

public sealed class TripAreaValidator : ITripAreaValidator
{
    private const double EarthRadiusMeters = 6_371_000;
    private readonly IServiceArea _serviceArea;
    private readonly RoutingOptions _options;

    public TripAreaValidator(IOptions<RoutingOptions> options)
        : this(new BoundingBoxServiceArea(options), options) { }

    public TripAreaValidator(IServiceArea serviceArea, IOptions<RoutingOptions> options)
    {
        _serviceArea = serviceArea;
        _options = options.Value;
    }

    public TripAreaValidationResult ValidateCoordinate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return new(false, "INVALID_COORDINATES", "The supplied coordinates are invalid.");

        return _serviceArea.Contains(latitude, longitude)
            ? TripAreaValidationResult.Valid
            : new(false, "OUTSIDE_SERVICE_AREA",
                "This location is outside Tuki's currently supported area.");
    }

    public TripAreaValidationResult ValidateTrip(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude)
    {
        var origin = ValidateCoordinate(originLatitude, originLongitude);
        if (!origin.IsValid) return origin;
        var destination = ValidateCoordinate(destinationLatitude, destinationLongitude);
        if (!destination.IsValid) return destination;

        var distance = DistanceMeters(
            originLatitude, originLongitude, destinationLatitude, destinationLongitude);
        return distance <= _options.MaxSupportedTripStraightLineMeters
            ? TripAreaValidationResult.Valid
            : new(false, "TRIP_DISTANCE_EXCEEDED",
                "This trip exceeds Tuki's maximum supported straight-line distance.");
    }

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var dLat = Radians(lat2 - lat1);
        var dLon = Radians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public sealed class RoutingValidationException(string errorCode, string message)
    : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}
