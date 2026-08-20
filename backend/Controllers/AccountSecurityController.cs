using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using backend.Authentication;
using backend.Services.Authentication.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AccountSecurityController(
    ILocalAuthenticationService localAuthenticationService) : ControllerBase
{
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        var userName = User.Identity?.Name?.Trim();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "Current password and new password are required." });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "The new password must be at least 8 characters." });
        }

        if (!localAuthenticationService.CredentialsAreValid(userName, request.CurrentPassword))
        {
            return Unauthorized(new { message = "Current password is incorrect." });
        }

        await localAuthenticationService.StoreCredentialAsync(
            userId,
            request.NewPassword,
            cancellationToken);

        return NoContent();
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

public sealed record ChangePasswordRequest(
    [Required, StringLength(256, MinimumLength = 8)] string CurrentPassword,
    [Required, StringLength(256, MinimumLength = 8)] string NewPassword);
