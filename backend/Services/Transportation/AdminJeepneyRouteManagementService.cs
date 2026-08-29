using backend.Helpers;
using backend.Models.Database;
using backend.Models.JeepneyRouteManagement;
using backend.Repositories;

namespace backend.Services.Transportation;

public sealed class AdminJeepneyRouteManagementService(
    ITransportRouteRepository routeRepository,
    ITransportModeRepository transportModeRepository,
    IRouteGeneratorService? routeGeneratorService = null) : IAdminJeepneyRouteManagementService
{
    public async Task<IReadOnlyList<AdminJeepneyRouteResponse>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default)
    {
        if (!includeActive && !includeDrafts)
            return [];

        var routes = await routeRepository.GetAdminSummariesByTransportModeCodeAsync(
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

    public async Task<AdminJeepneyRoutePublishReadinessResponse?> GetPublishReadinessAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0) return null;

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route)) return null;
        return BuildReadiness(route);
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

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route))
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        if (route.IsActive)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "Published jeepney routes cannot be edited by the draft workflow.");

        var code = request.RouteCode!.Trim();
        var codeOwner = await routeRepository.GetByRouteCodeAsync(code, cancellationToken);
        if (codeOwner is not null && codeOwner.RouteId != routeId)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.Conflict,
                $"Route code '{code}' is already in use.");

        var saved = await routeRepository.UpdateJeepneyDraftMetadataAsync(
            routeId,
            code,
            request.RouteName!.Trim(),
            request.OriginName!.Trim(),
            request.DestinationName!.Trim(),
            Normalize(request.DirectionName),
            Normalize(request.OperatorName),
            Normalize(request.Description),
            request.BaseFare,
            cancellationToken);

        if (saved is null)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "The route is no longer an editable draft. Refresh its details before saving again.");

        return AdminJeepneyRouteMutationResult.Success(Map(saved));
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

    public async Task<AdminJeepneyValhallaPreviewResult> PreviewValhallaAsync(
        long routeId,
        AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default)
    {
        var preparation = await PrepareValhallaAsync(routeId, request, cancellationToken);
        if (!preparation.Succeeded)
            return AdminJeepneyValhallaPreviewResult.Failure(preparation.Status, preparation.Errors.ToArray());

        var generated = preparation.GeneratedPoints!;
        var preview = new AdminJeepneyValhallaPreviewResponse(
            routeId,
            preparation.Waypoints!
                .Select((point, index) => new AdminJeepneyRouteGeometryPointResponse(index + 1, point.Latitude, point.Longitude))
                .ToArray(),
            generated
                .Select((point, index) => new AdminJeepneyRouteGeometryPointResponse(index + 1, point.Latitude, point.Longitude))
                .ToArray(),
            PolylineEncoder.EncodePolyline6(generated));

        return AdminJeepneyValhallaPreviewResult.Success(preview);
    }

    public async Task<AdminJeepneyRouteGeometryMutationResult> SaveValhallaGeometryAsync(
        long routeId,
        AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default)
    {
        var preparation = await PrepareValhallaAsync(routeId, request, cancellationToken);
        if (!preparation.Succeeded)
            return AdminJeepneyRouteGeometryMutationResult.Failure(preparation.Status, preparation.Errors.ToArray());

        var createdAt = DateTime.UtcNow;
        var generated = preparation.GeneratedPoints!;
        var waypoints = preparation.Waypoints!;

        var routePoints = generated.Select((point, index) => new RoutePoint
        {
            PointOrder = index + 1,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            CreatedAt = createdAt
        }).ToList();
        var routeWaypoints = waypoints.Select((point, index) => new RouteWaypoint
        {
            WaypointOrder = index + 1,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            CreatedAt = createdAt
        }).ToList();
        var encodedPolyline = PolylineEncoder.EncodePolyline6(generated);

        var saved = await routeRepository.ReplaceDraftGeometryAsync(
            routeId,
            routePoints,
            routeWaypoints,
            encodedPolyline,
            cancellationToken);

        if (saved is null)
            return AdminJeepneyRouteGeometryMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "The route is no longer an editable draft. Refresh the route before saving the generated geometry.");

        return AdminJeepneyRouteGeometryMutationResult.Success(MapGeometry(saved));
    }

    public async Task<AdminJeepneyRouteMutationResult> PublishDraftAsync(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        if (routeId <= 0)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route))
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        if (route.IsActive)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.Conflict,
                "This jeepney route is already published.");

        var readiness = BuildReadiness(route);
        if (!readiness.CanPublish)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                readiness.Checks.Where(check => !check.IsReady).Select(check => check.Message).ToArray());

        var published = await routeRepository.PublishReadyJeepneyDraftAsync(routeId, cancellationToken);
        if (published is null)
            return AdminJeepneyRouteMutationResult.Failure(
                AdminJeepneyRouteMutationStatus.Conflict,
                "The route changed or is no longer publishable. Refresh its details and verify readiness again.");

        return AdminJeepneyRouteMutationResult.Success(Map(published));
    }

    private async Task<ValhallaPreparationResult> PrepareValhallaAsync(
        long routeId,
        AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken)
    {
        if (routeId <= 0)
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        var errors = ValidateWaypoints(request, out var waypoints);
        if (errors.Count > 0)
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                errors.ToArray());

        var route = await routeRepository.GetByIdWithPointsForAdminAsync(routeId, cancellationToken);
        if (route is null || !IsJeepney(route))
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.NotFound,
                "Jeepney route was not found.");

        if (route.IsActive)
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.ActiveRouteLocked,
                "Published jeepney routes cannot be regenerated. Create or edit an inactive draft instead.");

        if (routeGeneratorService is null)
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.UpstreamFailure,
                "Valhalla route generation is not configured for this service instance.");

        try
        {
            var input = waypoints
                .Select(point => new List<double> { point.Latitude, point.Longitude })
                .ToList();
            var generatedLists = await routeGeneratorService.GenerateAsync(input, cancellationToken);
            var generated = generatedLists
                .Select(point => (Latitude: point[0], Longitude: point[1]))
                .ToList();

            if (generated.Count < 2)
                return ValhallaPreparationResult.Failure(
                    AdminJeepneyRouteMutationStatus.UpstreamFailure,
                    "Valhalla returned no usable route geometry.");

            if (generated.Count > 5000)
                return ValhallaPreparationResult.Failure(
                    AdminJeepneyRouteMutationStatus.ValidationFailed,
                    "Valhalla generated more than 5000 route points. Reduce or adjust the waypoint set.");

            return ValhallaPreparationResult.Success(waypoints, generated);
        }
        catch (HttpRequestException exception)
        {
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.UpstreamFailure,
                $"Valhalla could not generate the route: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.UpstreamFailure,
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            return ValhallaPreparationResult.Failure(
                AdminJeepneyRouteMutationStatus.ValidationFailed,
                exception.Message);
        }
    }

    private static bool IsJeepney(TransportRoute route) =>
        string.Equals(route.TransportMode?.Code, "JEEPNEY", StringComparison.OrdinalIgnoreCase);

    private static AdminJeepneyRoutePublishReadinessResponse BuildReadiness(TransportRoute route)
    {
        var metadataReady =
            !string.IsNullOrWhiteSpace(route.RouteCode) &&
            !string.IsNullOrWhiteSpace(route.RouteName) &&
            !string.IsNullOrWhiteSpace(route.OriginName) &&
            !string.IsNullOrWhiteSpace(route.DestinationName);
        var pointsReady = route.RoutePoints.Count >= 2 && route.RoutePoints.All(point =>
            double.IsFinite(point.Latitude) && point.Latitude is >= -90 and <= 90 &&
            double.IsFinite(point.Longitude) && point.Longitude is >= -180 and <= 180);
        var pointOrderReady = route.RoutePoints
            .OrderBy(point => point.PointOrder)
            .Select((point, index) => point.PointOrder == index + 1)
            .All(value => value);
        var waypointsReady = route.RouteWaypoints.Count >= 2 && route.RouteWaypoints.All(point =>
            double.IsFinite(point.Latitude) && point.Latitude is >= -90 and <= 90 &&
            double.IsFinite(point.Longitude) && point.Longitude is >= -180 and <= 180);
        var waypointOrderReady = route.RouteWaypoints
            .OrderBy(point => point.WaypointOrder)
            .Select((point, index) => point.WaypointOrder == index + 1)
            .All(value => value);
        var polylineReady = !string.IsNullOrWhiteSpace(route.EncodedPolyline);

        var checks = new[]
        {
            new AdminJeepneyRouteReadinessCheckResponse(
                "metadata",
                "Route metadata",
                metadataReady,
                metadataReady
                    ? "Route code, name, origin, and destination are complete."
                    : "Complete the route code, route name, origin, and destination before publishing."),
            new AdminJeepneyRouteReadinessCheckResponse(
                "points",
                "Ordered route points",
                pointsReady && pointOrderReady,
                pointsReady && pointOrderReady
                    ? $"{route.RoutePoints.Count} valid ordered route points are stored."
                    : "Store at least two valid route points with continuous PointOrder values starting at 1."),
            new AdminJeepneyRouteReadinessCheckResponse(
                "waypoints",
                "Ordered waypoints",
                waypointsReady && waypointOrderReady,
                waypointsReady && waypointOrderReady
                    ? $"{route.RouteWaypoints.Count} valid ordered waypoints are stored."
                    : "Store at least two valid route waypoints with continuous WaypointOrder values starting at 1."),
            new AdminJeepneyRouteReadinessCheckResponse(
                "polyline",
                "Encoded route polyline",
                polylineReady,
                polylineReady
                    ? "Encoded Polyline6 geometry is available."
                    : "Save route geometry so an encoded polyline is generated before publishing.")
        };

        return new AdminJeepneyRoutePublishReadinessResponse(
            route.RouteId,
            route.IsActive,
            !route.IsActive && checks.All(check => check.IsReady),
            checks);
    }

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

    private static List<string> ValidateWaypoints(
        AdminJeepneyValhallaRequest request,
        out List<(double Latitude, double Longitude)> waypoints)
    {
        waypoints = [];
        var errors = new List<string>();
        if (request.Waypoints is null || request.Waypoints.Count < 2)
        {
            errors.Add("At least 2 ordered waypoints are required for Valhalla generation.");
            return errors;
        }

        if (request.Waypoints.Count > 100)
        {
            errors.Add("Valhalla preview accepts at most 100 ordered waypoints. Use selected anchors rather than every route geometry point.");
            return errors;
        }

        for (var index = 0; index < request.Waypoints.Count; index++)
        {
            var point = request.Waypoints[index];
            if (!double.IsFinite(point.Latitude) || point.Latitude is < -90 or > 90)
                errors.Add($"Waypoint {index + 1} latitude must be a finite number between -90 and 90.");
            if (!double.IsFinite(point.Longitude) || point.Longitude is < -180 or > 180)
                errors.Add($"Waypoint {index + 1} longitude must be a finite number between -180 and 180.");
            waypoints.Add((point.Latitude, point.Longitude));
        }

        if (errors.Count > 0)
            waypoints = [];
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

    private static AdminJeepneyRouteResponse Map(TransportRouteAdminSummary route) => new(
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
        route.PointCount,
        route.WaypointCount,
        route.HasPolyline,
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

    private sealed record ValhallaPreparationResult(
        AdminJeepneyRouteMutationStatus Status,
        IReadOnlyList<string> Errors,
        List<(double Latitude, double Longitude)>? Waypoints,
        List<(double Latitude, double Longitude)>? GeneratedPoints)
    {
        public bool Succeeded => Status == AdminJeepneyRouteMutationStatus.Success;

        public static ValhallaPreparationResult Success(
            List<(double Latitude, double Longitude)> waypoints,
            List<(double Latitude, double Longitude)> generatedPoints) =>
            new(AdminJeepneyRouteMutationStatus.Success, [], waypoints, generatedPoints);

        public static ValhallaPreparationResult Failure(
            AdminJeepneyRouteMutationStatus status,
            params string[] errors) => new(status, errors, null, null);
    }
}
