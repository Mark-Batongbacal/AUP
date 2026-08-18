using System.Security.Claims;
using backend.Controllers;
using backend.Models.Database;
using backend.Models.Users;
using backend.Repositories;
using backend.Services;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Facebook;
using backend.Services.Authentication.Google;
using backend.Services.Authentication.Login;
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

    [Fact]
    public async Task Facebook_WhenTokenIsValid_IssuesApiKeyForVerifiedUserId()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var facebookTokens = new StubFacebookAccessTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookUserInfo("stable-facebook-user-id", "Verified Name", "verified@example.test"))
        };
        var controller = CreateController(
            apiKeys,
            facebookTokens: facebookTokens,
            userProfiles: userProfiles);

        var response = await controller.Facebook(
            new FacebookLoginRequest("verified-facebook-token"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("TEST_API_KEY", body.ApiKey);
        Assert.Equal(TestExpiration, body.ExpiresAt);
        Assert.Equal("ApiKey", body.AuthenticationScheme);
        Assert.Equal("X-Api-Key", body.HeaderName);
        Assert.Equal("verified-facebook-token", facebookTokens.LastAccessToken);
        Assert.Equal("facebook-app-id", facebookTokens.LastAppId);
        Assert.Equal("facebook-app-secret", facebookTokens.LastAppSecret);
        Assert.Equal(["facebook:stable-facebook-user-id"], apiKeys.CreatedFor);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal("facebook", persistedProfile.ExternalAuthProvider);
        Assert.Equal("stable-facebook-user-id", persistedProfile.ExternalAuthId);
        Assert.Equal("facebook:stable-facebook-user-id", persistedProfile.Email);
        Assert.Equal("Verified", persistedProfile.FirstName);
        Assert.Equal("Name", persistedProfile.LastName);
        await AssertCredentialResolvesUsersMeAsync(
            userProfiles,
            "facebook:stable-facebook-user-id",
            persistedProfile.UserId);
    }

    [Fact]
    public async Task Facebook_WhenProfileAlreadyExists_ReusesProfileWithoutCreatingDuplicate()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var userId = Guid.NewGuid();
        userProfiles.Seed(new UserProfile
        {
            UserId = userId,
            ExternalAuthProvider = "facebook",
            ExternalAuthId = "stable-facebook-user-id",
            Email = "facebook:stable-facebook-user-id",
            FirstName = "Manual",
            LastName = "Profile",
            PhoneNumber = "+63 900 000 0000",
            Role = "Passenger",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        var facebookTokens = new StubFacebookAccessTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookUserInfo("stable-facebook-user-id", "Provider Name", null))
        };
        var controller = CreateController(
            apiKeys,
            facebookTokens: facebookTokens,
            userProfiles: userProfiles);

        var response = await controller.Facebook(
            new FacebookLoginRequest("verified-facebook-token"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal(userId, persistedProfile.UserId);
        Assert.Equal("Manual", persistedProfile.FirstName);
        Assert.Equal("Profile", persistedProfile.LastName);
        Assert.Equal("+63 900 000 0000", persistedProfile.PhoneNumber);
        Assert.Equal(["facebook:stable-facebook-user-id"], apiKeys.CreatedFor);
        Assert.Equal(1, userProfiles.AddOrUpdateCalls);
    }

    [Fact]
    public async Task Facebook_WhenEmailIsMissing_StillCreatesProfileAndIssuesApiKey()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var facebookTokens = new StubFacebookAccessTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookUserInfo("facebook-user-without-email", "No Email", null))
        };
        var controller = CreateController(
            apiKeys,
            facebookTokens: facebookTokens,
            userProfiles: userProfiles);

        var response = await controller.Facebook(
            new FacebookLoginRequest("verified-facebook-token"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal("facebook:facebook-user-without-email", persistedProfile.Email);
        Assert.Equal("facebook", persistedProfile.ExternalAuthProvider);
        Assert.Equal("facebook-user-without-email", persistedProfile.ExternalAuthId);
        Assert.Equal(["facebook:facebook-user-without-email"], apiKeys.CreatedFor);
    }

    [Fact]
    public async Task Facebook_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var facebookTokens = new StubFacebookAccessTokenValidator
        {
            Validate = (_, _, _, _) => throw new FacebookAccessTokenValidationException()
        };
        var controller = CreateController(
            apiKeys,
            facebookTokens: facebookTokens,
            userProfiles: userProfiles);

        var response = await controller.Facebook(
            new FacebookLoginRequest("INVALID_TEST_TOKEN"),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Equal("INVALID_TEST_TOKEN", facebookTokens.LastAccessToken);
        Assert.Empty(apiKeys.CreatedFor);
        Assert.Empty(userProfiles.Profiles);
    }

    [Fact]
    public async Task Facebook_WhenAccessTokenIsMissing_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var facebookTokens = new StubFacebookAccessTokenValidator();
        var controller = CreateController(apiKeys, facebookTokens: facebookTokens);

        var response = await controller.Facebook(
            new FacebookLoginRequest(null),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Null(facebookTokens.LastAccessToken);
        Assert.Empty(apiKeys.CreatedFor);
    }

    [Fact]
    public async Task Facebook_WhenValidatorIsUnavailable_DoesNotExposeSecrets()
    {
        const string token = "SECRET_FACEBOOK_TOKEN";
        const string appSecret = "SECRET_FACEBOOK_APP_SECRET";
        var apiKeys = new RecordingApiKeyService();
        var facebookTokens = new StubFacebookAccessTokenValidator
        {
            Validate = (_, _, _, _) => throw new FacebookTokenValidationUnavailableException()
        };
        var controller = CreateController(
            apiKeys,
            facebookTokens: facebookTokens,
            facebookOptions: new FacebookOptions
            {
                AppId = "facebook-app-id",
                AppSecret = appSecret
            });

        var response = await controller.Facebook(
            new FacebookLoginRequest(token),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        var serializedProblem = System.Text.Json.JsonSerializer.Serialize(problem);
        Assert.DoesNotContain(token, serializedProblem, StringComparison.Ordinal);
        Assert.DoesNotContain(appSecret, serializedProblem, StringComparison.Ordinal);
        Assert.Empty(apiKeys.CreatedFor);
    }

    [Fact]
    public async Task FacebookOidc_WhenTokenIsValid_IssuesApiKeyForVerifiedSubject()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var oidcTokens = new StubFacebookOidcTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookOidcUserInfo("stable-facebook-subject", "Verified Name", "verified@example.test"))
        };
        var controller = CreateController(
            apiKeys,
            facebookOidcTokens: oidcTokens,
            userProfiles: userProfiles);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest("verified-facebook-id-token", "nonce-value"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("TEST_API_KEY", body.ApiKey);
        Assert.Equal(TestExpiration, body.ExpiresAt);
        Assert.Equal("ApiKey", body.AuthenticationScheme);
        Assert.Equal("X-Api-Key", body.HeaderName);
        Assert.Equal("verified-facebook-id-token", oidcTokens.LastIdToken);
        Assert.Equal("facebook-app-id", oidcTokens.LastAppId);
        Assert.Equal("nonce-value", oidcTokens.LastNonce);
        Assert.Equal(["facebook:stable-facebook-subject"], apiKeys.CreatedFor);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal("facebook", persistedProfile.ExternalAuthProvider);
        Assert.Equal("stable-facebook-subject", persistedProfile.ExternalAuthId);
        Assert.Equal("facebook:stable-facebook-subject", persistedProfile.Email);
        Assert.Equal("Verified", persistedProfile.FirstName);
        Assert.Equal("Name", persistedProfile.LastName);
        await AssertCredentialResolvesUsersMeAsync(
            userProfiles,
            "facebook:stable-facebook-subject",
            persistedProfile.UserId);
    }

    [Fact]
    public async Task FacebookOidc_WhenProfileAlreadyExists_ReusesProfileWithoutCreatingDuplicate()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var userId = Guid.NewGuid();
        userProfiles.Seed(new UserProfile
        {
            UserId = userId,
            ExternalAuthProvider = "facebook",
            ExternalAuthId = "stable-facebook-subject",
            Email = "facebook:stable-facebook-subject",
            FirstName = "Manual",
            LastName = "Profile",
            PhoneNumber = "+63 900 000 0000",
            Role = "Passenger",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        var oidcTokens = new StubFacebookOidcTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookOidcUserInfo("stable-facebook-subject", "Provider Name", null))
        };
        var controller = CreateController(
            apiKeys,
            facebookOidcTokens: oidcTokens,
            userProfiles: userProfiles);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest("verified-facebook-id-token", "nonce-value"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal(userId, persistedProfile.UserId);
        Assert.Equal("Manual", persistedProfile.FirstName);
        Assert.Equal("Profile", persistedProfile.LastName);
        Assert.Equal("+63 900 000 0000", persistedProfile.PhoneNumber);
        Assert.Equal(["facebook:stable-facebook-subject"], apiKeys.CreatedFor);
        Assert.Equal(1, userProfiles.AddOrUpdateCalls);
    }

    [Fact]
    public async Task FacebookOidc_WhenProfileClaimsAreMissing_StillCreatesProfileAndIssuesApiKey()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var oidcTokens = new StubFacebookOidcTokenValidator
        {
            Validate = (_, _, _, _) => Task.FromResult(
                new FacebookOidcUserInfo("subject-without-profile-claims", null, null))
        };
        var controller = CreateController(
            apiKeys,
            facebookOidcTokens: oidcTokens,
            userProfiles: userProfiles);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest("verified-facebook-id-token", "nonce-value"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        var persistedProfile = Assert.Single(userProfiles.Profiles);
        Assert.Equal("facebook:subject-without-profile-claims", persistedProfile.Email);
        Assert.Equal("facebook", persistedProfile.ExternalAuthProvider);
        Assert.Equal("subject-without-profile-claims", persistedProfile.ExternalAuthId);
        Assert.Null(persistedProfile.FirstName);
        Assert.Null(persistedProfile.LastName);
        Assert.Equal(["facebook:subject-without-profile-claims"], apiKeys.CreatedFor);
    }

    [Fact]
    public async Task FacebookOidc_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var userProfiles = new RecordingUserProfileRepository();
        var oidcTokens = new StubFacebookOidcTokenValidator
        {
            Validate = (_, _, _, _) => throw new FacebookOidcTokenValidationException()
        };
        var controller = CreateController(
            apiKeys,
            facebookOidcTokens: oidcTokens,
            userProfiles: userProfiles);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest("INVALID_ID_TOKEN", "nonce-value"),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Equal("INVALID_ID_TOKEN", oidcTokens.LastIdToken);
        Assert.Empty(apiKeys.CreatedFor);
        Assert.Empty(userProfiles.Profiles);
    }

    [Fact]
    public async Task FacebookOidc_WhenNonceIsMissing_ReturnsUnauthorized()
    {
        var apiKeys = new RecordingApiKeyService();
        var oidcTokens = new StubFacebookOidcTokenValidator();
        var controller = CreateController(apiKeys, facebookOidcTokens: oidcTokens);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest("verified-facebook-id-token", null),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response.Result);
        Assert.Null(oidcTokens.LastIdToken);
        Assert.Empty(apiKeys.CreatedFor);
    }

    [Fact]
    public async Task FacebookOidc_WhenValidatorIsUnavailable_DoesNotExposeSecrets()
    {
        const string idToken = "SECRET_FACEBOOK_AUTHENTICATION_TOKEN";
        var apiKeys = new RecordingApiKeyService();
        var oidcTokens = new StubFacebookOidcTokenValidator
        {
            Validate = (_, _, _, _) => throw new FacebookOidcTokenValidationUnavailableException()
        };
        var controller = CreateController(apiKeys, facebookOidcTokens: oidcTokens);

        var response = await controller.FacebookOidc(
            new FacebookOidcLoginRequest(idToken, "nonce-value"),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        var serializedProblem = System.Text.Json.JsonSerializer.Serialize(problem);
        Assert.DoesNotContain(idToken, serializedProblem, StringComparison.Ordinal);
        Assert.Empty(apiKeys.CreatedFor);
    }

    private static async Task AssertCredentialResolvesUsersMeAsync(
        RecordingUserProfileRepository userProfiles,
        string credentialOwner,
        Guid expectedUserId)
    {
        var userProfileService = new UserProfileService(userProfiles);
        var authenticatedProfile = await userProfileService.GetAuthenticatedUserProfileAsync(
            credentialOwner,
            CancellationToken.None);
        Assert.NotNull(authenticatedProfile);
        Assert.Equal(expectedUserId, authenticatedProfile.UserId);

        var usersController = new UsersController(userProfileService);
        usersController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, authenticatedProfile.UserId.ToString())],
                    "ApiKey")),
            },
        };

        var response = await usersController.GetCurrent(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var profile = Assert.IsType<UserProfileResponse>(ok.Value);
        Assert.Equal(expectedUserId, profile.UserId);
    }

    private static AuthController CreateController(
        RecordingApiKeyService? apiKeys = null,
        StubGoogleIdTokenValidator? googleTokens = null,
        StubFacebookAccessTokenValidator? facebookTokens = null,
        StubFacebookOidcTokenValidator? facebookOidcTokens = null,
        RecordingUserProfileRepository? userProfiles = null,
        LoginOptions? loginOptions = null,
        GoogleOptions? googleOptions = null,
        FacebookOptions? facebookOptions = null)
    {
        var profileRepository = userProfiles ?? new RecordingUserProfileRepository();

        return new AuthController(
            apiKeys ?? new RecordingApiKeyService(),
            new UserProfileService(profileRepository),
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
            Options.Create(facebookOptions ?? new FacebookOptions
            {
                AppId = "facebook-app-id",
                AppSecret = "facebook-app-secret"
            }),
            googleTokens ?? new StubGoogleIdTokenValidator(),
            facebookTokens ?? new StubFacebookAccessTokenValidator(),
            facebookOidcTokens ?? new StubFacebookOidcTokenValidator());
    }

    private sealed class RecordingUserProfileRepository : IUserProfileRepository
    {
        private readonly List<UserProfile> _profiles = [];

        public IReadOnlyList<UserProfile> Profiles => _profiles;

        public int AddOrUpdateCalls { get; private set; }

        public void Seed(UserProfile profile)
        {
            _profiles.Add(profile);
        }

        public Task<UserProfile?> GetByUserIdAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(profile => profile.UserId == userId));

        public Task<UserProfile?> GetActiveByUserIdAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(
                profile => profile.UserId == userId && profile.IsActive));

        public Task<UserProfile?> GetByExternalAuthAsync(
            string provider,
            string externalAuthId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(
                profile => profile.ExternalAuthProvider == provider &&
                    profile.ExternalAuthId == externalAuthId));

        public Task<UserProfile?> GetActiveByExternalAuthAsync(
            string provider,
            string externalAuthId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(
                profile => profile.ExternalAuthProvider == provider &&
                    profile.ExternalAuthId == externalAuthId &&
                    profile.IsActive));

        public Task<UserProfile?> GetActiveByEmailAsync(
            string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(profile => profile.Email == email && profile.IsActive));

        public Task<UserProfile?> GetByEmailAsync(
            string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.FirstOrDefault(profile => profile.Email == email));

        public Task<UserProfile> AddOrUpdateAsync(
            UserProfile profile, CancellationToken cancellationToken = default)
        {
            AddOrUpdateCalls++;
            var existing = _profiles.FirstOrDefault(current => current.UserId == profile.UserId);
            if (existing is null)
            {
                _profiles.Add(profile);
                return Task.FromResult(profile);
            }

            existing.ExternalAuthProvider = profile.ExternalAuthProvider;
            existing.ExternalAuthId = profile.ExternalAuthId;
            existing.Email = profile.Email;
            existing.FirstName = profile.FirstName;
            existing.LastName = profile.LastName;
            existing.PhoneNumber = profile.PhoneNumber;
            existing.Role = profile.Role;
            existing.ProfileImageUrl = profile.ProfileImageUrl;
            existing.IsActive = profile.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            return Task.FromResult(existing);
        }

        public Task<UserProfile?> UpdateEditableFieldsAsync(
            Guid userId,
            string? firstName,
            string? lastName,
            string? phoneNumber,
            string? profileImageUrl,
            CancellationToken cancellationToken = default)
        {
            var profile = _profiles.FirstOrDefault(profile => profile.UserId == userId && profile.IsActive);
            if (profile is null)
            {
                return Task.FromResult<UserProfile?>(null);
            }

            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.PhoneNumber = phoneNumber;
            profile.ProfileImageUrl = profileImageUrl;
            profile.UpdatedAt = DateTime.UtcNow;

            return Task.FromResult<UserProfile?>(profile);
        }

        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Any(profile => profile.UserId == userId));
    }

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

    private sealed class StubFacebookAccessTokenValidator : IFacebookAccessTokenValidator
    {
        public Func<string, string, string, CancellationToken, Task<FacebookUserInfo>> Validate { get; init; } =
            (_, _, _, _) => Task.FromResult(new FacebookUserInfo("stable-facebook-user-id", null, null));

        public string? LastAccessToken { get; private set; }

        public string? LastAppId { get; private set; }

        public string? LastAppSecret { get; private set; }

        public Task<FacebookUserInfo> ValidateAsync(
            string accessToken,
            string appId,
            string appSecret,
            CancellationToken cancellationToken = default)
        {
            LastAccessToken = accessToken;
            LastAppId = appId;
            LastAppSecret = appSecret;
            return Validate(accessToken, appId, appSecret, cancellationToken);
        }
    }

    private sealed class StubFacebookOidcTokenValidator : IFacebookOidcTokenValidator
    {
        public Func<string, string, string, CancellationToken, Task<FacebookOidcUserInfo>> Validate { get; init; } =
            (_, _, _, _) => Task.FromResult(new FacebookOidcUserInfo("stable-facebook-subject", null, null));

        public string? LastIdToken { get; private set; }

        public string? LastAppId { get; private set; }

        public string? LastNonce { get; private set; }

        public Task<FacebookOidcUserInfo> ValidateAsync(
            string idToken,
            string appId,
            string nonce,
            CancellationToken cancellationToken = default)
        {
            LastIdToken = idToken;
            LastAppId = appId;
            LastNonce = nonce;
            return Validate(idToken, appId, nonce, cancellationToken);
        }
    }
}
