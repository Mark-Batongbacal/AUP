using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using backend.Authentication;
using backend.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AccountSecurityController(
    IPasswordResetService passwordResetService) : ControllerBase
{
    [HttpPost("change-password/request-otp")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> RequestChangePasswordOtp(
        [FromBody] ChangePasswordOtpRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new { message = "Current password is required." });
        }

        var sent = await passwordResetService.RequestPasswordChangeAsync(
            userId,
            request.CurrentPassword,
            cancellationToken);

        return sent
            ? Ok(new { message = "A password change code was sent to your email." })
            : Unauthorized(new { message = "Current password is incorrect." });
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Current password, OTP code, and new password are required." });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "The new password must be at least 8 characters." });
        }

        var changed = await passwordResetService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.Code,
            request.NewPassword,
            cancellationToken);

        return changed
            ? Ok(new { message = "Password changed." })
            : BadRequest(new { message = "The current password or OTP code is invalid, or the code has expired." });
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

public sealed record ChangePasswordOtpRequest(
    [Required, StringLength(256, MinimumLength = 8)] string CurrentPassword);

public sealed record ChangePasswordRequest(
    [Required, StringLength(256, MinimumLength = 8)] string CurrentPassword,
    [Required, StringLength(32, MinimumLength = 4)] string Code,
    [Required, StringLength(256, MinimumLength = 8)] string NewPassword);
