using backend.Models.Database;
using backend.Repositories;

namespace backend.Services.Transportation;

public sealed class TransportRouteService(
    ITransportRouteRepository transportRouteRepository,
    IRouteStopRepository routeStopRepository,
    IRouteSegmentRepository routeSegmentRepository,
    IFareRuleRepository fareRuleRepository) : ITransportRouteService
{
    private readonly ITransportRouteRepository _transportRouteRepository = transportRouteRepository;
    private readonly IRouteStopRepository _routeStopRepository = routeStopRepository;
    private readonly IRouteSegmentRepository _routeSegmentRepository = routeSegmentRepository;
    private readonly IFareRuleRepository _fareRuleRepository = fareRuleRepository;

    public Task<List<TransportRoute>> GetAllActiveRoutesAsync(CancellationToken cancellationToken = default) =>
        _transportRouteRepository.GetAllActiveAsync(cancellationToken);

    public Task<TransportRoute?> GetRouteByIdAsync(long routeId, CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return Task.FromResult<TransportRoute?>(null);
        }

        return _transportRouteRepository.GetByIdAsync(routeId, cancellationToken);
    }

    public Task<TransportRoute?> GetRouteByCodeAsync(string routeCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routeCode))
        {
            return Task.FromResult<TransportRoute?>(null);
        }

        return _transportRouteRepository.GetByRouteCodeAsync(routeCode.Trim(), cancellationToken);
    }

    public Task<List<TransportRoute>> GetRoutesByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default)
    {
        if (transportModeId <= 0)
        {
            return Task.FromResult(new List<TransportRoute>());
        }

        return _transportRouteRepository.GetByTransportModeAsync(transportModeId, cancellationToken);
    }

    public async Task<TransportRouteDetailsDto?> GetRouteDetailsAsync(long routeId, CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return null;
        }

        var route = await _transportRouteRepository.GetWithEndpointsAsync(routeId, cancellationToken);
        if (route is null)
        {
            return null;
        }

        // Transport route details combine route metadata with ordered stop/segment sequences and fare rules.
        var routeStops = await _routeStopRepository.GetOrderedStopsForRouteAsync(routeId, cancellationToken);
        var routeSegments = await _routeSegmentRepository.GetOrderedSegmentsForRouteAsync(routeId, cancellationToken);
        var fareRules = await _fareRuleRepository.GetActiveByRouteAsync(routeId, cancellationToken);

        return MapRouteDetails(route, routeStops, routeSegments, fareRules);
    }

    public Task<List<RouteStop>> GetRouteStopsAsync(long routeId, CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return Task.FromResult(new List<RouteStop>());
        }

        // The repository owns the StopOrder sort so callers receive the route sequence as stored.
        return _routeStopRepository.GetOrderedStopsForRouteAsync(routeId, cancellationToken);
    }

    public Task<List<RouteSegment>> GetRouteSegmentsAsync(long routeId, CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
        {
            return Task.FromResult(new List<RouteSegment>());
        }

        // The repository owns the SegmentOrder sort so callers receive the route path as stored.
        return _routeSegmentRepository.GetOrderedSegmentsForRouteAsync(routeId, cancellationToken);
    }

    private static TransportRouteDetailsDto MapRouteDetails(
        TransportRoute route,
        IReadOnlyList<RouteStop> routeStops,
        IReadOnlyList<RouteSegment> routeSegments,
        IReadOnlyList<FareRule> fareRules) =>
        new(
            route.RouteId,
            route.RouteCode,
            route.RouteName,
            route.TransportModeId,
            MapTransportMode(route.TransportMode),
            route.StartStopId,
            MapTransportStop(route.StartStop),
            route.EndStopId,
            MapTransportStop(route.EndStop),
            route.RouteDescription,
            route.BaseFare,
            route.EstimatedTotalMinutes,
            route.ServiceStartTime,
            route.ServiceEndTime,
            route.AverageHeadwayMinutes,
            route.OperatesMonday,
            route.OperatesTuesday,
            route.OperatesWednesday,
            route.OperatesThursday,
            route.OperatesFriday,
            route.OperatesSaturday,
            route.OperatesSunday,
            routeStops.Select(MapRouteStop).ToList(),
            routeSegments.Select(MapRouteSegment).ToList(),
            fareRules.Select(MapFareRule).ToList());

    private static TransportModeSummaryDto? MapTransportMode(TransportMode? mode) =>
        mode is null
            ? null
            : new TransportModeSummaryDto(
                mode.TransportModeId,
                mode.Code,
                mode.Name,
                mode.IsMotorized,
                mode.AllowsLiveDriver,
                mode.IconName);

    private static TransportStopSummaryDto? MapTransportStop(TransportStop? stop) =>
        stop is null
            ? null
            : new TransportStopSummaryDto(
                stop.StopId,
                stop.StopCode,
                stop.Name,
                stop.Description,
                stop.StopType,
                stop.Address,
                stop.Latitude,
                stop.Longitude);

    private static RouteStopDto MapRouteStop(RouteStop routeStop) =>
        new(
            routeStop.RouteStopId,
            routeStop.StopId,
            routeStop.StopOrder,
            routeStop.EstimatedMinutesFromStart,
            routeStop.CanBoard,
            routeStop.CanAlight,
            MapTransportStop(routeStop.Stop));

    private static RouteSegmentDto MapRouteSegment(RouteSegment segment) =>
        new(
            segment.SegmentId,
            segment.FromRouteStop.StopId,
            MapTransportStop(segment.FromRouteStop.Stop),
            segment.ToRouteStop.StopId,
            MapTransportStop(segment.ToRouteStop.Stop),
            segment.SegmentOrder,
            segment.DistanceMeters,
            segment.EstimatedDurationSeconds,
            segment.SegmentFare);

    private static FareRuleDto MapFareRule(FareRule fareRule) =>
        new(
            fareRule.FareRuleId,
            fareRule.TransportModeId,
            fareRule.RouteId,
            fareRule.RuleName,
            fareRule.BaseFare,
            fareRule.IncludedDistanceMeters,
            fareRule.AdditionalFarePerKm,
            fareRule.MinimumFare,
            fareRule.MaximumFare,
            fareRule.EffectiveFrom,
            fareRule.EffectiveTo);
}
