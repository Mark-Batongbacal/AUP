using System.Security.Cryptography;
using System.Text;
using backend.Models.Database;
using backend.Repositories;
using backend.Services.Authentication.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/auth/admin")]
public sealed class AdminAuthController(
    IConfiguration configuration,
    IUserProfileRepository userProfileRepository,
    IApiKeyService apiKeyService) : ControllerBase
{
    private const string AdminRole = "Admin";

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var configuredUserName = configuration["AdminLogin:UserName"]?.Trim();
        var configuredPassword = configuration["AdminLogin:Password"];

        if (string.IsNullOrWhiteSpace(configuredUserName) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            return Problem(
                title: "Admin login is not configured.",
                detail: "Set AdminLogin__UserName and AdminLogin__Password in the backend environment.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var userNameMatches = string.Equals(
            request.UserName.Trim(),
            configuredUserName,
            StringComparison.OrdinalIgnoreCase);
        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Password),
            Encoding.UTF8.GetBytes(configuredPassword));

        if (!userNameMatches || !passwordMatches)
        {
            return Unauthorized(new { message = "Invalid administrator username or password." });
        }

        await EnsureAdminProfileAsync(configuredUserName, cancellationToken);

        var issuedKey = apiKeyService.Create(configuredUserName);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    private async Task EnsureAdminProfileAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        var existing = await userProfileRepository.GetByEmailAsync(userName, cancellationToken);
        var now = DateTime.UtcNow;

        var profile = new UserProfile
        {
            UserId = existing?.UserId ?? Guid.NewGuid(),
            ExternalAuthProvider = existing?.ExternalAuthProvider,
            ExternalAuthId = existing?.ExternalAuthId,
            Email = userName,
            FirstName = existing?.FirstName ?? "TUKI",
            LastName = existing?.LastName ?? "Administrator",
            PhoneNumber = existing?.PhoneNumber,
            Role = AdminRole,
            ProfileImageUrl = existing?.ProfileImageUrl,
            PreferredLanguage = existing?.PreferredLanguage ?? "English",
            IsActive = true,
            IsEmailVerified = existing?.IsEmailVerified ?? true,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };

        await userProfileRepository.AddOrUpdateAsync(profile, cancellationToken);
    }
}
