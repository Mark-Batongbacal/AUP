using System.Collections.Concurrent;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

public partial class RoutingService : IRoutingService
{
    private int MaxNearbyRoutes => _options.MaxNearbyRoutes;
    private int MaxTripOptions => _options.MaxTripOptions;
    private double DefaultSampleIntervalMeters => _options.DefaultSampleIntervalMeters;
    private int MaxRouteSamples => _options.MaxRouteSamples;
    private int MatrixChunkSize => _options.MatrixMaxTargets;
    private int MaxInterchangesPerRoutePair => _options.MaxInterchangesPerRoutePair;
    private double MaxTransferWalkMeters => _options.MaxTransferWalkMeters;
    private int MaxNearbyTrikeCandidates => _options.MaxNearbyTrikeCandidates;
    private double MaxWalkToTrikePointMeters => _options.MaxWalkToTrikePointMeters;
    private double MaxWalkOnlyTripDistanceMeters => _options.MaxWalkOnlyTripDistanceMeters;
    private double MaxWalkTrikeTripDistanceMeters => _options.MaxWalkTrikeTripDistanceMeters;
    private double MaxWalkAccessDistanceMeters => _options.MaxWalkAccessDistanceMeters;
    private double MaxTotalWalkingMetersPerJourney => _options.MaxTotalWalkingMetersPerJourney;
    private double MaxStaticRouteSegmentJumpMeters => _options.MaxStaticRouteSegmentJumpMeters;
    private double TrikeBaseFarePesos => _options.TrikeBaseFarePesos;
    private double TrikeBaseDistanceMeters => _options.TrikeBaseDistanceMeters;
    private double TrikePerAdditionalKmPesos => _options.TrikePerAdditionalKmPesos;
    private double ValueOfTimePesosPerMinute => _options.ValueOfTimePesosPerMinute;
    private double WalkingFatiguePesosPerKilometer =>
        _options.WalkingFatiguePesosPerKilometer;
    private double WalkingSpeedMetersPerSecond => _options.WalkingSpeedMetersPerSecond;
    private double TrikeSpeedMetersPerSecond => _options.TrikeSpeedMetersPerSecond;
    private double JeepneySpeedMetersPerSecond => _options.JeepneySpeedMetersPerSecond;
    private double JeepneyBoardingWaitTimeSeconds => _options.JeepneyBoardingWaitTimeSeconds;
    private double JeepneyBaseFarePesos => _options.JeepneyBaseFarePesos;
    // "auto" is a road-network proxy, not a tricycle legality model.
    private string TrikeCostingModel => _options.TrikeCostingModel;
    private int MaxCandidatesToConfirm => _options.MaxCandidatesToConfirm;

    private const double EarthRadiusMeters = 6_371_000;

    private readonly IValhallaService _valhallaService;
    private readonly ILogger<RoutingService> _logger;
    private readonly RoutingOptions _options;
    private List<StaticJeepneyRoute> _routes = [];
    private List<TrikePoint> _trikePoints = [];

    private Dictionary<string, List<(double Latitude, double Longitude)>> _routeSamples = [];
    private Dictionary<string, FullRouteGeometry> _routeGeometries = [];
    private Dictionary<string, List<RouteAnchor>> _routeSearchAnchors = [];
    private Dictionary<string, List<RouteInterchange>> _interchangesByRoute = [];
    private readonly ITransportRouteRepository _transportRouteRepository;
    private readonly ITricyclePointRepository _tricyclePointRepository;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;
    // RoutingService is scoped in DI, so this deduplicates only one HTTP
    // request's exact matrix work and never becomes a stale global cache.
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<ValhallaMatrixResult>>>
        _matrixRequests = new(StringComparer.Ordinal);

    public RoutingService(
        IValhallaService valhallaService,
        ITransportRouteRepository transportRouteRepository,
        ITricyclePointRepository tricyclePointRepository,
        ILogger<RoutingService> logger,
        IOptions<RoutingOptions> options)
    {
        _valhallaService = valhallaService;
        _logger = logger;
        _options = options.Value;
        _transportRouteRepository = transportRouteRepository;
        _tricyclePointRepository = tricyclePointRepository;

        _logger.LogInformation(
            "Routing configuration loaded: VOT={Vot}, WalkingFatigue={WalkingFatigue}",
            _options.ValueOfTimePesosPerMinute,
            _options.WalkingFatiguePesosPerKilometer);

    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            var databaseRoutes = await _transportRouteRepository
                .GetAllActiveWithOrderedPointsAsync(cancellationToken);
            var databaseTrikePoints = await _tricyclePointRepository
                .GetAllActiveAsync(cancellationToken);

            _routes = ValidateRoutes(databaseRoutes
                .Where(route => string.Equals(
                    route.TransportMode?.Code,
                    "JEEPNEY",
                    StringComparison.OrdinalIgnoreCase))
                .Select(route => new StaticJeepneyRoute
                {
                    RouteId = route.RouteCode,
                    RouteName = route.RouteName,
                    Coordinates = route.RoutePoints
                        .OrderBy(point => point.PointOrder)
                        .Select(point => new[] { point.Longitude, point.Latitude })
                        .ToList()
                }));

            _trikePoints = ValidateTrikePoints(databaseTrikePoints.Select(point =>
                new TrikePoint(
                    point.PointCode,
                    point.PointName,
                    point.CenterLatitude,
                    point.CenterLongitude)));

            _routeGeometries = _routes.ToDictionary(
            route => route.RouteId,
            route => BuildFullRouteGeometry(route.Coordinates));

            _routeSamples = _routes
            .Where(route => route.Coordinates.Count >= 2)
            .ToDictionary(
                route => route.RouteId,
            route => SampleRoutePoints(
                route.Coordinates,
                DefaultSampleIntervalMeters,
                MaxRouteSamples).ToList());

            _routeSearchAnchors = _routeSamples.ToDictionary(
            pair => pair.Key,
            pair => BuildSearchAnchors(pair.Key, pair.Value));

            var routeNamesById = _routes.ToDictionary(
            route => route.RouteId,
            route => route.RouteName);

            _interchangesByRoute = BuildInterchangeGraph(
                _routeSamples,
                routeNamesById);

            _logger.LogInformation(
                "Loaded {RouteCount} jeepney routes and {TrikePointCount} tricycle points from the database",
                _routes.Count,
                _trikePoints.Count);
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    // -------------------------------------------------------------------
    // Pickup-only lookup
    // -------------------------------------------------------------------

    private List<StaticJeepneyRoute> ValidateRoutes(
        IEnumerable<StaticJeepneyRoute> routes)
    {
        var valid = new List<StaticJeepneyRoute>();
        var routeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            var coordinates = route.Coordinates ?? [];
            var validCoordinates = new List<double[]>();
            var malformedReason = string.Empty;

            for (var index = 0; index < coordinates.Count; index++)
            {
                var point = coordinates[index];
                if (point is not { Length: >= 2 } ||
                    !double.IsFinite(point[0]) || !double.IsFinite(point[1]))
                {
                    malformedReason = $"coordinate {index} is missing or non-finite";
                    break;
                }

                // Static route data is GeoJSON [longitude, latitude]. Never
                // swap suspicious values: report and skip the bad route.
                if (point[0] is >= -90 and <= 90 && point[1] is > 90 or < -90)
                {
                    malformedReason = $"coordinate {index} appears to use [latitude, longitude] order";
                    break;
                }

                var latitude = point[1];
                var longitude = point[0];
                if (!IsWithinServiceArea(latitude, longitude))
                {
                    malformedReason = $"coordinate {index} ({longitude}, {latitude}) is outside the configured service area";
                    break;
                }

                if (validCoordinates.Count > 0 &&
                    ApproximateDistanceMeters(
                        validCoordinates[^1][1], validCoordinates[^1][0],
                        latitude, longitude) > MaxStaticRouteSegmentJumpMeters)
                {
                    malformedReason = $"coordinate {index} creates a segment larger than {MaxStaticRouteSegmentJumpMeters}m";
                    break;
                }

                if (validCoordinates.Count == 0 ||
                    validCoordinates[^1][0] != point[0] ||
                    validCoordinates[^1][1] != point[1])
                {
                    validCoordinates.Add(point);
                }
            }

            if (string.IsNullOrWhiteSpace(route.RouteId) ||
                string.IsNullOrWhiteSpace(route.RouteName) ||
                validCoordinates.Count < 2 || !routeIds.Add(route.RouteId) ||
                !string.IsNullOrEmpty(malformedReason))
            {
                _logger.LogWarning(
                    "Skipping malformed or duplicate static jeepney route {RouteId}: {Reason}",
                    route.RouteId,
                    string.IsNullOrEmpty(malformedReason)
                        ? "missing id/name, duplicate id, or fewer than two distinct coordinates"
                        : malformedReason);
                continue;
            }

            route.Coordinates = validCoordinates;
            valid.Add(route);
        }

        return valid;
    }

    private List<TrikePoint> ValidateTrikePoints(IEnumerable<TrikePoint> points)
    {
        var valid = new List<TrikePoint>();
        var pointIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var point in points)
        {
            if (string.IsNullOrWhiteSpace(point.Id) ||
                string.IsNullOrWhiteSpace(point.Name) ||
                !double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) ||
                !IsWithinServiceArea(point.Latitude, point.Longitude) || !pointIds.Add(point.Id))
            {
                _logger.LogWarning("Skipping malformed or duplicate trike point {TrikePointId}", point.Id);
                continue;
            }

            valid.Add(point);
        }

        return valid;
    }

    private async Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing,
        CancellationToken cancellationToken)
    {
        var key = string.Join('|', costing,
            CoordinateKey(source),
            string.Join(';', targets.Select(CoordinateKey)));

        var request = _matrixRequests.GetOrAdd(key, _ =>
            _valhallaService.GetMatrixAsync(
                source,
                targets,
                costing,
                cancellationToken));

        try
        {
            return await request;
        }
        catch
        {
            if (request.IsFaulted || request.IsCanceled)
                _matrixRequests.TryRemove(key, out _);
            throw;
        }
    }

    private static string CoordinateKey(ValhallaLocation location) =>
        $"{location.Lat:F7},{location.Lon:F7}";

    private bool IsWithinServiceArea(double latitude, double longitude) =>
        latitude >= _options.ServiceAreaMinLatitude &&
        latitude <= _options.ServiceAreaMaxLatitude &&
        longitude >= _options.ServiceAreaMinLongitude &&
        longitude <= _options.ServiceAreaMaxLongitude;

}
