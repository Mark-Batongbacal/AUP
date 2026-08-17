using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/transport-routes")]
public sealed class TransportRoutesController(ITransportRouteService transportRouteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransportRouteListItemDto>>> GetActiveRoutes(
        CancellationToken cancellationToken)
    {
        var routes = await transportRouteService.GetAllActiveRoutesAsync(cancellationToken);
        var result = routes
            .Select(route => new TransportRouteListItemDto(
                route.RouteId,
                route.RouteCode,
                route.RouteName,
                route.IsActive))
            .ToList();

        return Ok(result);
    }
}

public sealed record TransportRouteListItemDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    bool IsActive);
