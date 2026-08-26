using backend.Helpers;
using backend.Models.Database;
using backend.Models.JeepneyRouteManagement;
using backend.Repositories;

namespace backend.Services.Transportation;

public sealed class AdminJeepneyRouteManagementService(
    ITransportRouteRepository routeRepository,
    ITransportModeRepository transportModeRepository) : IAdminJeepneyRouteManagementService
{
    public async Task<IReadOnlyList<AdminJeepneyRouteResponse>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default)
    {
        if (!includeActive && !includeDrafts)
            return [];

        var routes = await routeRepository.GetAllByTransportModeCodeForAdminAsync(
            "JEEPNEY",
            includeActive,
            includeDrafts,
            cancellationToken);

        return routes.Select(Map).ToArray();
    }

    public async Task<AdminJeepneyRouteResponse?> GetByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0) return null;

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route)) return null;
        return Map(route);
    }

    public async Task<AdminJeepneyRouteGeometryResponse?> GetGeometryAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0) return null;

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route)) return null;
        return MapGeometry(route);
    }

    public async Task<AdminJeepneyRouteMutationResult> CreateDraftAsync(
        AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                errors.ToArray());

        var code = request.RouteCode!.Trim();
        var existing = await routeRepository.GetByRouteCodeAsync(code, cancellationToken);
        if (existing is not null)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.Conflict,
                $"Route code '{code}' is already in use.");

        var jeepneyMode = await transportModeRepository.GetByCodeAsync("JEEPNEY", cancellationToken);
        if (jeepneyMode is null || !jeepneyMode.IsActive)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.JeepneyModeNotFound,
                "The active JEEPNEY transport mode was not found.");

        var route = new TransportRoute
        {
            RouteCode = code,
            RouteName = request.RouteName!.Trim(),
            TransportModeId = jeepneyMode.TransportModeId,
            OriginName = request.OriginName!.Trim(),
            DestinationName = request.DestinationName!.Trim(),
            DirectionName = Normalize(request.DirectionName),
            OperatorName = Normalize(request.OperatorName),
            RouteDescription = Normalize(request.Description),
            BaseFare = request.BaseFare,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            OperatesMonday = true,
            OperatesTuesday = true,
            OperatesWednesday = true,
            OperatesThursday = true,
            OperatesFriday = true,
            OperatesSaturday = true,
            OperatesSunday = true
        };

        var saved = await routeRepository.AddAsync(route, cancellationToken);
        return AdminJeepneyRouteMutationResult.Success(Map(saved));
    }

    public async Task<AdminJeepneyRouteMutationResult> UpdateDraftAsync(
        long routeId,
        AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        var errors = Validate(request);
        if (errors.Count > 0)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                errors.ToArray());

        var route = await routeRepository.GetTrackedByIdAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route))
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        if (route.IsActive)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "Published jeepney routes cannot be edited by the draft foundation. Use the route editor workflow.");

        var code = request.RouteCode!.Trim();
        var codeOwner = await routeRepository.GetByRouteCodeAsync(code, cancellationToken);
        if (codeOwner is not null && codeOwner.RouteId != routeId)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.Conflict,
                $"Route code '{code}' is already in use.");

        route.RouteCode = code;
        route.RouteName = request.RouteName!.Trim();
        route.OriginName = request.OriginName!.Trim();
        route.DestinationName = request.DestinationName!.Trim();
        route.DirectionName = Normalize(request.DirectionName);
        route.OperatorName = Normalize(request.OperatorName);
        route.RouteDescription = Normalize(request.Description);
        route.BaseFare = request.BaseFare;
        route.UpdatedAt = DateTime.UtcNow;

        var saved = await routeRepository.UpdateAsync(route, cancellationToken);
        var withPoints = await routeRepository.GetByIdWithPointsForAdminAsync(saved.RouteId, cancellationToken)
            ?? saved;
        return AdminJeepneyRouteMutationResult.Success(Map(withPoints));
    }

    public async Task<AdminJeepneyRouteGeometryMutationResult> ReplaceDraftGeometryAsync(
        long routeId,
        AdminJeepneyRouteGeometryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        var errors = ValidateGeometry(request, out var points);
        if (errors.Count > 0)
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                errors.ToArray());

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route))
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        if (route.IsActive)
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "Published jeepney routes cannot be replotted. Create or edit an inactive draft instead.");

        var createdAt = DateTime.UtcNow;
        var routePoints = points.Select((point, index) => new RoutePoint
        {
            PointOrder = index + 1,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            CreatedAt = createdAt
        }).ToList();
        var waypoints = points.Select((point, index) => new RouteWaypoint
        {
            WaypointOrder = index + 1,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            CreatedAt = createdAt
        }).ToList();
        var encodedPolyline = PolylineEncoder.EncodePolyline6(points);

        var saved = await routeRepository.ReplaceDraftGeometryAsync(
            routeId,
            routePoints,
            waypoints,
            encodedPolyline,
            cancellationToken);

        if (saved is null)
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "The route is no longer an editable draft. Refresh the route before plotting again.");

        return AdminJeepneyRouteGeometryMutationResult.Success(MapGeometry(saved));
    }

    private static bool IsJeepney(TransportRoute route) =>
        string.Equals(route.TransportMode?.Code, "JEEPNEY", StringComparison.OrdinalIgnoreCase);

    private static List<string> Validate(AdminJeepneyRouteMutationRequest request)
    {
        var errors = new List<string>();
        Required(request.RouteCode, "Route code", 50, errors);
        Required(request.RouteName, "Route name", 200, errors);
        Required(request.OriginName, "Origin name", 200, errors);
        Required(request.DestinationName, "Destination name", 200, errors);
        OptionalLength(request.DirectionName, "Direction name", 100, errors);
        OptionalLength(request.OperatorName, "Operator name", 200, errors);
        OptionalLength(request.Description, "Description", 1000, errors);
        if (request.BaseFare is < 0)
            errors.Add("Base fare cannot be negative.");
        return errors;
    }

    private static List<string> ValidateGeometry(
        AdminJeepneyRouteGeometryRequest request,
        out List<(double Latitude, double Longitude)> points)
    {
        points = [];
        var errors = new List<string>();
        if (request.Points is null || request.Points.Count < 2)
        {
            errors.Add("At least 2 route points are required.");
            return errors;
        }

        if (request.Points.Count > 5000)
        {
            errors.Add("A route cannot contain more than 5000 plotted points.");
            return errors;
        }

        for (var index = 0; index < request.Points.Count; index++)
        {
            var point = request.Points[index];
            if (!double.IsFinite(point.Latitude) || point.Latitude is < -90 or > 90)
                errors.Add($"Point {index + 1} latitude must be a finite number between -90 and 90.");
            if (!double.IsFinite(point.Longitude) || point.Longitude is < -180 or > 180)
                errors.Add($"Point {index + 1} longitude must be a finite number between -180 and 180.");

            points.Add((point.Latitude, point.Longitude));
        }

        if (errors.Count > 0)
            points = [];
        return errors;
    }

    private static void Required(string? value, string label, int max, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{label} is required.");
        else if (value.Trim().Length > max) errors.Add($"{label} cannot exceed {max} characters.");
    }

    private static void OptionalLength(string? value, string label, int max, ICollection<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > max)
            errors.Add($"{label} cannot exceed {max} characters.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminJeepneyRouteResponse Map(TransportRoute route) => new(
        route.RouteId,
        route.RouteCode,
        route.RouteName,
        route.OriginName,
        route.DestinationName,
        route.DirectionName,
        route.OperatorName,
        route.RouteDescription,
        route.BaseFare,
        route.IsActive,
        route.RoutePoints?.Count ?? 0,
        route.RouteWaypoints?.Count ?? 0,
        !string.IsNullOrWhiteSpace(route.EncodedPolyline),
        route.CreatedAt,
        route.UpdatedAt);

    private static AdminJeepneyRouteGeometryResponse MapGeometry(TransportRoute route) => new(
        route.RouteId,
        route.RouteCode,
        route.RouteName,
        route.OriginName,
        route.DestinationName,
        route.IsActive,
        route.EncodedPolyline,
        route.RoutePoints
            .OrderBy(point => point.PointOrder)
            .Select(point => new AdminJeepneyRouteGeometryPointResponse(
                point.PointOrder,
                point.Latitude,
                point.Longitude))
            .ToArray(),
        route.UpdatedAt);
}
