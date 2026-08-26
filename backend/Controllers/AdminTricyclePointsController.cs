using backend.Authentication;
using backend.Models.TricyclePointManagement;
using backend.Services.Authentication.ApiKey;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/tricycle-points")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = "Admin")]
public sealed class AdminTricyclePointsController(
    IAdminTricyclePointManagementService managementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminTricyclePointResponse>>> GetAll(
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        var points = await managementService.GetAllAsync(includeArchived, cancellationToken);
        return Ok(points);
    }

    [HttpGet("{tricyclePointId:long}")]
    public async Task<ActionResult<AdminTricyclePointResponse>> GetById(
        long tricyclePointId,
        CancellationToken cancellationToken = default)
    {
        var point = await managementService.GetByIdAsync(tricyclePointId, cancellationToken);
        return point is null ? NotFound() : Ok(point);
    }

    [HttpGet("duplicates")]
    public async Task<ActionResult<IReadOnlyList<TricyclePointDuplicateWarning>>> GetDuplicates(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] long? excludeTricyclePointId = null,
        [FromQuery] double thresholdMeters = 75,
        CancellationToken cancellationToken = default)
    {
        if (latitude is null || longitude is null ||
            !double.IsFinite(latitude.Value) || latitude.Value is < -90 or > 90 ||
            !double.IsFinite(longitude.Value) || longitude.Value is < -180 or > 180)
        {
            return BadRequest(new { errors = new[] { "Valid latitude and longitude are required." } });
        }

        var warnings = await managementService.GetDuplicateWarningsAsync(
            latitude.Value,
            longitude.Value,
            excludeTricyclePointId,
            thresholdMeters,
            cancellationToken);
        return Ok(warnings);
    }

    [HttpPost]
    public async Task<ActionResult<AdminTricyclePointMutationResponse>> Create(
        [FromBody] AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.CreateAsync(request, cancellationToken);
        return ToActionResult(result, created: true);
    }

    [HttpPut("{tricyclePointId:long}")]
    public async Task<ActionResult<AdminTricyclePointMutationResponse>> Update(
        long tricyclePointId,
        [FromBody] AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.UpdateAsync(tricyclePointId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tricyclePointId:long}/archive")]
    public async Task<ActionResult<AdminTricyclePointMutationResponse>> Archive(
        long tricyclePointId,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.SetActiveAsync(
            tricyclePointId,
            isActive: false,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tricyclePointId:long}/restore")]
    public async Task<ActionResult<AdminTricyclePointMutationResponse>> Restore(
        long tricyclePointId,
        CancellationToken cancellationToken = default)
    {
        var result = await managementService.SetActiveAsync(
            tricyclePointId,
            isActive: true,
            cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<AdminTricyclePointMutationResponse> ToActionResult(
        AdminTricyclePointMutationResult result,
        bool created = false)
    {
        if (result.Succeeded && result.Response is not null)
        {
            if (created)
            {
                return CreatedAtAction(
                    nameof(GetById),
                    new { tricyclePointId = result.Response.Point.TricyclePointId },
                    result.Response);
            }

            return Ok(result.Response);
        }

        if (result.NotFound)
        {
            return NotFound();
        }

        var error = new { errors = result.Errors };
        return result.Conflict ? Conflict(error) : BadRequest(error);
    }
}
