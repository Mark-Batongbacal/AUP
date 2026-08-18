using backend.Controllers;
using backend.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Tests.Controllers;

public sealed class AuthControllerTests
{
    private static readonly DateTimeOffset TestExpiration =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Login_WhenPasswordIsValid_IssuesExistingApiKeyResponse()
    {
        var apiKeys = new RecordingApiKeyService();
        var controller = CreateController(apiKeys);

        var response = controller.Login(new LoginRequest("admin", "correct-password"));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("TEST_API_KEY", body.ApiKey);
        Assert.Equal(TestExpiration, body.ExpiresAt);
        Assert.Equal("ApiKey", body.AuthenticationScheme);
        Assert.Equal("X-Api-Key", body.HeaderName);
        Assert.Equal(["admin"], apiKeys.CreatedFor);
    }

    [Fact]
    public void Login_WhenPasswordIsInvalid_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var controller = CreateController(apiKeys);

        var response = controller.Login(new LoginRequest("admin", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Empty(apiKeys.CreatedFor);
    }

    [Fact]
    public async Task Google_WhenClientIdIsMissing_ReturnsServerConfigurationError()
    {
        var apiKeys = new RecordingApiKeyService();
        var googleTokens = new StubGoogleIdTokenValidator();
        var controller = CreateController(
            apiKeys,
            googleTokens,
            googleOptions: new GoogleOptions());

        var response = await controller.Google(new GoogleLoginRequest("id-token"));

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Empty(apiKeys.CreatedFor);
        Assert.Null(googleTokens.LastIdToken);
    }

    [Fact]
    public async Task Google_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var googleTokens = new StubGoogleIdTokenValidator
        {
            Validate = (_, _) => throw new InvalidJwtException("invalid")
        };
        var controller = CreateController(apiKeys, googleTokens);

        var response = await controller.Google(new GoogleLoginRequest("INVALID_TEST_TOKEN"));

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Equal("INVALID_TEST_TOKEN", googleTokens.LastIdToken);
        Assert.Equal("google-client-id", googleTokens.LastAudience);
        Assert.Empty(apiKeys.CreatedFor);
    }

    [Fact]
    public async Task Google_WhenTokenIsValid_IssuesApiKeyForVerifiedSubject()
    {
        var apiKeys = new RecordingApiKeyService();
        var googleTokens = new StubGoogleIdTokenValidator
        {
            Validate = (_, _) => new GoogleJsonWebSignature.Payload
            {
                Subject = "stable-google-sub",
                Email = "verified@example.test",
                Name = "Verified Name"
            }
        };
        var controller = CreateController(apiKeys, googleTokens);

        var response = await controller.Google(new GoogleLoginRequest("verified-id-token"));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("TEST_API_KEY", body.ApiKey);
        Assert.Equal(TestExpiration, body.ExpiresAt);
        Assert.Equal("ApiKey", body.AuthenticationScheme);
        Assert.Equal("X-Api-Key", body.HeaderName);
        Assert.Equal("verified-id-token", googleTokens.LastIdToken);
        Assert.Equal("google-client-id", googleTokens.LastAudience);
        Assert.Equal(["google:stable-google-sub"], apiKeys.CreatedFor);
    }

    [Fact]
    public async Task Google_WhenVerifiedTokenHasNoSubject_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var googleTokens = new StubGoogleIdTokenValidator
        {
            Validate = (_, _) => new GoogleJsonWebSignature.Payload()
        };
        var controller = CreateController(apiKeys, googleTokens);

        var response = await controller.Google(new GoogleLoginRequest("verified-id-token"));

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Empty(apiKeys.CreatedFor);
    }

    private static AuthController CreateController(
        RecordingApiKeyService? apiKeys = null,
        StubGoogleIdTokenValidator? googleTokens = null,
        LoginOptions? loginOptions = null,
        GoogleOptions? googleOptions = null) =>
        new(
            apiKeys ?? new RecordingApiKeyService(),
            Options.Create(loginOptions ?? new LoginOptions
            {
                Users =
                [
                    new LoginUserOptions
                    {
                        UserName = "admin",
                        Password = "correct-password"
                    }
                ]
            }),
            Options.Create(googleOptions ?? new GoogleOptions
            {
                ClientId = "google-client-id"
            }),
            googleTokens ?? new StubGoogleIdTokenValidator());

    private sealed class RecordingApiKeyService : IApiKeyService
    {
        public List<string> CreatedFor { get; } = [];

        public IssuedApiKey Create(string userName)
        {
            CreatedFor.Add(userName);
            return new IssuedApiKey("TEST_API_KEY", TestExpiration);
        }

        public bool TryGetOwner(string apiKey, out string? userName)
        {
            userName = null;
            return false;
        }
    }

    private sealed class StubGoogleIdTokenValidator : IGoogleIdTokenValidator
    {
        public Func<string, string, GoogleJsonWebSignature.Payload> Validate { get; init; } =
            (_, _) => new GoogleJsonWebSignature.Payload
            {
                Subject = "stable-google-sub"
            };

        public string? LastIdToken { get; private set; }

        public string? LastAudience { get; private set; }

        public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string audience)
        {
            LastIdToken = idToken;
            LastAudience = audience;
            return Task.FromResult(Validate(idToken, audience));
        }
    }
}
