using backend.Services.Destinations;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/places")]
public sealed class PlacesController(IDestinationSearchService searchService) : ControllerBase
{
    [HttpGet("search")]
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
}
