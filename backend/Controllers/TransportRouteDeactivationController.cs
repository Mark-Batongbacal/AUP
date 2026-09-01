using backend.Repositories;
using backend.Services.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/transport-routes")]
public sealed class TransportRouteDeactivationController(
    ITransportRouteRepository transportRouteRepository,
    IRoutingNetworkChangeNotifier? routingNetwork = null) : ControllerBase
{
    [HttpDelete("{routeId:long}")]
    [Authorize]
    public async Task<IActionResult> DeactivateRoute(
        [FromRoute] long routeId,
        CancellationToken cancellationToken)
    {
        if (routeId <= 0)
        {
            return BadRequest(new RoutePointErrorResponseDto(["Route id must be greater than zero."]));
        }

        var deactivated = await transportRouteRepository.DeactivateAsync(routeId, cancellationToken);
        if (!deactivated)
        {
            return NotFound(new RoutePointErrorResponseDto(["Transport route was not found."]));
        }

        routingNetwork?.Invalidate("transport route deactivated");
        return NoContent();
    }

    [HttpPatch("{routeId:long}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateRoute(
        [FromRoute] long routeId,
        CancellationToken cancellationToken)
    {
        if (routeId <= 0)
        {
            return BadRequest(new RoutePointErrorResponseDto(["Route id must be greater than zero."]));
        }

        var activated = await transportRouteRepository.ActivateAsync(routeId, cancellationToken);
        if (!activated)
        {
            return NotFound(new RoutePointErrorResponseDto(["Transport route was not found."]));
        }

        routingNetwork?.Invalidate("transport route activated");
        return NoContent();
    }
}
