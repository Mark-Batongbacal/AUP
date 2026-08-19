using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using backend.Authentication;
using backend.Services;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Facebook;
using backend.Services.Authentication.Google;
using backend.Services.Authentication.Login;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IApiKeyService apiKeyService,
    IUserProfileService userProfileService,
    IOptions<LoginOptions> options,
    IOptions<GoogleOptions> googleOptions,
    IOptions<FacebookOptions> facebookOptions,
    IGoogleIdTokenValidator googleIdTokenValidator,
    IFacebookAccessTokenValidator facebookAccessTokenValidator,
    IFacebookOidcTokenValidator facebookOidcTokenValidator,
    ILocalAuthenticationService? localAuthenticationService = null) : ControllerBase
{
    private readonly LoginOptions _options = options.Value;
    private readonly GoogleOptions _googleOptions = googleOptions.Value;
    private readonly FacebookOptions _facebookOptions = facebookOptions.Value;
    private readonly ILocalAuthenticationService? _localAuthenticationService = localAuthenticationService;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var validPersistentCredential =
            _localAuthenticationService?.CredentialsAreValid(request.UserName, request.Password) == true;

        if (!validPersistentCredential && !ConfiguredCredentialsAreValid(request.UserName, request.Password))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var issuedKey = apiKeyService.Create(request.UserName.Trim());
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        // Tests and legacy deployments that do not register the persistent local-auth service
        // keep the previous configured-user behavior. Normal application startup registers the
        // service, allowing arbitrary users to create local accounts safely in the database.
        if (_localAuthenticationService is null &&
            !ConfiguredCredentialsAreValid(request.UserName, request.Password))
        {
            return Unauthorized(new { message = "The account is not configured or the password is invalid." });
        }

        var registration = await userProfileService.RegisterLocalProfileAsync(
            request.UserName,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            cancellationToken);
        if (registration.Status == UserProfileRegistrationStatus.Duplicate)
        {
            return Conflict(new { message = registration.Errors[0] });
        }

        if (registration.Status == UserProfileRegistrationStatus.ValidationFailed)
        {
            return BadRequest(new { errors = registration.Errors });
        }

        var authentication = registration.Authentication!;
        if (_localAuthenticationService is not null)
        {
            await _localAuthenticationService.StoreCredentialAsync(
                authentication.UserId,
                request.Password,
                cancellationToken);
        }

        var issuedKey = apiKeyService.Create(authentication.CredentialOwner);
        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(
            authentication.UserId,
            authentication.CredentialOwner,
            authentication.Profile.FirstName,
            authentication.Profile.LastName,
            issuedKey.Value,
            issuedKey.ExpiresAt));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Google(
        GoogleLoginRequest? request,
        CancellationToken cancellationToken = default)
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

        var profile = await userProfileService.CreateOrUpdateExternalProfileAsync(
            "google",
            payload.Subject,
            payload.Name,
            payload.Email,
            cancellationToken);
        if (profile is null)
        {
            return Problem(
                title: "User profile could not be synchronized.",
                detail: "The Google identity could not be mapped to a user profile.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var issuedKey = apiKeyService.Create(profile.CredentialOwner);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpPost("facebook")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LoginResponse>> Facebook(
        FacebookLoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_facebookOptions.AppId) ||
            string.IsNullOrWhiteSpace(_facebookOptions.AppSecret))
        {
            return Problem(
                title: "Facebook login is not configured.",
                detail: "The Facebook app ID or app secret is missing.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return Unauthorized(new { message = "Invalid Facebook token." });
        }

        FacebookUserInfo facebookProfile;
        try
        {
            facebookProfile = await facebookAccessTokenValidator.ValidateAsync(
                request.AccessToken,
                _facebookOptions.AppId,
                _facebookOptions.AppSecret,
                cancellationToken);
        }
        catch (FacebookAccessTokenValidationException)
        {
            return Unauthorized(new { message = "Invalid Facebook token." });
        }
        catch (FacebookTokenValidationUnavailableException)
        {
            return Problem(
                title: "Facebook login is temporarily unavailable.",
                detail: "The Facebook access token could not be validated at this time.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var profile = await userProfileService.CreateOrUpdateExternalProfileAsync(
            "facebook",
            facebookProfile.UserId,
            facebookProfile.Name,
            facebookProfile.Email,
            cancellationToken);
        if (profile is null)
        {
            return Problem(
                title: "User profile could not be synchronized.",
                detail: "The Facebook identity could not be mapped to a user profile.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var issuedKey = apiKeyService.Create(profile.CredentialOwner);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpPost("facebook/oidc")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LoginResponse>> FacebookOidc(
        FacebookOidcLoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_facebookOptions.AppId))
        {
            return Problem(
                title: "Facebook login is not configured.",
                detail: "The Facebook app ID is missing.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.IdToken) ||
            string.IsNullOrWhiteSpace(request.Nonce))
        {
            return Unauthorized(new { message = "Invalid Facebook token." });
        }

        FacebookOidcUserInfo facebookProfile;
        try
        {
            facebookProfile = await facebookOidcTokenValidator.ValidateAsync(
                request.IdToken,
                _facebookOptions.AppId,
                request.Nonce,
                cancellationToken);
        }
        catch (FacebookOidcTokenValidationException)
        {
            return Unauthorized(new { message = "Invalid Facebook token." });
        }
        catch (FacebookTokenValidationUnavailableException)
        {
            return Problem(
                title: "Facebook login is temporarily unavailable.",
                detail: "The Facebook authentication token could not be validated at this time.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var profile = await userProfileService.CreateOrUpdateExternalProfileAsync(
            "facebook",
            facebookProfile.Subject,
            facebookProfile.Name,
            facebookProfile.Email,
            cancellationToken);
        if (profile is null)
        {
            return Problem(
                title: "User profile could not be synchronized.",
                detail: "The Facebook identity could not be mapped to a user profile.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var issuedKey = apiKeyService.Create(profile.CredentialOwner);
        return Ok(new LoginResponse(issuedKey.Value, issuedKey.ExpiresAt));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public ActionResult<object> Me() => Ok(new { userName = User.Identity?.Name });

    private bool ConfiguredCredentialsAreValid(string userName, string password)
    {
        var configuredUser = _options.ConfiguredUsers.FirstOrDefault(user =>
            string.Equals(userName, user.UserName, StringComparison.Ordinal));
        return configuredUser is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(configuredUser.Password));
    }
}

public sealed record LoginRequest(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(256, MinimumLength = 8)] string Password);

public sealed record GoogleLoginRequest(string? IdToken);

public sealed record FacebookLoginRequest(string? AccessToken);

public sealed record FacebookOidcLoginRequest(string? IdToken, string? Nonce);

public sealed record LoginResponse(string ApiKey, DateTimeOffset ExpiresAt)
{
    public string AuthenticationScheme { get; init; } = ApiKeyAuthenticationHandler.SchemeName;
    public string HeaderName { get; init; } = ApiKeyAuthenticationHandler.HeaderName;
}

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(255)] string UserName,
    [Required, StringLength(256, MinimumLength = 8)] string Password,
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [StringLength(30)] string? PhoneNumber = null);

public sealed record RegisterResponse(
    Guid UserId,
    string UserName,
    string? FirstName,
    string? LastName,
    string ApiKey,
    DateTimeOffset ExpiresAt)
{
    public string AuthenticationScheme { get; init; } = ApiKeyAuthenticationHandler.SchemeName;
    public string HeaderName { get; init; } = ApiKeyAuthenticationHandler.HeaderName;
}
