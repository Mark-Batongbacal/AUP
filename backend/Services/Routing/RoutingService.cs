using System.Text.Json;
using backend.Models.Routing;
using backend.Models.Valhalla;
using Microsoft.Extensions.Logging;

namespace backend.Services.Routing;

public partial class RoutingService : IRoutingService
{
    private const int MaxNearbyRoutes = 20;
    private const int MaxTripOptions = 10;

    // Route geometry is sampled by geographic distance rather than coordinate
    // index so dense source vertices do not consume the sample budget.
    private const double DefaultSampleIntervalMeters = 150.0;
    private const int MaxRouteSamples = 40;

    private const int MatrixChunkSize = 100;

    // Keep several geographically distinct transfer candidates between a pair
    // of routes. One global closest pair is not sufficient because the closest
    // interchange is not necessarily useful for every origin/destination.
    private const int MaxInterchangesPerRoutePair = 4;
    private const double MaxTransferWalkMeters = 400;

    // Trike points are candidate pickup/dropoff points. The geometric nearest
    // point is not necessarily the best walking point, so keep several nearby
    // candidates before selecting one by the cheap generalized-cost estimate.
    private const int MaxNearbyTrikeCandidates = 3;
    private const double MaxWalkToTrikePointMeters = 1000;

    // Direct-trip limits are candidate-generation limits that are validated
    // again with Valhalla's actual road distance before a plan is returned.
    private const double MaxWalkOnlyTripDistanceMeters = 2_000;
    private const double MaxWalkTrikeTripDistanceMeters = 5_000;

    // This is deliberately separate from the direct-trip limits: it applies
    // only when recovering a failed trike access leg with walking.
    private const double MaxWalkAccessDistanceMeters = 1_500;

    // Provisional local fare model. Verify against the actual municipality/TODA
    // fare rules before treating these values as authoritative.
    private const double TrikeBaseFarePesos = 35;
    private const double TrikeBaseDistanceMeters = 1_000;
    private const double TrikePerAdditionalKmPesos = 15;

    private const double ValueOfTimePesosPerMinute = 10.0;

    // Used only for candidate generation before Valhalla confirmation.
    private const double WalkingSpeedMetersPerSecond = 1.2;
    private const double TrikeSpeedMetersPerSecond = 5.6;
    private const double JeepneySpeedMetersPerSecond = 6.5;
    private const double JeepneyBoardingWaitTimeSeconds = 300;
    private const double JeepneyBaseFarePesos = 13;

    // Valhalla has no built-in tricycle profile. "auto" is currently only a
    // road-network stand-in; replace with a local/custom trike model later.
    private const string TrikeCostingModel = "auto";

    private const int MaxCandidatesToConfirm = 60;

    private const double EarthRadiusMeters = 6_371_000;

    private readonly IValhallaService _valhallaService;
    private readonly ILogger<RoutingService> _logger;
    private readonly List<StaticJeepneyRoute> _routes;
    private readonly List<TrikePoint> _trikePoints;

    private readonly Dictionary<string, List<(double Latitude, double Longitude)>> _routeSamples;
    private readonly Dictionary<string, List<RouteInterchange>> _interchangesByRoute;

    public RoutingService(
        IValhallaService valhallaService,
        IWebHostEnvironment environment,
        ILogger<RoutingService> logger)
    {
        _valhallaService = valhallaService;
        _logger = logger;

        var routesPath = Path.Combine(
            environment.ContentRootPath,
            "TestData",
            "jeepney-routes.json");

        if (!File.Exists(routesPath))
        {
            throw new FileNotFoundException(
                "Static jeepney route file was not found.",
                routesPath);
        }

        _routes = JsonSerializer.Deserialize<List<StaticJeepneyRoute>>(
            File.ReadAllText(routesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];

        var trikePointsPath = Path.Combine(
            environment.ContentRootPath,
            "TestData",
            "trike-points.json");

        if (File.Exists(trikePointsPath))
        {
            _trikePoints = JsonSerializer.Deserialize<List<TrikePoint>>(
                File.ReadAllText(trikePointsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? [];
        }
        else
        {
            _logger.LogWarning(
                "Trike points file not found at {Path}; trike-assisted routing will be unavailable.",
                trikePointsPath);

            _trikePoints = [];
        }

        _routeSamples = _routes
            .Where(route => route.Coordinates.Count >= 2)
            .ToDictionary(
                route => route.RouteId,
                route => SampleRoutePoints(route.Coordinates).ToList());

        var routeNamesById = _routes.ToDictionary(
            route => route.RouteId,
            route => route.RouteName);

        _interchangesByRoute = BuildInterchangeGraph(
            _routeSamples,
            routeNamesById);
    }

    // -------------------------------------------------------------------
    // Pickup-only lookup
    // -------------------------------------------------------------------

}
