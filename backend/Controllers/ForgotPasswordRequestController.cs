using backend.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth/forgot-password")]
public sealed class ForgotPasswordRequestController(
    IPasswordResetService passwordResetService) : ControllerBase
{
    [HttpPost("request")]
    [AllowAnonymous]
    public async Task<IActionResult> Request(
        [FromBody] ForgotPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "An email address is required." });
        }

        var accepted = await passwordResetService.RequestResetAsync(
            request.Email,
            cancellationToken);

        if (!accepted)
        {
            return NotFound(new
            {
                message = "No registered password account was found for this email."
            });
        }

        return Ok(new
        {
            message = "A password reset code was sent, or a recent code is still within the resend cooldown."
        });
    }
}
