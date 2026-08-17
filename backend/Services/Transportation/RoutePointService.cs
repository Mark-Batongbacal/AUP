using backend.Models.Database;
using backend.Repositories;

namespace backend.Services;

public sealed class RoutePointService(
    IRoutePointRepository routePointRepository,
    ITransportRouteRepository transportRouteRepository) : IRoutePointService
{
    private const int MinimumRoutePoints = 2;

    private readonly IRoutePointRepository _routePointRepository = routePointRepository;
    private readonly ITransportRouteRepository _transportRouteRepository = transportRouteRepository;

    public async Task<List<RoutePointDetailsDto>> GetRoutePointsAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return [];
        }

        var routePoints = await _routePointRepository.GetOrderedByRouteAsync(routeId, cancellationToken);
        return routePoints.Select(MapRoutePoint).ToList();
    }

    public async Task<RoutePointReplacementResult> ReplaceRoutePointsAsync(
        long routeId,
        List<List<double>> routePoints,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = Validate(routeId, routePoints, out var normalizedPoints);
        if (validationErrors.Count > 0)
        {
            return RoutePointReplacementResult.ValidationFailed(validationErrors);
        }

        var route = await _transportRouteRepository.GetByIdAsync(routeId, cancellationToken);
        if (route is null)
        {
            return RoutePointReplacementResult.RouteNotFound(routeId);
        }

        var createdAt = DateTime.UtcNow;
        var entities = normalizedPoints
            .Select((point, index) => new RoutePoint
            {
                RouteId = routeId,
                PointOrder = index + 1,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                CreatedAt = createdAt,
            })
            .ToList();

        var savedRoutePoints = await _routePointRepository.ReplaceForRouteAsync(
            routeId,
            entities,
            cancellationToken);

        return RoutePointReplacementResult.Success(savedRoutePoints.Select(MapRoutePoint).ToList());
    }

    private static List<string> Validate(
        long routeId,
        List<List<double>> routePoints,
        out List<(double Latitude, double Longitude)> normalizedPoints)
    {
        normalizedPoints = [];
        var errors = new List<string>();

        if (routeId <= 0)
        {
            errors.Add("Route id must be greater than zero.");
        }

        if (routePoints is null)
        {
            errors.Add("Route points payload is required.");
            return errors;
        }

        if (routePoints.Count < MinimumRoutePoints)
        {
            errors.Add($"At least {MinimumRoutePoints} route points are required.");
        }

        for (var index = 0; index < routePoints.Count; index++)
        {
            var pointNumber = index + 1;
            var coordinate = routePoints[index];
            if (coordinate is null || coordinate.Count != 2)
            {
                errors.Add($"Point {pointNumber} must contain exactly two values: [latitude, longitude].");
                continue;
            }

            var latitude = coordinate[0];
            var longitude = coordinate[1];

            if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
            {
                errors.Add($"Point {pointNumber} values must be finite numbers.");
                continue;
            }

            if (latitude is < -90 or > 90)
            {
                errors.Add($"Point {pointNumber} latitude must be between -90 and 90.");
            }

            if (longitude is < -180 or > 180)
            {
                errors.Add($"Point {pointNumber} longitude must be between -180 and 180.");
            }

            normalizedPoints.Add((latitude, longitude));
        }

        if (errors.Count > 0)
        {
            normalizedPoints = [];
        }

        return errors;
    }

    private static RoutePointDetailsDto MapRoutePoint(RoutePoint routePoint) =>
        new(
            routePoint.RoutePointId,
            routePoint.RouteId,
            routePoint.PointOrder,
            routePoint.Latitude,
            routePoint.Longitude);
}
