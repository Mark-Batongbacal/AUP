using backend.Services.Route;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/test/valhalla")]
public class ValhallaTestController : ControllerBase
{
    private readonly IValhallaService _valhallaService;

    public ValhallaTestController(IValhallaService valhallaService)
    {
        _valhallaService = valhallaService;
    }

    [HttpGet("route")]
    public async Task<IActionResult> GetRoute(
        [FromQuery] double startLat,
        [FromQuery] double startLon,
        [FromQuery] double endLat,
        [FromQuery] double endLon,
        CancellationToken cancellationToken)
    {
        try
        {
            var route = await _valhallaService.GetRouteAsync(
                startLat,
                startLon,
                endLat,
                endLon,
                "pedestrian",
                cancellationToken);

            return Ok(route);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    error = "Unable to communicate with Valhalla.",
                    message = ex.Message
                });
        }
    }
}