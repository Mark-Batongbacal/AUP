using backend.Services.Route;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.Test;

[ApiController]
[Route("api/test/jeepney")]
public class JeepneyRoutingTestController : ControllerBase
{
    private readonly IJeepneyRoutingService _jeepneyRoutingService;

    public JeepneyRoutingTestController(
        IJeepneyRoutingService jeepneyRoutingService)
    {
        _jeepneyRoutingService = jeepneyRoutingService;
    }
    

    [HttpGet("nearby")]
    public async Task<IActionResult> FindNearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken cancellationToken)
    {
        var results =
            await _jeepneyRoutingService.FindNearbyRoutesAsync(
                lat,
                lon,
                cancellationToken);

        return Ok(results);
    }

    [HttpGet("plan")]
    public async Task<IActionResult> PlanRoute(
        [FromQuery] double originLat,
        [FromQuery] double originLon,
        [FromQuery] double destinationLat,
        [FromQuery] double destinationLon,
        CancellationToken cancellationToken)
    {
        var result =
            await _jeepneyRoutingService.PlanTripsAsync(
                originLat,
                originLon,
                destinationLat,
                destinationLon,
                cancellationToken);

        return Ok(result);
    }
}