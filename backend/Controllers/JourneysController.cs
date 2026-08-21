using System.Security.Claims;
using backend.Services.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/journeys")]
public sealed class JourneysController(IJourneyPlanningFacadeService journeys) : ControllerBase
{
    [HttpPost("plan")]
    [AllowAnonymous]
    public async Task<IActionResult> Plan(
        JourneyPlanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await journeys.PlanAsync(UserId(), request, cancellationToken));
        }
        catch (RoutingValidationException exception)
        {
            return BadRequest(new { error = exception.ErrorCode, message = exception.Message });
        }
    }

    private Guid UserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}
