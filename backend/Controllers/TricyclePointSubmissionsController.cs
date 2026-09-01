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
public sealed class TricyclePointSubmissionsController(
    ITricyclePointSubmissionService service,
    ITricycleProofStorage? proofStorage = null) : ControllerBase
{
    private const string GuestRole = "Guest";
    private const string ProofRoutePrefix = "/api/tricycle-point-submissions/proof/";

    [HttpPost("proof")]
    [RequestSizeLimit(TricycleProofFileValidator.MaxFileBytes + 256 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TricyclePointSubmissionErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadProof(IFormFile? image, CancellationToken cancellationToken)
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
                Error("Sign in with a registered TUKI account to upload tricycle/TODA proof."));
        }

        if (proofStorage is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Error("Proof image storage is unavailable."));
        }

        if (image is null || image.Length <= 0)
        {
            return BadRequest(Error("Choose a proof image to upload."));
        }

        if (image.Length > TricycleProofFileValidator.MaxFileBytes)
        {
            return BadRequest(Error("Proof images must be 10 MB or smaller."));
        }

        await using var buffer = new MemoryStream((int)image.Length);
        await image.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var detected = TricycleProofFileValidator.Detect(bytes);
        if (detected is null)
        {
            return BadRequest(Error("Proof images must be JPEG, PNG, or WebP files."));
        }

        var stored = await proofStorage.SaveAsync(
            userId,
            bytes,
            detected.Value.Extension,
            detected.Value.ContentType,
            cancellationToken);

        return Ok(new TricycleProofUploadResponse($"{ProofRoutePrefix}{stored.FileName}"));
    }

    [HttpGet("proof/{fileName}")]
    public async Task<IActionResult> GetProof(string fileName, CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (proofStorage is null)
        {
            return NotFound();
        }

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && !await proofStorage.ExistsOwnedAsync(userId, fileName, cancellationToken))
        {
            return NotFound();
        }

        var proof = await proofStorage.OpenReadAsync(fileName, cancellationToken);
        return proof is null ? NotFound() : File(proof.Content, proof.ContentType);
    }

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

        if (proofStorage is not null)
        {
            var proofFileName = ProofFileName(request.ProofImageUrl);
            if (proofFileName is null ||
                !await proofStorage.ExistsOwnedAsync(userId, proofFileName, cancellationToken))
            {
                return BadRequest(Error("Upload the proof image through TUKI before submitting the tricycle/TODA point."));
            }
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

    private static string? ProofFileName(string? proofUrl)
    {
        var normalized = proofUrl?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !normalized.StartsWith(ProofRoutePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fileName = normalized[ProofRoutePrefix.Length..];
        return !string.IsNullOrWhiteSpace(fileName) &&
               string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            ? fileName
            : null;
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static TricyclePointSubmissionErrorResponse Error(string message) => new([message]);
}

public sealed record TricycleProofUploadResponse(string ProofImageUrl);
