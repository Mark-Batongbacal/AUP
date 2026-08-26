using System.Security.Claims;
using backend.Authentication;
using backend.Models.TricyclePointSubmissions;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/tricycle-point-submissions")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public sealed class TricyclePointSubmissionsController(ITricyclePointSubmissionService service)
    : ControllerBase
{
    private const string GuestRole = "Guest";

    [HttpPost]
    [ProducesResponseType<TricyclePointSubmissionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TricyclePointSubmissionResponse>> Create(
        [FromBody] CreateTricyclePointSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (User.IsInRole(GuestRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Error("Sign in with a registered TUKI account to suggest a tricycle/TODA point."));
        }

        var result = await service.CreateAsync(userId, request, cancellationToken);
        if (!result.Succeeded || result.Submission is null)
        {
            return BadRequest(new TricyclePointSubmissionErrorResponse(result.Errors));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Submission.TricyclePointSubmissionId },
            result.Submission);
    }

    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<TricyclePointSubmissionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TricyclePointSubmissionResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var submissions = await service.GetByUserAsync(userId, cancellationToken);
        return Ok(submissions);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<TricyclePointSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TricyclePointSubmissionResponse>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var submission = await service.GetByIdForUserAsync(userId, id, cancellationToken);
        return submission is null ? NotFound() : Ok(submission);
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static TricyclePointSubmissionErrorResponse Error(string message) => new([message]);
}
