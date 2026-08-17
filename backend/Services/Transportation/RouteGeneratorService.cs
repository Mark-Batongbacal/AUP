using backend.Services.Routing;

namespace backend.Services.Transportation;

public sealed class RouteGeneratorService(IValhallaService valhallaService) : IRouteGeneratorService
{
    private readonly IValhallaService _valhallaService = valhallaService;

    public async Task<IReadOnlyList<List<double>>> GenerateAsync(
        IReadOnlyList<List<double>> waypoints,
        CancellationToken cancellationToken = default)
    {
        if (waypoints.Count < 2)
        {
            throw new ArgumentException("At least two waypoints are required.", nameof(waypoints));
        }

        for (var index = 0; index < waypoints.Count; index++)
        {
            var waypoint = waypoints[index];
            if (waypoint is null || waypoint.Count != 2)
            {
                throw new ArgumentException(
                    $"Waypoint {index + 1} must contain exactly two values: [latitude, longitude].",
                    nameof(waypoints));
            }

            if (!double.IsFinite(waypoint[0]) || waypoint[0] is < -90 or > 90 ||
                !double.IsFinite(waypoint[1]) || waypoint[1] is < -180 or > 180)
            {
                throw new ArgumentException(
                    $"Waypoint {index + 1} contains an invalid latitude or longitude.",
                    nameof(waypoints));
            }
        }

        var generatedPoints = new List<List<double>>();

        for (var index = 0; index < waypoints.Count - 1; index++)
        {
            var from = waypoints[index];
            var to = waypoints[index + 1];
            var response = await _valhallaService.GetRouteAsync(
                from[0],
                from[1],
                to[0],
                to[1],
                "auto",
                cancellationToken);

            var segmentPoints = response.Trip?.Legs
                .SelectMany(leg => leg.Points)
                .ToList();

            if (segmentPoints is null || segmentPoints.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Valhalla returned no usable geometry for waypoint segment {index}->{index + 1}.");
            }

            // ValhallaService exposes decoded points as [longitude, latitude],
            // while transport-routes accepts and stores [latitude, longitude].
            foreach (var point in segmentPoints)
            {
                if (point.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"Valhalla returned an invalid point for waypoint segment {index}->{index + 1}.");
                }

                var converted = new List<double> { point[1], point[0] };
                if (generatedPoints.Count > 0 && SameCoordinate(generatedPoints[^1], converted))
                {
                    continue;
                }

                generatedPoints.Add(converted);
            }
        }

        return generatedPoints;
    }

    private static bool SameCoordinate(IReadOnlyList<double> left, IReadOnlyList<double> right) =>
        left[0] == right[0] && left[1] == right[1];
}
