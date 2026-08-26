using System.ComponentModel.DataAnnotations;
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
public sealed class AdminTricyclePointSubmissionsController(
    IAdminTricyclePointSubmissionService service) : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses =
        ["Pending", "Approved", "Rejected", "NeedsChanges"];

    [HttpGet]
    [ProducesResponseType<AdminTricyclePointSubmissionPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminTricyclePointSubmissionPageResponse>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedStatus) &&
            !AllowedStatuses.Any(item => item.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(Error(
                "Status must be one of: Pending, Approved, Rejected, NeedsChanges."));
        }

        var result = await service.GetPageAsync(normalizedStatus, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<AdminTricyclePointSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminTricyclePointSubmissionResponse>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var submission = await service.GetByIdAsync(id, cancellationToken);
        return submission is null ? NotFound() : Ok(submission);
    }

    [HttpPut("{id:long}/review")]
    [ProducesResponseType<AdminTricyclePointSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminTricyclePointSubmissionResponse>> UpdateReview(
        long id,
        [FromBody] UpdateAdminTricyclePointSubmissionReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateReviewAsync(AdminUserId(), id, request, cancellationToken);
        return MutationResult(result);
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType<AdminTricyclePointSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminTricyclePointSubmissionResponse>> Reject(
        long id,
        [FromBody] AdminTricyclePointSubmissionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(AdminUserId(), id, request.Reason, cancellationToken);
        return MutationResult(result);
    }

    [HttpPost("{id:long}/needs-changes")]
    [ProducesResponseType<AdminTricyclePointSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminTricyclePointSubmissionResponse>> NeedsChanges(
        long id,
        [FromBody] AdminTricyclePointSubmissionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.MarkNeedsChangesAsync(AdminUserId(), id, request.Reason, cancellationToken);
        return MutationResult(result);
    }

    private ActionResult<AdminTricyclePointSubmissionResponse> MutationResult(
        AdminTricyclePointSubmissionMutationResult result)
    {
        if (result.Succeeded && result.Submission is not null)
        {
            return Ok(result.Submission);
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

    private static TricyclePointSubmissionErrorResponse Error(string message) => new([message]);
}
