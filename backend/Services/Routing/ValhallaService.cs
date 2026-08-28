
using System.Diagnostics;
using System.Net.Http.Json;
using backend.Helpers;
using backend.Models.Valhalla;
using backend.Services.Telemetry;

namespace backend.Services.Routing;

public class ValhallaService : IValhallaService
{
    private const int DefaultMaxConcurrentRequests = 5;
    private const double DefaultWalkingSpeedMetersPerSecond = 1.2;
    private const double DefaultTrikeSpeedMetersPerSecond = 5.6;
    private const string DefaultTrikeCostingModel = "auto";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrentRequests;
    private readonly ITukiTelemetry _telemetry;
    private readonly double _walkingSpeedMetersPerSecond;
    private readonly double _trikeSpeedMetersPerSecond;
    private readonly string _trikeCostingModel;

    public ValhallaService(
        HttpClient httpClient,
        IConfiguration configuration,
        ITukiTelemetry? telemetry = null)
    {
        _httpClient = httpClient;
        _telemetry = telemetry ?? NullTukiTelemetry.Instance;

        var configuredConcurrency = configuration.GetValue<int?>(
            "Valhalla:MaxConcurrentRequests");
        var maxConcurrentRequests = configuredConcurrency is > 0
            ? configuredConcurrency.Value
            : DefaultMaxConcurrentRequests;

        _walkingSpeedMetersPerSecond = PositiveOrDefault(
            configuration.GetValue<double?>(
                "Routing:WalkingSpeedMetersPerSecond"),
            DefaultWalkingSpeedMetersPerSecond);
        _trikeSpeedMetersPerSecond = PositiveOrDefault(
            configuration.GetValue<double?>(
                "Routing:TrikeSpeedMetersPerSecond"),
            DefaultTrikeSpeedMetersPerSecond);
        _trikeCostingModel = configuration["Routing:TrikeCostingModel"]
            ?.Trim() is { Length: > 0 } configuredTrikeCosting
                ? configuredTrikeCosting
                : DefaultTrikeCostingModel;

        _maxConcurrentRequests = maxConcurrentRequests;
        _semaphore = new SemaphoreSlim(
            _maxConcurrentRequests,
            _maxConcurrentRequests);
    }

    public async Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude,
        string costing = "car",
        CancellationToken cancellationToken = default)
    {
        var request = new ValhallaRouteRequest
        {
            Locations =
            [
                new ValhallaLocation
                {
                    Lat = startLatitude,
                    Lon = startLongitude
                },
                new ValhallaLocation
                {
                    Lat = endLatitude,
                    Lon = endLongitude
                }
            ],
            Costing = costing
        };

        var response = await PostToValhallaAsync(
            "/route",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<ValhallaRouteResponse>(
                cancellationToken);

        var route = result
            ?? throw new InvalidOperationException(
                "Valhalla returned an empty response.");

        if (route.Trip is not null)
        {
            foreach (var leg in route.Trip.Legs)
            {
                leg.Points = PolylineDecoder.DecodePolyline6(leg.Shape)
                    .Select(point => new[]
                    {
                        point.Longitude,
                        point.Latitude
                    })
                    .ToList();
            }

            NormalizeRouteSummaryTime(route.Trip.Summary, costing);
        }

        return route;
    }

    public async Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default)
    {
        if (targets.Count == 0)
            return [];

        var request = new ValhallaMatrixRequest
        {
            Sources = [source],
            Targets = targets.ToList(),
            Costing = costing,
            Units = "kilometers",
            Verbose = true
        };

        var response = await PostToValhallaAsync(
            "/sources_to_targets",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<ValhallaMatrixResponse>(cancellationToken);

        var matrix = result?.SourcesToTargets
            .SelectMany(row => row)
            .ToList()
            ?? throw new InvalidOperationException(
                "Valhalla returned an empty matrix response.");

        NormalizeMatrixTimes(matrix, costing);
        return matrix;
    }

    /// <summary>
    /// Valhalla remains authoritative for the traversable road/path and its
    /// distance. Tuki owns passenger-facing ETA assumptions so provisional
    /// candidate scoring and confirmed journeys use the same per-mode speeds
    /// instead of switching to Valhalla's pedestrian/car timing model after
    /// confirmation.
    /// </summary>
    private void NormalizeMatrixTimes(
        IEnumerable<ValhallaMatrixResult> results,
        string costing)
    {
        var speed = GetConfiguredModeSpeed(costing);
        if (speed is null)
            return;

        foreach (var result in results)
        {
            if (result.Distance is not { } distanceKilometers ||
                !double.IsFinite(distanceKilometers) ||
                distanceKilometers < 0)
            {
                continue;
            }

            result.Time = distanceKilometers * 1_000 / speed.Value;
        }
    }

    private void NormalizeRouteSummaryTime(
        ValhallaSummary? summary,
        string costing)
    {
        var speed = GetConfiguredModeSpeed(costing);
        if (summary is null || speed is null ||
            !double.IsFinite(summary.Length) || summary.Length < 0)
        {
            return;
        }

        summary.Time = summary.Length * 1_000 / speed.Value;
    }

    private double? GetConfiguredModeSpeed(string costing)
    {
        if (string.Equals(costing, "pedestrian", StringComparison.OrdinalIgnoreCase))
            return _walkingSpeedMetersPerSecond;

        if (string.Equals(costing, _trikeCostingModel, StringComparison.OrdinalIgnoreCase))
            return _trikeSpeedMetersPerSecond;

        return null;
    }

    private static double PositiveOrDefault(double? configured, double fallback) =>
        configured is > 0 && double.IsFinite(configured.Value)
            ? configured.Value
            : fallback;

    private async Task<HttpResponseMessage> PostToValhallaAsync<T>(
        string endpoint,
        T request,
        CancellationToken cancellationToken)
    {
        using var measurement = _telemetry.Measure($"Valhalla{endpoint}");
        _telemetry.SetRoutingValue(
            "valhalla_concurrency_limit",
            _maxConcurrentRequests);
        var waitStarted = Stopwatch.GetTimestamp();
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
        }
        finally
        {
            _telemetry.ObserveRouting(
                "valhalla_gate_wait_ms",
                Stopwatch.GetElapsedTime(waitStarted).TotalMilliseconds);
        }

        _telemetry.IncrementRouting(
            endpoint == "/sources_to_targets"
                ? "valhalla_matrix_http_calls"
                : "valhalla_route_http_calls");
        var executionStarted = Stopwatch.GetTimestamp();
        try
        {
            return await _httpClient.PostAsJsonAsync(
                endpoint,
                request,
                cancellationToken);
        }
        finally
        {
            _telemetry.ObserveRouting(
                "valhalla_execution_ms",
                Stopwatch.GetElapsedTime(executionStarted).TotalMilliseconds);
            _semaphore.Release();
        }
    }
}
