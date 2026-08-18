using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using backend.Authentication;
using backend.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IApiKeyService apiKeyService,
    IOptions<LoginOptions> options,
    IOptions<GoogleOptions> googleOptions,
    IGoogleIdTokenValidator googleIdTokenValidator) : ControllerBase
{
    private readonly LoginOptions _options = options.Value;
    private readonly GoogleOptions _googleOptions = googleOptions.Value;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var configuredUser = _options.ConfiguredUsers.FirstOrDefault(user =>
            string.Equals(request.UserName, user.UserName, StringComparison.Ordinal));

        if (configuredUser is null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.Password),
                Encoding.UTF8.GetBytes(configuredUser.Password)))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var issuedKey = apiKeyService.Create(configuredUser.UserName);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Google(GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            return Problem(
                title: "Google login is not configured.",
                detail: "The Google client ID is missing.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await googleIdTokenValidator.ValidateAsync(
                request.IdToken,
                _googleOptions.ClientId);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject))
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        var issuedKey = apiKeyService.Create($"google:{payload.Subject}");
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public ActionResult<object> Me() => Ok(new { userName = User.Identity?.Name });
}

public sealed record LoginRequest(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(256, MinimumLength = 8)] string Password);

public sealed record GoogleLoginRequest(string? IdToken);

public sealed record LoginResponse(string ApiKey, DateTimeOffset ExpiresAt)
{
    public string AuthenticationScheme { get; init; } = ApiKeyAuthenticationHandler.SchemeName;
    public string HeaderName { get; init; } = ApiKeyAuthenticationHandler.HeaderName;
}
