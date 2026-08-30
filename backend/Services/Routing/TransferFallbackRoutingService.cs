using System.Globalization;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

/// <summary>
/// Keeps ordinary route planning bounded to at most two transfers, then expands
/// to a third transfer only when the fully confirmed/pruned first pass returns
/// no usable journey. This avoids paying the depth-three search cost on normal
/// trips while preserving coverage for the uncommon routes that genuinely need
/// four transit legs.
/// </summary>
public sealed class TransferFallbackRoutingService : IRoutingService, IJourneyGeometryEnricher
{
    private const int PreferredTransferDepth = 2;
    private const int FallbackTransferDepth = 3;

    private readonly RoutingService _preferredRouting;
    private readonly RoutingService? _fallbackRouting;
    private readonly ILogger<TransferFallbackRoutingService> _logger;
    private readonly ITukiTelemetry _telemetry;
    private readonly int _preferredMaxTransfers;
    private readonly int _fallbackMaxTransfers;

    public TransferFallbackRoutingService(
        IValhallaService valhallaService,
        ITransportRouteRepository transportRouteRepository,
        ITricyclePointRepository tricyclePointRepository,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IOptions<RoutingOptions> configuredOptions,
        ITripAreaValidator tripAreaValidator,
        ITukiTelemetry telemetry,
        RoutingNetworkSnapshotProvider networkSnapshotProvider,
        ValhallaResultCache valhallaResultCache,
        ILogger<TransferFallbackRoutingService> logger)
    {
        _logger = logger;
        _telemetry = telemetry;

        var configuredMaxTransfers = Math.Max(0, configuredOptions.Value.MaxTransfers);
        _preferredMaxTransfers = Math.Min(configuredMaxTransfers, PreferredTransferDepth);
        _fallbackMaxTransfers = Math.Min(configuredMaxTransfers, FallbackTransferDepth);

        var routingLogger = loggerFactory.CreateLogger<RoutingService>();
        var networkSnapshotScope = new RoutingNetworkSnapshotScope();

        _preferredRouting = new RoutingService(
            valhallaService,
            transportRouteRepository,
            tricyclePointRepository,
            routingLogger,
            CreateRoutingOptions(configuration, _preferredMaxTransfers),
            tripAreaValidator,
            telemetry,
            networkSnapshotProvider,
            networkSnapshotScope,
            valhallaResultCache);

        if (_fallbackMaxTransfers > _preferredMaxTransfers)
        {
            _fallbackRouting = new RoutingService(
                valhallaService,
                transportRouteRepository,
                tricyclePointRepository,
                routingLogger,
                CreateRoutingOptions(configuration, _fallbackMaxTransfers),
                tripAreaValidator,
                telemetry,
                networkSnapshotProvider,
                networkSnapshotScope,
                valhallaResultCache);
        }
    }

    public Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        _preferredRouting.FindNearbyRoutesAsync(
            latitude,
            longitude,
            cancellationToken);

    public Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default) =>
        PlanTripsWithFallbackAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences: null,
            cancellationToken);

    public Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken = default) =>
        PlanTripsWithFallbackAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences,
            cancellationToken);

    public Task EnrichSelectedPlanGeometryAsync(
        IReadOnlyList<JeepneyTripPlan> plans,
        CancellationToken cancellationToken = default) =>
        // The preferred planner always executes first and therefore has the
        // same authoritative route geometry loaded even when the plans came
        // from the fallback pass.
        _preferredRouting.EnrichSelectedPlanGeometryAsync(plans, cancellationToken);

    private async Task<List<JeepneyTripPlan>> PlanTripsWithFallbackAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        JourneyPlanningPreferences? preferences,
        CancellationToken cancellationToken)
    {
        using var planTelemetry = _telemetry.BeginRoutingPlan(
            "TransferFallbackRoutingService",
            cancellationToken);
        var preferredPlans = await _preferredRouting.PlanTripsAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences,
            cancellationToken);

        if (preferredPlans.Count > 0 || _fallbackRouting is null)
        {
            planTelemetry.Complete(
                preferredPlans.Count == 0 ? "no_route" : "success");
            return preferredPlans;
        }

        _telemetry.IncrementRouting("fallback_used");
        _logger.LogInformation(
            "No usable journey survived routing with at most {PreferredMaxTransfers} transfers; retrying with at most {FallbackMaxTransfers} transfers",
            _preferredMaxTransfers,
            _fallbackMaxTransfers);

        var fallbackPlans = await _fallbackRouting.PlanTripsAsync(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            preferences,
            cancellationToken);
        planTelemetry.Complete(
            fallbackPlans.Count == 0 ? "no_route" : "success");
        return fallbackPlans;
    }

    private static IOptions<RoutingOptions> CreateRoutingOptions(
        IConfiguration configuration,
        int maxTransfers)
    {
        var overrideValues = new Dictionary<string, string?>
        {
            [$"{RoutingOptions.SectionName}:MaxTransfers"] =
                maxTransfers.ToString(CultureInfo.InvariantCulture)
        };

        var scopedConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(overrideValues)
            .Build();

        var options = scopedConfiguration
            .GetSection(RoutingOptions.SectionName)
            .Get<RoutingOptions>() ?? new RoutingOptions();

        if (!options.IsValid(out var error))
        {
            throw new InvalidOperationException(
                $"Routing configuration is invalid for MaxTransfers={maxTransfers}: {error}");
        }

        return Options.Create(options);
    }
}
