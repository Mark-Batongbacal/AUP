using backend.Services.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.Test;

[ApiController]
[Route("api/test/jeepney")]
public class RoutingController : ControllerBase
{
    private readonly IRoutingService _routingService;

    public RoutingController(
        IRoutingService routingService)
    {
        _routingService = routingService;
    }


    [HttpGet("nearby")]
    [AllowAnonymous]
    public async Task<IActionResult> FindNearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken cancellationToken)
    {
        var results =
            await _routingService.FindNearbyRoutesAsync(
                lat,
                lon,
                cancellationToken);

        return Ok(results);
    }
    [HttpGet("plan")]
    [AllowAnonymous]
    public async Task<IActionResult> PlanRoute(
        [FromQuery] double originLat,
        [FromQuery] double originLon,
        [FromQuery] double destinationLat,
        [FromQuery] double destinationLon,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _routingService.PlanTripsAsync(
                originLat, originLon, destinationLat, destinationLon, cancellationToken);
            return Ok(result);
        }
        catch (RoutingValidationException exception)
        {
            return BadRequest(new { error = exception.ErrorCode, message = exception.Message });
        }
    }
}
