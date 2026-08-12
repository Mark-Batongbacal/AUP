using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using backend.Authentication;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IApiKeyService apiKeyService, IOptions<LoginOptions> options) : ControllerBase
{
    private readonly LoginOptions _options = options.Value;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var user = _options.ConfiguredUsers.FirstOrDefault(user =>
            !string.IsNullOrWhiteSpace(user.UserName) &&
            !string.IsNullOrEmpty(user.Password) &&
            string.Equals(request.UserName, user.UserName, StringComparison.Ordinal) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.Password),
                Encoding.UTF8.GetBytes(user.Password)));

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var issuedKey = apiKeyService.Create(user.UserName);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public ActionResult<object> Me() => Ok(new { userName = User.Identity?.Name });
}

public sealed record LoginRequest(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(256, MinimumLength = 8)] string Password);

public sealed record LoginResponse(string ApiKey, DateTimeOffset ExpiresAt)
{
    public string AuthenticationScheme { get; init; } = ApiKeyAuthenticationHandler.SchemeName;
    public string HeaderName { get; init; } = ApiKeyAuthenticationHandler.HeaderName;
}
