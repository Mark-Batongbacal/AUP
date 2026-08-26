using backend.Authentication;
using backend.Models.JeepneyRouteManagement;
using backend.Services.Authentication.ApiKey;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/jeepney-routes")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = "Admin")]
public sealed class AdminJeepneyRoutesController(
    IAdminJeepneyRouteManagementService managementService) : ControllerBase
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
            _ => BadRequest(error)
        };
    }
}
