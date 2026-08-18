using backend.Models.Database;
using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public enum OffRouteStatus { OnRoute, Suspected, Confirmed, UncertainGps }

public interface IOffRouteDetector
{
    OffRouteStatus Evaluate(TripSession session, RecommendationLeg leg,
        double distanceFromGeometryMeters, double accuracyMeters, DateTime timestamp);
}

public sealed class OffRouteDetector(IOptions<NavigationOptions> options) : IOffRouteDetector
{
    private readonly NavigationOptions _options = options.Value;

    public OffRouteStatus Evaluate(TripSession session, RecommendationLeg leg,
        double distance, double accuracy, DateTime timestamp)
    {
        var walking = leg.TransportMode?.Code is "WALK" or "WALKING" or "PEDESTRIAN";
        var threshold = walking ? _options.WalkingOffRouteMeters : _options.TransitOffRouteMeters;
        if (accuracy >= threshold || distance <= threshold + accuracy)
        {
            session.ConsecutiveOffRouteSamples = 0;
            session.OffRouteSuspectedAt = null;
            return accuracy >= threshold ? OffRouteStatus.UncertainGps : OffRouteStatus.OnRoute;
        }
        session.OffRouteSuspectedAt ??= timestamp;
        session.ConsecutiveOffRouteSamples++;
        var sustained = (timestamp - session.OffRouteSuspectedAt.Value).TotalSeconds >=
            _options.OffRouteDurationSeconds;
        return sustained && session.ConsecutiveOffRouteSamples >= _options.MinimumOffRouteSamples
            ? OffRouteStatus.Confirmed : OffRouteStatus.Suspected;
    }
}
