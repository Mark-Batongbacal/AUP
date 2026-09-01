using backend.Models.Database;
using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public interface IGpsQualityValidator
{
    string? Validate(LocationUpdate update, TripSession session, DateTime utcNow);
    string? ValidateForReroute(LocationUpdate update, TripSession session, DateTime utcNow);
}

public sealed class GpsQualityValidator(IOptions<NavigationOptions> options) : IGpsQualityValidator
{
    private readonly NavigationOptions _options = options.Value;

    public string? Validate(LocationUpdate update, TripSession session, DateTime utcNow)
        => ValidateCore(update, session, utcNow, allowPreviouslyAcceptedFix: false);

    public string? ValidateForReroute(LocationUpdate update, TripSession session, DateTime utcNow)
        => ValidateCore(update, session, utcNow, allowPreviouslyAcceptedFix: true);

    private string? ValidateCore(
        LocationUpdate update,
        TripSession session,
        DateTime utcNow,
        bool allowPreviouslyAcceptedFix)
    {
        if (!double.IsFinite(update.Latitude) || !double.IsFinite(update.Longitude) ||
            update.Latitude is < -90 or > 90 || update.Longitude is < -180 or > 180 ||
            !double.IsFinite(update.AccuracyMeters) || update.AccuracyMeters < 0)
            return "INVALID_LOCATION";
        if (update.Timestamp.Kind == DateTimeKind.Unspecified ||
            update.Timestamp < utcNow.AddSeconds(-_options.MaxLocationAgeSeconds) ||
            update.Timestamp > utcNow.AddSeconds(30)) return "STALE_LOCATION";
        if (update.AccuracyMeters > _options.MaxGpsAccuracyMeters) return "POOR_ACCURACY";
        if (update.SpeedMetersPerSecond is < 0 ||
            update.SpeedMetersPerSecond > _options.MaxPlausibleSpeedMetersPerSecond)
            return "IMPLAUSIBLE_SPEED";
        if (session.LastLocationAt is { } previous && update.Timestamp <= previous)
        {
            var sameAcceptedFix = allowPreviouslyAcceptedFix && update.Timestamp == previous &&
                session.LastLatitude == update.Latitude && session.LastLongitude == update.Longitude &&
                session.LastAccuracyMeters == update.AccuracyMeters;
            if (!sameAcceptedFix) return "OUT_OF_ORDER_LOCATION";
        }
        if (session.LastLatitude is { } lat && session.LastLongitude is { } lon &&
            session.LastLocationAt is { } time && update.Timestamp > time)
        {
            var seconds = (update.Timestamp - time).TotalSeconds;
            var speed = Geo.DistanceMeters(lat, lon, update.Latitude, update.Longitude) / seconds;
            if (speed > _options.MaxPlausibleSpeedMetersPerSecond) return "IMPOSSIBLE_JUMP";
        }
        return null;
    }
}

internal static class Geo
{
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var latScale = 111_000d;
        var lonScale = latScale * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        return Math.Sqrt(Math.Pow((lat2 - lat1) * latScale, 2) + Math.Pow((lon2 - lon1) * lonScale, 2));
    }
}
