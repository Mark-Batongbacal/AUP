using System.Net;
using System.Net.Http.Json;
using backend.Services;
using Microsoft.Extensions.Logging;

namespace backend.Tests.Services;

public sealed class FacebookAccessTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenTokenAndProfileAreValid_ReturnsVerifiedFacebookUser()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "verified-facebook-user-id",
                        scopes = new[] { "email", "public_profile" }
                    }
                });
            }

            if (request.RequestUri?.AbsolutePath == "/me")
            {
                return JsonResponse(new
                {
                    id = "verified-facebook-user-id",
                    name = "Verified User"
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var validator = new FacebookAccessTokenValidator(new HttpClient(handler));

        var user = await validator.ValidateAsync(
            "facebook-access-token",
            "configured-app-id",
            "configured-app-secret");

        Assert.Equal("verified-facebook-user-id", user.UserId);
        Assert.Equal("Verified User", user.Name);
        Assert.Null(user.Email);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.DoesNotContain(
                "configured-app-secret",
                request.RequestUri?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
        });
        var debugTokenRequest = Assert.Single(
            handler.Requests,
            request => request.RequestUri?.AbsolutePath == "/debug_token");
        Assert.Equal("Bearer", debugTokenRequest.Headers.Authorization?.Scheme);
        Assert.Equal("configured-app-id|configured-app-secret", debugTokenRequest.Headers.Authorization?.Parameter);
        Assert.Contains("input_token=", debugTokenRequest.RequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("appsecret_proof=", debugTokenRequest.RequestUri?.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", debugTokenRequest.RequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenDebugTokenHasWrongAppId_RejectsToken()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "different-app-id",
                        type = "USER",
                        expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "verified-facebook-user-id"
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(new HttpClient(handler));

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenDebugTokenIsExpired_RejectsToken()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        expires_at = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "verified-facebook-user-id"
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(new HttpClient(handler));

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenDebugTokenIsInvalid_RejectsToken()
    {
        var handler = new StubHttpMessageHandler(request => JsonResponse(new
        {
            data = new
            {
                app_id = "configured-app-id",
                type = "USER",
                expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                is_valid = false,
                user_id = "verified-facebook-user-id"
            }
        }));
        var validator = new FacebookAccessTokenValidator(new HttpClient(handler));

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        Assert.Single(handler.Requests);
    }

#if DEBUG
    [Fact]
    public async Task ValidateAsync_WhenDebugTokenHttpRequestFails_LogsSafeDiagnostic()
    {
        const string accessToken = "SECRET_FACEBOOK_ACCESS_TOKEN";
        const string appSecret = "SECRET_FACEBOOK_APP_SECRET";
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                accessToken,
                "configured-app-id",
                appSecret));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("token present: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token length: 28", diagnostic, StringComparison.Ordinal);
        Assert.Contains("contains '.' separators: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("separator count: 0", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token looks JWT-shaped: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token begins with expected Facebook opaque-token style: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Android/iOS token classification: OPAQUE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("debug_token HTTP request succeeded: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("is_valid: UNKNOWN", diagnostic, StringComparison.Ordinal);
        Assert.Contains("configured Facebook App Secret present: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type: MISSING", diagnostic, StringComparison.Ordinal);
        Assert.Contains("scopes returned: NOT CHECKED", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error code: NONE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error type: NONE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error message: NONE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("/me request succeeded: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: debug_token HTTP request failed", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(appSecret, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("configured-app-id|", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenDebugTokenTypeIsNotUser_LogsRejectedTypeDiagnostic()
    {
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "CLIENT",
                        expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "verified-facebook-user-id"
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("debug_token HTTP request succeeded: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("is_valid: TRUE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token app ID matches configured app ID: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type accepted: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token expired: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("verified user ID present: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("/me request succeeded: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: token type not accepted", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("verified-facebook-user-id", diagnostic, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenDebugTokenIsValid_LogsSafeOpaqueTokenMetadataAndScopes()
    {
        const string accessToken = "EAA_SYNTHETIC_FACEBOOK_ACCESS_TOKEN";
        const string appSecret = "SECRET_FACEBOOK_APP_SECRET";
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "verified-facebook-user-id",
                        scopes = new[] { "email", "public_profile" }
                    }
                });
            }

            return JsonResponse(new
            {
                id = "verified-facebook-user-id",
                name = "Verified User"
            });
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await validator.ValidateAsync(
            accessToken,
            "configured-app-id",
            appSecret);

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("token present: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("contains '.' separators: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("separator count: 0", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token looks JWT-shaped: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token begins with expected Facebook opaque-token style: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Android/iOS token classification: OPAQUE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("is_valid: TRUE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token app ID matches configured app ID: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type: USER", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type accepted: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("scopes returned: email,public_profile", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: NONE", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(appSecret, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("verified-facebook-user-id", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenJwtLikeTokenIsRejected_LogsSafeTokenShapeAndMetaError()
    {
        const string accessToken = "eyJhbGciOiJub25lIn0.eyJzdWIiOiIxMjMifQ.";
        const string appSecret = "SECRET_FACEBOOK_APP_SECRET";
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        is_valid = false,
                        error = new
                        {
                            code = 190,
                            type = "OAuthException",
                            message = "Invalid OAuth access token - Cannot parse access token"
                        }
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                accessToken,
                "configured-app-id",
                appSecret));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("token present: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("contains '.' separators: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("separator count: 2", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token looks JWT-shaped: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token begins with expected Facebook opaque-token style: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Android/iOS token classification: JWT-LIKE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("is_valid: FALSE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token app ID matches configured app ID: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type: MISSING", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token type accepted: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("expires_at diagnostic: MISSING", diagnostic, StringComparison.Ordinal);
        Assert.Contains("verified user ID present: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("scopes returned: MISSING", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error code: 190", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error type: OAuthException", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Meta error message: Invalid OAuth access token - Cannot parse access token", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: debug_token is_valid false", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(appSecret, diagnostic, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpiresAtIsZero_LogsRejectedExpiryDiagnostic()
    {
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        expires_at = 0,
                        is_valid = true,
                        user_id = "verified-facebook-user-id"
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("token type accepted: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("token expired: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("expires_at diagnostic: ZERO_OR_NEGATIVE", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: token expired or expires_at rejected", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("verified-facebook-user-id", diagnostic, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpiresAtIsMissing_LogsRejectedExpiryDiagnostic()
    {
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        is_valid = true,
                        user_id = "verified-facebook-user-id"
                    }
                });
            }

            return JsonResponse(new { id = "verified-facebook-user-id" });
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("token expired: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("expires_at diagnostic: MISSING", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: token expired or expires_at rejected", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("verified-facebook-user-id", diagnostic, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenProfileUserIdDoesNotMatchDebugToken_LogsSafeDiagnostic()
    {
        var logger = new RecordingLogger<FacebookAccessTokenValidator>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/debug_token")
            {
                return JsonResponse(new
                {
                    data = new
                    {
                        app_id = "configured-app-id",
                        type = "USER",
                        expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                        is_valid = true,
                        user_id = "debug-token-facebook-user-id"
                    }
                });
            }

            if (request.RequestUri?.AbsolutePath == "/me")
            {
                return JsonResponse(new { id = "profile-facebook-user-id" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var validator = new FacebookAccessTokenValidator(
            new HttpClient(handler),
            logger,
            enableDiagnostics: true);

        await Assert.ThrowsAsync<FacebookAccessTokenValidationException>(() =>
            validator.ValidateAsync(
                "facebook-access-token",
                "configured-app-id",
                "configured-app-secret"));

        var diagnostic = Assert.Single(logger.Messages);
        Assert.Contains("/me request succeeded: YES", diagnostic, StringComparison.Ordinal);
        Assert.Contains("/me user ID matches debug token user ID: NO", diagnostic, StringComparison.Ordinal);
        Assert.Contains("failed validation condition: /me user ID mismatch", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("debug-token-facebook-user-id", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("profile-facebook-user-id", diagnostic, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
    }
#endif

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(send(request));
        }
    }

#if DEBUG
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
#endif
}
