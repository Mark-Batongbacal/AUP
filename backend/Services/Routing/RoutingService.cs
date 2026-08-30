using System.Collections.Concurrent;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using backend.Services.Telemetry;

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
    private double MinimumSelfTransferProgressMeters =>
        _options.MinimumSelfTransferProgressMeters;
    private double MinimumSelfTransferRouteToWalkRatio =>
        _options.MinimumSelfTransferRouteToWalkRatio;
    private int MaxNearbyTrikeCandidates => _options.MaxNearbyTrikeCandidates;
    private double MaxWalkToTrikePointMeters => _options.MaxWalkToTrikePointMeters;
    private double MaxWalkOnlyTripDistanceMeters => _options.MaxWalkOnlyTripDistanceMeters;
    private double MaxWalkTrikeTripDistanceMeters => _options.MaxWalkTrikeTripDistanceMeters;
    private double MaxWalkAccessDistanceMeters => _options.MaxWalkAccessDistanceMeters;
    private double LessWalkingPreferenceAccessMeters =>
        _options.LessWalkingPreferenceAccessMeters;
    private double NormalWalkingPreferenceAccessMeters =>
        _options.NormalWalkingPreferenceAccessMeters;
    private double MoreWalkingPreferenceAccessMeters =>
        _options.MoreWalkingPreferenceAccessMeters;
    private double MaxTotalWalkingMetersPerJourney => _options.MaxTotalWalkingMetersPerJourney;
    private double FeederShadowingMinProgressMeters =>
        _options.FeederShadowingMinProgressMeters;
    private double FeederShadowingAccessDistanceRatio =>
        _options.FeederShadowingAccessDistanceRatio;
    private double ProgressEqualityToleranceMeters =>
        _options.FeederShadowEquivalentProgressToleranceMeters;
    private double TokenTransitJeepneyMultiple =>
        _options.TokenTransitJeepneyMultiple;
    private int MaxBoardingVariantsPerRoute => _options.MaxBoardingVariantsPerRoute;
    private double PrimaryJeepneyMinimumDistanceMeters =>
        _options.PrimaryJeepneyMinimumDistanceMeters;
    private double PrimaryJeepneyMinimumJourneyShare =>
        _options.PrimaryJeepneyMinimumJourneyShare;
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
    private int MaxTransfers => _options.MaxTransfers;
    private int MinTransferCandidatesPerRoute => _options.MinTransferCandidatesPerRoute;

    private const double EarthRadiusMeters = 6_371_000;

    private readonly IValhallaService _valhallaService;
    private readonly ILogger<RoutingService> _logger;
    private readonly RoutingOptions _options;
    private readonly ITripAreaValidator _tripAreaValidator;
    private readonly ITukiTelemetry _telemetry;
    private IReadOnlyList<StaticJeepneyRoute> _routes = [];
    private IReadOnlyList<TrikePoint> _trikePoints = [];

    private IReadOnlyDictionary<string,
        IReadOnlyList<(double Latitude, double Longitude)>> _routeSamples =
        new Dictionary<string, IReadOnlyList<(double, double)>>();
    private IReadOnlyDictionary<string, FullRouteGeometry> _routeGeometries =
        new Dictionary<string, FullRouteGeometry>();
    private IReadOnlyDictionary<string, IReadOnlyList<RouteAnchor>>
        _routeSearchAnchors = new Dictionary<string, IReadOnlyList<RouteAnchor>>();
    private IReadOnlyDictionary<string, IReadOnlyList<RouteInterchange>>
        _interchangesByRoute = new Dictionary<string, IReadOnlyList<RouteInterchange>>();
    private IRouteSpatialIndex _spatialRouteIndex = RouteSpatialIndex.Build([]);
    private IReadOnlySet<string> _routesWithTodaAccess =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly ITransportRouteRepository _transportRouteRepository;
    private readonly ITricyclePointRepository _tricyclePointRepository;
    private readonly IRoutingNetworkSnapshotProvider _networkSnapshotProvider;
    private readonly RoutingNetworkSnapshotScope _networkSnapshotScope;
    private readonly IValhallaResultCache _valhallaResultCache;
    private readonly RoutingBenchmarkNetworkFixtureProvider?
        _benchmarkNetworkFixtureProvider;
    // RoutingService is scoped in DI, so this deduplicates only one HTTP
    // request's exact matrix work and never becomes a stale global cache.
    private readonly ConcurrentDictionary<ValhallaCacheKey,
        Task<IReadOnlyList<ValhallaMatrixResult>>>
        _matrixRequests = new();

    public RoutingService(
        IValhallaService valhallaService,
        ITransportRouteRepository transportRouteRepository,
        ITricyclePointRepository tricyclePointRepository,
        ILogger<RoutingService> logger,
        IOptions<RoutingOptions> options,
        ITripAreaValidator? tripAreaValidator = null,
        ITukiTelemetry? telemetry = null)
        : this(
            valhallaService,
            transportRouteRepository,
            tricyclePointRepository,
            logger,
            options,
            tripAreaValidator,
            telemetry,
            new RoutingNetworkSnapshotProvider(),
            valhallaResultCache: null)
    {
    }

    internal RoutingService(
        IValhallaService valhallaService,
        ITransportRouteRepository transportRouteRepository,
        ITricyclePointRepository tricyclePointRepository,
        ILogger<RoutingService> logger,
        IOptions<RoutingOptions> options,
        ITripAreaValidator? tripAreaValidator,
        ITukiTelemetry? telemetry,
        IRoutingNetworkSnapshotProvider networkSnapshotProvider,
        RoutingNetworkSnapshotScope? networkSnapshotScope = null,
        RoutingBenchmarkNetworkFixtureProvider? benchmarkNetworkFixtureProvider = null,
        IValhallaResultCache? valhallaResultCache = null)
    {
        _valhallaService = valhallaService;
        _logger = logger;
        _options = options.Value;
        _tripAreaValidator = tripAreaValidator ?? new TripAreaValidator(options);
        _telemetry = telemetry ?? NullTukiTelemetry.Instance;
        _transportRouteRepository = transportRouteRepository;
        _tricyclePointRepository = tricyclePointRepository;
        _networkSnapshotProvider = networkSnapshotProvider;
        _networkSnapshotScope = networkSnapshotScope ?? new RoutingNetworkSnapshotScope();
        _benchmarkNetworkFixtureProvider = benchmarkNetworkFixtureProvider;
        _valhallaResultCache = valhallaResultCache ??
            PassThroughValhallaResultCache.Instance;

        _logger.LogInformation(
            "Routing configuration loaded: VOT={Vot}, WalkingFatigue={WalkingFatigue}",
            _options.ValueOfTimePesosPerMinute,
            _options.WalkingFatiguePesosPerKilometer);

    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        using var initializationMeasurement =
            _telemetry.MeasureRouting("network_initialization_ms");
        var pinnedSnapshot = _networkSnapshotScope.Snapshot;
        var access = pinnedSnapshot is null
            ? await _networkSnapshotProvider.GetSnapshotAsync(
                BuildNetworkSnapshotAsync,
                cancellationToken)
            : new RoutingNetworkSnapshotAccess(pinnedSnapshot, false);
        pinnedSnapshot = _networkSnapshotScope.Pin(access.Snapshot);
        ApplyNetworkSnapshot(pinnedSnapshot);
        _telemetry.IncrementRouting(
            access.BuiltSnapshot
                ? "network_initialization_builds"
                : "network_initialization_cache_hits");
        _telemetry.SetRoutingValue(
            "network_snapshot_version",
            pinnedSnapshot.Version);
        RecordNetworkSizeTelemetry();
    }

    private async Task<RoutingNetworkSnapshot> BuildNetworkSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var benchmarkFixture = _benchmarkNetworkFixtureProvider is null
            ? null
            : await _benchmarkNetworkFixtureProvider.GetFixtureAsync();
        if (benchmarkFixture is not null)
        {
            _routes = ValidateRoutes(benchmarkFixture.Routes.Select(route =>
                new StaticJeepneyRoute
                {
                    RouteId = route.RouteId,
                    RouteName = route.RouteName,
                    Coordinates = route.Coordinates
                        .Select(point => point.ToArray())
                        .ToList()
                }));
            _trikePoints = ValidateTrikePoints(benchmarkFixture.TrikePoints);
        }
        else
        {
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
        }

        _routeGeometries = _routes.ToDictionary(
            route => route.RouteId,
            route => BuildFullRouteGeometry(route.Coordinates));

        _routeSamples = _routes
            .Where(route => route.Coordinates.Count >= 2)
            .ToDictionary(
                route => route.RouteId,
                route => (IReadOnlyList<(double Latitude, double Longitude)>)
                    SampleRoutePoints(
                        route.Coordinates,
                        DefaultSampleIntervalMeters,
                        MaxRouteSamples).ToList());

        _routeSearchAnchors = _routeSamples.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<RouteAnchor>)
                BuildSearchAnchors(pair.Key, pair.Value));

        var routeNamesById = _routes.ToDictionary(
            route => route.RouteId,
            route => route.RouteName);

        _interchangesByRoute = BuildInterchangeGraph(
            _routeSamples,
            routeNamesById);

        IRouteSpatialIndex spatialRouteIndex;
        try
        {
            spatialRouteIndex = RouteSpatialIndex.Build(_routes);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to build routing network spatial index");
            throw new InvalidOperationException(
                "The routing network spatial index could not be built.",
                exception);
        }
        var routesWithTodaAccess = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trikePoint in _trikePoints)
        {
            routesWithTodaAccess.UnionWith(
                spatialRouteIndex.FindNearbyRoutes(
                    trikePoint.Latitude,
                    trikePoint.Longitude,
                    MaxWalkToTrikePointMeters));
        }

        _logger.LogInformation(
            "Loaded {RouteCount} jeepney routes and {TrikePointCount} tricycle points from {NetworkSource}",
            _routes.Count,
            _trikePoints.Count,
            benchmarkFixture is null ? "database" : benchmarkFixture.FixtureId);

        return new RoutingNetworkSnapshot(
            Version: 0,
            _routes,
            _trikePoints,
            _routeSamples,
            _routeGeometries,
            _routeSearchAnchors,
            _interchangesByRoute,
            spatialRouteIndex,
            routesWithTodaAccess);
    }

    private void ApplyNetworkSnapshot(RoutingNetworkSnapshot snapshot)
    {
        _routes = snapshot.Routes;
        _trikePoints = snapshot.TrikePoints;
        _routeSamples = snapshot.RouteSamples;
        _routeGeometries = snapshot.RouteGeometries;
        _routeSearchAnchors = snapshot.RouteSearchAnchors;
        _interchangesByRoute = snapshot.InterchangesByRoute;
        _spatialRouteIndex = snapshot.SpatialRouteIndex;
        _routesWithTodaAccess = snapshot.RoutesWithTodaAccess;
    }

    private void RecordNetworkSizeTelemetry()
    {
        _telemetry.SetRoutingValue("route_count", _routes.Count);
        _telemetry.SetRoutingValue("trike_point_count", _trikePoints.Count);
        _telemetry.SetRoutingValue("toda_point_count", _trikePoints.Count);
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
        CancellationToken cancellationToken,
        ValhallaCacheUsage usage = ValhallaCacheUsage.General)
    {
        var key = ValhallaCacheKey.Matrix(
            CurrentNetworkSnapshotVersion(),
            source,
            targets,
            costing);

        Task<IReadOnlyList<ValhallaMatrixResult>>? createdRequest = null;
        var request = _matrixRequests.GetOrAdd(key, _ =>
        {
            createdRequest = _valhallaResultCache.GetOrCreateAsync(
                key,
                usage,
                sharedCancellationToken => _valhallaService.GetMatrixAsync(
                    source,
                    targets,
                    costing,
                    sharedCancellationToken),
                ValhallaCacheSize.Matrix,
                cancellationToken);
            return createdRequest;
        });

        _telemetry.IncrementRouting(
            createdRequest is not null && ReferenceEquals(request, createdRequest)
                ? "request_local_matrix_cache_misses"
                : "request_local_matrix_cache_hits");

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

    private Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing,
        CancellationToken cancellationToken,
        ValhallaCacheUsage usage = ValhallaCacheUsage.General)
    {
        var key = ValhallaCacheKey.Route(
            CurrentNetworkSnapshotVersion(),
            startLatitude,
            startLongitude,
            endLatitude,
            endLongitude,
            costing);
        return _valhallaResultCache.GetOrCreateAsync(
            key,
            usage,
            sharedCancellationToken => _valhallaService.GetRouteAsync(
                startLatitude,
                startLongitude,
                endLatitude,
                endLongitude,
                costing,
                sharedCancellationToken),
            ValhallaCacheSize.Route,
            cancellationToken);
    }

    private long CurrentNetworkSnapshotVersion() =>
        _networkSnapshotScope.Snapshot?.Version ??
        throw new InvalidOperationException(
            "Routing network snapshot must be initialized before Valhalla access.");

    private bool IsWithinServiceArea(double latitude, double longitude) =>
        latitude >= _options.ServiceAreaMinLatitude &&
        latitude <= _options.ServiceAreaMaxLatitude &&
        longitude >= _options.ServiceAreaMinLongitude &&
        longitude <= _options.ServiceAreaMaxLongitude;

}
