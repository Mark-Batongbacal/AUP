using backend.Authentication;
using backend.Models.Database;
using backend.Models.JeepneyRouteManagement;
using backend.Services.Authentication.ApiKey;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/jeepney-routes")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = "Admin")]
public sealed class AdminJeepneyRoutesController(
    IAdminJeepneyRouteManagementService managementService,
    TukiDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminJeepneyRouteResponse>>> GetAll(
        [FromQuery] bool includeActive = true,
        [FromQuery] bool includeDrafts = true,
        CancellationToken cancellationToken = default)
    {
        var routes = await managementService.GetAllAsync(
            includeActive,
            includeDrafts,
            cancellationToken);
        return Ok(routes);
    }

    [HttpGet("archived")]
    public async Task<ActionResult<IReadOnlyList<AdminJeepneyRouteResponse>>> GetArchived(
        CancellationToken cancellationToken = default)
    {
        var routes = await dbContext.TransportRoutes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(route => route.TransportMode)
            .Include(route => route.RoutePoints)
            .Include(route => route.RouteWaypoints)
            .Where(route =>
                route.ArchivedAt != null &&
                route.TransportMode.Code == "JEEPNEY")
            .OrderBy(route => route.RouteName)
            .ThenBy(route => route.RouteCode)
            .ToListAsync(cancellationToken);

        return Ok(routes.Select(MapArchived).ToArray());
    }

    [HttpGet("{routeId:long}")]
    public async Task<ActionResult<AdminJeepneyRouteResponse>> GetById(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var route = await managementService.GetByIdAsync(routeId, cancellationToken);
        return route is null ? NotFound() : Ok(route);
    }

    [HttpGet("{routeId:long}/geometry")]
    public async Task<ActionResult<AdminJeepneyRouteGeometryResponse>> GetGeometry(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var geometry = await managementService.GetGeometryAsync(routeId, cancellationToken);
        return geometry is null ? NotFound() : Ok(geometry);
    }

    [HttpPost("{routeId:long}/valhalla/preview")]
    public async Task<ActionResult<AdminJeepneyValhallaPreviewResponse>> PreviewValhalla(
        long routeId,
        [FromBody] AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.PreviewValhallaAsync(routeId, request, cancellationToken);
        return result.Succeeded && result.Preview is not null
            ? Ok(result.Preview)
            : Failure(result);
    }

    [HttpPost("{routeId:long}/valhalla/save")]
    public async Task<ActionResult<AdminJeepneyRouteGeometryResponse>> SaveValhallaGeometry(
        long routeId,
        [FromBody] AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.SaveValhallaGeometryAsync(routeId, request, cancellationToken);
        return result.Succeeded && result.Geometry is not null
            ? Ok(result.Geometry)
            : Failure(result);
    }

    [HttpGet("{routeId:long}/publish-readiness")]
    public async Task<ActionResult<AdminJeepneyRoutePublishReadinessResponse>> GetPublishReadiness(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var readiness = await managementService.GetPublishReadinessAsync(routeId, cancellationToken);
        return readiness is null ? NotFound() : Ok(readiness);
    }

    [HttpPost]
    public async Task<ActionResult<AdminJeepneyRouteResponse>> CreateDraft(
        [FromBody] AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.CreateDraftAsync(request, cancellationToken);
        if (result.Succeeded && result.Route is not null)
        {
            return CreatedAtAction(
                nameof(GetById),
                new { routeId = result.Route.RouteId },
                result.Route);
        }

        return Failure(result);
    }

    [HttpPut("{routeId:long}")]
    public async Task<ActionResult<AdminJeepneyRouteResponse>> UpdateDraft(
        long routeId,
        [FromBody] AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.UpdateDraftAsync(routeId, request, cancellationToken);
        return result.Succeeded && result.Route is not null
            ? Ok(result.Route)
            : Failure(result);
    }

    [HttpPut("{routeId:long}/geometry")]
    public async Task<ActionResult<AdminJeepneyRouteGeometryResponse>> ReplaceDraftGeometry(
        long routeId,
        [FromBody] AdminJeepneyRouteGeometryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.ReplaceDraftGeometryAsync(routeId, request, cancellationToken);
        return result.Succeeded && result.Geometry is not null
            ? Ok(result.Geometry)
            : Failure(result);
    }

    [HttpPost("{routeId:long}/publish")]
    public async Task<ActionResult<AdminJeepneyRouteResponse>> Publish(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.PublishDraftAsync(routeId, cancellationToken);
        return result.Succeeded && result.Route is not null
            ? Ok(result.Route)
            : Failure(result);
    }

    [HttpPost("{routeId:long}/archive")]
    public async Task<IActionResult> Archive(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var route = await dbContext.TransportRoutes
            .IgnoreQueryFilters()
            .Include(item => item.TransportMode)
            .SingleOrDefaultAsync(item => item.RouteId == routeId, cancellationToken);

        if (route is null || !string.Equals(route.TransportMode.Code, "JEEPNEY", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { errors = new[] { "Jeepney route was not found." } });

        if (route.ArchivedAt.HasValue)
            return Conflict(new { errors = new[] { "This jeepney route is already archived." } });

        var archivedAt = DateTime.UtcNow;
        route.ArchivedAt = archivedAt;
        route.UpdatedAt = archivedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { routeId, archivedAt, message = "Jeepney route archived." });
    }

    [HttpPost("{routeId:long}/restore")]
    public async Task<IActionResult> Restore(
        long routeId,
        CancellationToken cancellationToken = default)
    {
        var route = await dbContext.TransportRoutes
            .IgnoreQueryFilters()
            .Include(item => item.TransportMode)
            .SingleOrDefaultAsync(item => item.RouteId == routeId, cancellationToken);

        if (route is null || !string.Equals(route.TransportMode.Code, "JEEPNEY", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { errors = new[] { "Jeepney route was not found." } });

        if (!route.ArchivedAt.HasValue)
            return Conflict(new { errors = new[] { "This jeepney route is not archived." } });

        route.ArchivedAt = null;
        route.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { routeId, isActive = route.IsActive, message = "Jeepney route restored." });
    }

    private static AdminJeepneyRouteResponse MapArchived(TransportRoute route) => new(
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
        route.RoutePoints.Count,
        route.RouteWaypoints.Count,
        !string.IsNullOrWhiteSpace(route.EncodedPolyline),
        route.CreatedAt,
        route.UpdatedAt)
    {
        ArchivedAt = route.ArchivedAt
    };

    private ActionResult<AdminJeepneyRouteResponse> Failure(AdminJeepneyRouteMutationResult result)
    {
        var error = new { errors = result.Errors };
        return result.Status switch
        {
            AdminJeepneyRouteMutationStatus.NotFound => NotFound(error),
            AdminJeepneyRouteMutationStatus.Conflict => Conflict(error),
            AdminJeepneyRouteMutationStatus.ActiveRouteLocked => Conflict(error),
            AdminJeepneyRouteMutationStatus.JeepneyModeNotFound =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, error),
            AdminJeepneyRouteMutationStatus.UpstreamFailure =>
                StatusCode(StatusCodes.Status502BadGateway, error),
            _ => BadRequest(error)
        };
    }

    private ActionResult<AdminJeepneyRouteGeometryResponse> Failure(AdminJeepneyRouteGeometryMutationResult result)
    {
        var error = new { errors = result.Errors };
        return result.Status switch
        {
            AdminJeepneyRouteMutationStatus.NotFound => NotFound(error),
            AdminJeepneyRouteMutationStatus.Conflict => Conflict(error),
            AdminJeepneyRouteMutationStatus.ActiveRouteLocked => Conflict(error),
            AdminJeepneyRouteMutationStatus.UpstreamFailure =>
                StatusCode(StatusCodes.Status502BadGateway, error),
            _ => BadRequest(error)
        };
    }

    private ActionResult<AdminJeepneyValhallaPreviewResponse> Failure(AdminJeepneyValhallaPreviewResult result)
    {
        var error = new { errors = result.Errors };
        return result.Status switch
        {
            AdminJeepneyRouteMutationStatus.NotFound => NotFound(error),
            AdminJeepneyRouteMutationStatus.Conflict => Conflict(error),
            AdminJeepneyRouteMutationStatus.ActiveRouteLocked => Conflict(error),
            AdminJeepneyRouteMutationStatus.UpstreamFailure =>
                StatusCode(StatusCodes.Status502BadGateway, error),
            _ => BadRequest(error)
        };
    }
}
