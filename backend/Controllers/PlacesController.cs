using backend.Services.Destinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController(
    IDestinationSearchService searchService,
    IReverseGeocodingService reverseGeocoding) : ControllerBase
{
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] double? focusLat,
        [FromQuery] double? focusLon,
        CancellationToken cancellationToken)
    {
        var response = await searchService.SearchAsync(
            q ?? string.Empty, new(focusLat, focusLon), cancellationToken);
        return response.Error is null ? Ok(response.Results) : BadRequest(response);
    }

    [HttpGet("reverse")]
    [AllowAnonymous]
    public async Task<IActionResult> Reverse(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken cancellationToken)
    {
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            return BadRequest(new { error = "INVALID_COORDINATES" });

        try
        {
            var result = await reverseGeocoding.ReverseAsync(lat, lon, cancellationToken);
            return result is null ? NotFound(new { error = "PLACE_NOT_FOUND" }) : Ok(result);
        }
        catch (DestinationProviderUnavailableException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
    }
}
