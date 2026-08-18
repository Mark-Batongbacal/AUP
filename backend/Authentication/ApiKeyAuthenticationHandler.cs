using System.Security.Claims;
using System.Text.Encodings.Web;
using backend.Services;
using backend.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace backend.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyService apiKeyService,
    IUserProfileRepository userProfileRepository)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (!apiKeyService.TryGetOwner(apiKey.ToString(), out var userName))
        {
            return AuthenticateResult.Fail("The API key is invalid or expired.");
        }

        var profile = await userProfileRepository.GetActiveByEmailAsync(
            userName!, Context.RequestAborted);
        if (profile is null)
            return AuthenticateResult.Fail("The API key owner has no active user profile.");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, userName!),
                new Claim(ClaimTypes.NameIdentifier, profile.UserId.ToString())
            ],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
