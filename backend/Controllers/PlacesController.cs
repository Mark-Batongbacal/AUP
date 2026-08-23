using backend.Services.Destinations;
using backend.Services.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController(
    IDestinationSearchService searchService,
    IReverseGeocodingService reverseGeocoding,
    ITripAreaValidator areaValidator) : ControllerBase
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

    [HttpGet("search/more")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchMore(
        [FromQuery] string q,
        [FromQuery] double? focusLat,
        [FromQuery] double? focusLon,
        CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < 2)
            return BadRequest(new { error = "INVALID_QUERY", message = "Enter at least two characters." });

        try
        {
            var results = await GooglePlacesSearchClient.SearchAsync(
                query, new(focusLat, focusLon), cancellationToken);
            var supported = results
                .Where(result => areaValidator.ValidateCoordinate(
                    result.Latitude, result.Longitude).IsValid)
                .ToList();
            return Ok(supported);
        }
        catch (DestinationProviderUnavailableException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "GOOGLE_PLACES_UNAVAILABLE", message = exception.Message });
        }
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
