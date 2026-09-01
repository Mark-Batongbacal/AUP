using System.Security.Claims;
using backend.Authentication;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/tricycle-point-submissions")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = "Admin")]
public sealed class AdminTricyclePointSubmissionPublishingController(
    ITricyclePointSubmissionPublishingService service) : ControllerBase
{
    [HttpPost("{id:long}/approve")]
    [ProducesResponseType<TricyclePointSubmissionPublishResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TricyclePointSubmissionPublishResponse>> Approve(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await service.PublishAsync(AdminUserId(), id, cancellationToken);

        if (result.Succeeded && result.Publication is not null)
        {
            return Ok(result.Publication);
        }

        if (result.NotFound)
        {
            return NotFound();
        }

        var error = new TricyclePointSubmissionErrorResponse(result.Errors);
        return result.Conflict ? Conflict(error) : BadRequest(error);
    }

    private Guid AdminUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}
