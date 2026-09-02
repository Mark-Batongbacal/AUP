using backend.Models.Database;
using backend.Repositories;
using backend.Helpers;
using backend.Services.Routing;

namespace backend.Services.Transportation;

public sealed class TransportRouteService(
    ITransportRouteRepository transportRouteRepository,
    ITransportModeRepository transportModeRepository,
    IRouteStopRepository routeStopRepository,
    IRouteSegmentRepository routeSegmentRepository,
    IFareRuleRepository fareRuleRepository,
    IRoutingNetworkChangeNotifier? routingNetwork = null) : ITransportRouteService
{
    private readonly ITransportRouteRepository _transportRouteRepository = transportRouteRepository;
    private readonly ITransportModeRepository _transportModeRepository = transportModeRepository;
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

    public Task<TransportRoute?> GetLatestRouteWithPolylineAsync(
        CancellationToken cancellationToken = default) =>
        _transportRouteRepository.GetLatestWithPolylineAsync(cancellationToken);

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

    public async Task<TransportRouteCreationResult> CreateJeepneyRouteAsync(
        CreateJeepneyRouteCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreation(command, out var points);
        if (errors.Count > 0)
        {
            return TransportRouteCreationResult.Failure(
                TransportRouteCreationStatus.ValidationFailed,
                errors.ToArray());
        }

        var routeCode = command.RouteCode!.Trim();
        var existingRoute = await _transportRouteRepository.GetByRouteCodeAsync(routeCode, cancellationToken);

        var jeepneyMode = await _transportModeRepository.GetByCodeAsync(
            "JEEPNEY",
            cancellationToken);
        if (jeepneyMode is null || !jeepneyMode.IsActive)
        {
            return TransportRouteCreationResult.Failure(
                TransportRouteCreationStatus.JeepneyModeNotFound,
                "The active JEEPNEY transport mode was not found.");
        }

        var createdAt = DateTime.UtcNow;
        var route = new TransportRoute
        {
            RouteCode = routeCode,
            RouteName = command.RouteName!.Trim(),
            TransportModeId = jeepneyMode.TransportModeId,
            OriginName = command.OriginName!.Trim(),
            DestinationName = command.DestinationName!.Trim(),
            RouteDescription = NormalizeOptional(command.Description),
            EncodedPolyline = PolylineEncoder.EncodePolyline6(points),
            BaseFare = command.BaseFare,
            IsActive = true,
            CreatedAt = createdAt,
            OperatesMonday = true,
            OperatesTuesday = true,
            OperatesWednesday = true,
            OperatesThursday = true,
            OperatesFriday = true,
            OperatesSaturday = true,
            OperatesSunday = true,
            RoutePoints = points.Select((point, index) => new RoutePoint
            {
                PointOrder = index + 1,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                CreatedAt = createdAt
            }).ToList(),
            RouteWaypoints = CreateRouteWaypoints(
                command.Waypoints ?? command.Points!,
                createdAt)
        };

        // EF inserts the route and its RoutePoints in one SaveChanges
        // transaction, so a partial geometry cannot be persisted.
        var saved = existingRoute is null
            ? await _transportRouteRepository.AddAsync(route, cancellationToken)
            : await _transportRouteRepository.ReplaceAsync(existingRoute.RouteId, route, cancellationToken);
        routingNetwork?.Invalidate("active jeepney route created or replaced");
        return TransportRouteCreationResult.Success(saved);
    }

    private static List<string> ValidateCreation(
        CreateJeepneyRouteCommand command,
        out List<(double Latitude, double Longitude)> points)
    {
        points = [];
        var errors = new List<string>();
        ValidateRequired(command.RouteCode, "Route code", 50, errors);
        ValidateRequired(command.RouteName, "Route name", 200, errors);
        ValidateRequired(command.OriginName, "Origin name", 200, errors);
        ValidateRequired(command.DestinationName, "Destination name", 200, errors);

        if (command.Description?.Trim().Length > 1_000)
            errors.Add("Description cannot exceed 1000 characters.");
        if (command.BaseFare is < 0)
            errors.Add("Base fare cannot be negative.");
        if (command.Points is null || command.Points.Count < 2)
        {
            errors.Add("At least 2 route points are required.");
            return errors;
        }

        for (var index = 0; index < command.Points.Count; index++)
        {
            var coordinate = command.Points[index];
            if (coordinate is null || coordinate.Count != 2)
            {
                errors.Add($"Point {index + 1} must contain exactly two values: [latitude, longitude].");
                continue;
            }

            var latitude = coordinate[0];
            var longitude = coordinate[1];
            if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
                errors.Add($"Point {index + 1} latitude must be a finite number between -90 and 90.");
            if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
                errors.Add($"Point {index + 1} longitude must be a finite number between -180 and 180.");
            points.Add((latitude, longitude));
        }

        if (errors.Count > 0)
            points = [];
        return errors;
    }

    private static void ValidateRequired(
        string? value,
        string label,
        int maximumLength,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{label} is required.");
        else if (value.Trim().Length > maximumLength)
            errors.Add($"{label} cannot exceed {maximumLength} characters.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<RouteWaypoint> CreateRouteWaypoints(
        IReadOnlyList<List<double>> waypoints,
        DateTime createdAt) =>
        waypoints.Select((point, index) => new RouteWaypoint
        {
            WaypointOrder = index + 1,
            Latitude = point[0],
            Longitude = point[1],
            CreatedAt = createdAt
        }).ToList();

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
