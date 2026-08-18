using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public interface IMapMatchingService
{
    RouteMatch? Match(
        LocationUpdate update,
        IReadOnlyList<(double Latitude, double Longitude)> geometry,
        double legStartRouteProgressMeters,
        double legEndRouteProgressMeters,
        double? previousRouteProgressMeters);
    double ProjectProgress(IReadOnlyList<(double Latitude, double Longitude)> geometry, double latitude, double longitude);
    RouteMatch? ProjectClosest(IReadOnlyList<(double Latitude, double Longitude)> geometry, double latitude, double longitude);
}

public sealed class MapMatchingService(IOptions<NavigationOptions> options) : IMapMatchingService
{
    private readonly NavigationOptions _options = options.Value;

    public RouteMatch? Match(LocationUpdate update,
        IReadOnlyList<(double Latitude, double Longitude)> geometry,
        double legStartRouteProgressMeters, double legEndRouteProgressMeters,
        double? previousRouteProgressMeters)
    {
        if (geometry.Count < 2) return null;
        var cumulative = Cumulative(geometry);
        var minimum = Math.Max(legStartRouteProgressMeters,
            (previousRouteProgressMeters ?? legStartRouteProgressMeters) - _options.MaxBackwardProgressMeters);
        var maximum = Math.Min(legEndRouteProgressMeters,
            (previousRouteProgressMeters ?? legStartRouteProgressMeters) + _options.MaxForwardProgressMetersPerUpdate);
        RouteMatch? best = null;
        for (var index = 0; index < geometry.Count - 1; index++)
        {
            var length = cumulative[index + 1] - cumulative[index];
            if (cumulative[index + 1] < minimum || cumulative[index] > maximum) continue;
            var projected = Project(geometry[index], geometry[index + 1], update.Latitude, update.Longitude);
            var progress = cumulative[index] + length * projected.Fraction;
            if (progress < minimum || progress > maximum) continue;
            var distance = Geo.DistanceMeters(update.Latitude, update.Longitude, projected.Latitude, projected.Longitude);
            if (best is null || distance < best.DistanceFromGeometryMeters)
                best = new(projected.Latitude, projected.Longitude, distance,
                    Math.Max(0, progress - legStartRouteProgressMeters), progress,
                    index, projected.Fraction);
        }
        return best;
    }

    public double ProjectProgress(IReadOnlyList<(double Latitude, double Longitude)> geometry, double latitude, double longitude)
        => ProjectClosest(geometry, latitude, longitude)?.DistanceFromRouteStartMeters ?? 0;

    public RouteMatch? ProjectClosest(IReadOnlyList<(double Latitude, double Longitude)> geometry, double latitude, double longitude)
    {
        if (geometry.Count < 2) return null;
        var cumulative = Cumulative(geometry);
        RouteMatch? best = null;
        for (var index = 0; index < geometry.Count - 1; index++)
        {
            var projected = Project(geometry[index], geometry[index + 1], latitude, longitude);
            var distance = Geo.DistanceMeters(latitude, longitude, projected.Latitude, projected.Longitude);
            if (best is not null && distance >= best.DistanceFromGeometryMeters) continue;
            var progress = cumulative[index] + (cumulative[index + 1] - cumulative[index]) * projected.Fraction;
            best = new(projected.Latitude, projected.Longitude, distance, progress, progress, index, projected.Fraction);
        }
        return best;
    }

    private static double[] Cumulative(IReadOnlyList<(double Latitude, double Longitude)> geometry)
    {
        var result = new double[geometry.Count];
        for (var index = 1; index < geometry.Count; index++)
            result[index] = result[index - 1] + Geo.DistanceMeters(
                geometry[index - 1].Latitude, geometry[index - 1].Longitude,
                geometry[index].Latitude, geometry[index].Longitude);
        return result;
    }

    private static (double Latitude, double Longitude, double Fraction) Project(
        (double Latitude, double Longitude) from, (double Latitude, double Longitude) to,
        double latitude, double longitude)
    {
        var lonScale = 111_000 * Math.Cos((from.Latitude + to.Latitude) / 2 * Math.PI / 180);
        var x = (longitude - from.Longitude) * lonScale;
        var y = (latitude - from.Latitude) * 111_000;
        var sx = (to.Longitude - from.Longitude) * lonScale;
        var sy = (to.Latitude - from.Latitude) * 111_000;
        var fraction = sx * sx + sy * sy <= 0 ? 0 : Math.Clamp((x * sx + y * sy) / (sx * sx + sy * sy), 0, 1);
        return (from.Latitude + (to.Latitude - from.Latitude) * fraction,
            from.Longitude + (to.Longitude - from.Longitude) * fraction, fraction);
    }
}
