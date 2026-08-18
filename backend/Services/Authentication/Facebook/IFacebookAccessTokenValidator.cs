using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace backend.Services.Authentication.Facebook;

public interface IFacebookAccessTokenValidator
{
    Task<FacebookUserInfo> ValidateAsync(
        string accessToken,
        string appId,
        string appSecret,
        CancellationToken cancellationToken = default);
}

public sealed record FacebookUserInfo(string UserId, string? Name, string? Email);

public sealed class FacebookAccessTokenValidationException : Exception
{
    public FacebookAccessTokenValidationException()
        : base("The Facebook access token is invalid.")
    {
    }

    public FacebookAccessTokenValidationException(Exception innerException)
        : base("The Facebook access token is invalid.", innerException)
    {
    }
}

public sealed class FacebookTokenValidationUnavailableException : Exception
{
    public FacebookTokenValidationUnavailableException()
        : base("Facebook token validation is unavailable.")
    {
    }

    public FacebookTokenValidationUnavailableException(Exception innerException)
        : base("Facebook token validation is unavailable.", innerException)
    {
    }
}

public sealed class FacebookAccessTokenValidator : IFacebookAccessTokenValidator
{
    private static readonly Uri GraphBaseUri = new("https://graph.facebook.com/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

#if DEBUG
    private readonly bool _enableDiagnostics;
    private readonly ILogger<FacebookAccessTokenValidator>? _logger;
#endif

    public FacebookAccessTokenValidator(
        HttpClient httpClient
#if DEBUG
        ,
        ILogger<FacebookAccessTokenValidator>? logger = null,
        bool enableDiagnostics = false
#endif
    )
    {
        _httpClient = httpClient;
#if DEBUG
        _logger = logger;
        _enableDiagnostics = enableDiagnostics;
#endif
    }

    public async Task<FacebookUserInfo> ValidateAsync(
        string accessToken,
        string appId,
        string appSecret,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        var diagnostic = new FacebookValidationDiagnostic
        {
            ConfiguredFacebookAppIdPresent = !string.IsNullOrWhiteSpace(appId),
            ConfiguredFacebookAppSecretPresent = !string.IsNullOrWhiteSpace(appSecret),
            IncomingToken = FacebookIncomingTokenDiagnostic.Capture(accessToken)
        };
#endif

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(appSecret))
        {
#if DEBUG
            LogDiagnostic(diagnostic, "missing validator input");
#endif
            throw new FacebookAccessTokenValidationException();
        }

        FacebookDebugTokenData debugData;
        try
        {
            debugData = await GetDebugTokenDataAsync(
                accessToken,
                appId,
                appSecret,
                cancellationToken
#if DEBUG
                ,
                diagnostic
#endif
            );
        }
        catch (FacebookAccessTokenValidationException)
        {
#if DEBUG
            LogDiagnostic(
                diagnostic,
                diagnostic.DebugTokenHttpRequestSucceeded
                    ? "debug_token response missing data"
                    : "debug_token HTTP request failed");
#endif
            throw;
        }
        catch (FacebookTokenValidationUnavailableException)
        {
#if DEBUG
            LogDiagnostic(
                diagnostic,
                diagnostic.DebugTokenHttpRequestSucceeded
                    ? "debug_token response malformed"
                    : "debug_token unavailable");
#endif
            throw;
        }

#if DEBUG
        diagnostic.CaptureDebugTokenData(debugData, appId, accessToken);
#endif

        var debugTokenFailure = GetDebugTokenValidationFailure(debugData, appId);
        if (debugTokenFailure is not null)
        {
#if DEBUG
            LogDiagnostic(diagnostic, debugTokenFailure);
#endif
            throw new FacebookAccessTokenValidationException();
        }

        FacebookProfileResponse? profile;
        try
        {
            profile = await GetProfileAsync(
                accessToken,
                appSecret,
                cancellationToken
#if DEBUG
                ,
                diagnostic
#endif
            );
        }
        catch (FacebookAccessTokenValidationException)
        {
#if DEBUG
            LogDiagnostic(diagnostic, "/me HTTP request failed");
#endif
            throw;
        }
        catch (FacebookTokenValidationUnavailableException)
        {
#if DEBUG
            LogDiagnostic(diagnostic, "/me response malformed or unavailable");
#endif
            throw;
        }

#if DEBUG
        diagnostic.MeUserIdMatchesDebugTokenUserId = profile is not null &&
            string.Equals(profile.Id, debugData.UserId, StringComparison.Ordinal);
#endif

        if (profile is null ||
            !string.Equals(profile.Id, debugData.UserId, StringComparison.Ordinal))
        {
#if DEBUG
            LogDiagnostic(
                diagnostic,
                profile is null
                    ? "/me response missing profile"
                    : "/me user ID mismatch");
#endif
            throw new FacebookAccessTokenValidationException();
        }

#if DEBUG
        LogDiagnostic(diagnostic, "NONE");
#endif

        return new FacebookUserInfo(debugData.UserId!, profile.Name, profile.Email);
    }

    private async Task<FacebookDebugTokenData> GetDebugTokenDataAsync(
        string accessToken,
        string appId,
        string appSecret,
        CancellationToken cancellationToken
#if DEBUG
        ,
        FacebookValidationDiagnostic? diagnostic
#endif
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildGraphUri("debug_token", new Dictionary<string, string>
            {
                ["input_token"] = accessToken,
                ["appsecret_proof"] = CreateAppSecretProof($"{appId}|{appSecret}", appSecret)
            }));
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {appId}|{appSecret}");

        using var response = await SendAsync(request, cancellationToken);
#if DEBUG
        if (diagnostic is not null)
        {
            diagnostic.DebugTokenHttpRequestSucceeded = true;
        }
#endif
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<FacebookDebugTokenResponse>(
                body,
                JsonOptions,
                cancellationToken);

            return payload?.Data ?? throw new FacebookAccessTokenValidationException();
        }
        catch (JsonException ex)
        {
            throw new FacebookTokenValidationUnavailableException(ex);
        }
    }

    private async Task<FacebookProfileResponse?> GetProfileAsync(
        string accessToken,
        string appSecret,
        CancellationToken cancellationToken
#if DEBUG
        ,
        FacebookValidationDiagnostic? diagnostic
#endif
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildGraphUri("me", new Dictionary<string, string>
            {
                ["fields"] = "id,name,email",
                ["appsecret_proof"] = CreateAppSecretProof(accessToken, appSecret)
            }));
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");

        using var response = await SendAsync(request, cancellationToken);
#if DEBUG
        if (diagnostic is not null)
        {
            diagnostic.MeRequestSucceeded = true;
        }
#endif
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            return await JsonSerializer.DeserializeAsync<FacebookProfileResponse>(
                body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new FacebookTokenValidationUnavailableException(ex);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = response.StatusCode;
            response.Dispose();
            if ((int)statusCode >= StatusCodes.Status500InternalServerError ||
                statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            {
                throw new FacebookTokenValidationUnavailableException();
            }

            throw new FacebookAccessTokenValidationException();
        }
        catch (FacebookAccessTokenValidationException)
        {
            throw;
        }
        catch (FacebookTokenValidationUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new FacebookTokenValidationUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FacebookTokenValidationUnavailableException(ex);
        }
    }

    private static bool IsVerifiedUserToken(FacebookDebugTokenData? data, string expectedAppId)
    {
        return GetDebugTokenValidationFailure(data, expectedAppId) is null;
    }

    private static string? GetDebugTokenValidationFailure(
        FacebookDebugTokenData? data,
        string expectedAppId)
    {
        if (data is null)
        {
            return "debug_token response missing data";
        }

        if (data.IsValid != true)
        {
            return "debug_token is_valid false";
        }

        if (!string.Equals(data.AppId, expectedAppId, StringComparison.Ordinal))
        {
            return "token app ID mismatch";
        }

        if (!string.Equals(data.Type, "USER", StringComparison.OrdinalIgnoreCase))
        {
            return "token type not accepted";
        }

        if (string.IsNullOrWhiteSpace(data.UserId))
        {
            return "verified user ID missing";
        }

        if (!IsFutureUnixTimestamp(data.ExpiresAt))
        {
            return "token expired or expires_at rejected";
        }

        return null;
    }

    private static bool IsFutureUnixTimestamp(long? expiresAt)
    {
        if (expiresAt is null or <= 0)
        {
            return false;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(expiresAt.Value) > DateTimeOffset.UtcNow;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static Uri BuildGraphUri(string path, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(new Uri(GraphBaseUri, path))
        {
            Query = string.Join(
                '&',
                query.Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"))
        };

        return builder.Uri;
    }

    private static string CreateAppSecretProof(string accessToken, string appSecret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(accessToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FacebookDebugTokenResponse
    {
        public FacebookDebugTokenData? Data { get; init; }
    }

    private sealed class FacebookDebugTokenData
    {
        [JsonPropertyName("app_id")]
        public string? AppId { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; init; }

        [JsonPropertyName("is_valid")]
        public bool? IsValid { get; init; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        [JsonPropertyName("scopes")]
        public string[]? Scopes { get; init; }

        [JsonPropertyName("error")]
        public FacebookDebugTokenError? Error { get; init; }
    }

    private sealed class FacebookDebugTokenError
    {
        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed class FacebookProfileResponse
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public string? Email { get; init; }
    }

#if DEBUG
    private void LogDiagnostic(FacebookValidationDiagnostic diagnostic, string failedValidationCondition)
    {
        if (!_enableDiagnostics || _logger is null)
        {
            return;
        }

        diagnostic.FailedValidationCondition = failedValidationCondition;
        _logger.LogWarning(
            """
            Facebook access token validation diagnostic:
            token present: {TokenPresent}
            token length: {TokenLength}
            contains '.' separators: {ContainsDotSeparators}
            separator count: {SeparatorCount}
            token looks JWT-shaped: {LooksJwtShaped}
            token begins with expected Facebook opaque-token style: {OpaqueTokenStyle}
            Android/iOS token classification: {TokenClassification}
            debug_token HTTP request succeeded: {DebugTokenHttpRequestSucceeded}
            is_valid: {IsValid}
            token app ID matches configured app ID: {TokenAppIdMatchesConfiguredAppId}
            configured Facebook App ID present: {ConfiguredFacebookAppIdPresent}
            configured Facebook App Secret present: {ConfiguredFacebookAppSecretPresent}
            token type: {TokenType}
            token type accepted: {TokenTypeAccepted}
            token expired: {TokenExpired}
            expires_at diagnostic: {ExpiresAtDiagnostic}
            verified user ID present: {VerifiedUserIdPresent}
            scopes returned: {ScopesReturned}
            Meta error code: {MetaErrorCode}
            Meta error type: {MetaErrorType}
            Meta error message: {MetaErrorMessage}
            /me request succeeded: {MeRequestSucceeded}
            /me user ID matches debug token user ID: {MeUserIdMatchesDebugTokenUserId}
            failed validation condition: {FailedValidationCondition}
            """,
            diagnostic.YesNo(diagnostic.IncomingToken.TokenPresent),
            diagnostic.IncomingToken.TokenLength,
            diagnostic.YesNo(diagnostic.IncomingToken.ContainsDotSeparators),
            diagnostic.IncomingToken.SeparatorCount,
            diagnostic.YesNo(diagnostic.IncomingToken.LooksJwtShaped),
            diagnostic.IncomingToken.OpaqueTokenStyle,
            diagnostic.IncomingToken.Classification,
            diagnostic.YesNo(diagnostic.DebugTokenHttpRequestSucceeded),
            diagnostic.TrueFalse(diagnostic.IsValid),
            diagnostic.YesNo(diagnostic.TokenAppIdMatchesConfiguredAppId),
            diagnostic.YesNo(diagnostic.ConfiguredFacebookAppIdPresent),
            diagnostic.YesNo(diagnostic.ConfiguredFacebookAppSecretPresent),
            diagnostic.TokenType,
            diagnostic.YesNo(diagnostic.TokenTypeAccepted),
            diagnostic.YesNo(diagnostic.TokenExpired),
            diagnostic.ExpiresAtDiagnostic,
            diagnostic.YesNo(diagnostic.VerifiedUserIdPresent),
            diagnostic.ScopesReturned,
            diagnostic.MetaErrorCode,
            diagnostic.MetaErrorType,
            diagnostic.MetaErrorMessage,
            diagnostic.YesNo(diagnostic.MeRequestSucceeded),
            diagnostic.YesNo(diagnostic.MeUserIdMatchesDebugTokenUserId),
            diagnostic.FailedValidationCondition);
    }

    private sealed class FacebookValidationDiagnostic
    {
        public FacebookIncomingTokenDiagnostic IncomingToken { get; init; } =
            FacebookIncomingTokenDiagnostic.Capture(null);

        public bool DebugTokenHttpRequestSucceeded { get; set; }

        public bool? IsValid { get; private set; }

        public bool? TokenAppIdMatchesConfiguredAppId { get; private set; }

        public bool ConfiguredFacebookAppIdPresent { get; init; }

        public bool ConfiguredFacebookAppSecretPresent { get; init; }

        public string TokenType { get; private set; } = "MISSING";

        public bool? TokenTypeAccepted { get; private set; }

        public bool? TokenExpired { get; private set; }

        public string ExpiresAtDiagnostic { get; private set; } = "NOT CHECKED";

        public bool? VerifiedUserIdPresent { get; private set; }

        public string ScopesReturned { get; private set; } = "NOT CHECKED";

        public string MetaErrorCode { get; private set; } = "NONE";

        public string MetaErrorType { get; private set; } = "NONE";

        public string MetaErrorMessage { get; private set; } = "NONE";

        public bool MeRequestSucceeded { get; set; }

        public bool? MeUserIdMatchesDebugTokenUserId { get; set; }

        public string? FailedValidationCondition { get; set; }

        public void CaptureDebugTokenData(
            FacebookDebugTokenData data,
            string expectedAppId,
            string accessToken)
        {
            IsValid = data.IsValid;
            TokenAppIdMatchesConfiguredAppId =
                string.Equals(data.AppId, expectedAppId, StringComparison.Ordinal);
            TokenType = string.IsNullOrWhiteSpace(data.Type) ? "MISSING" : data.Type;
            TokenTypeAccepted = string.Equals(data.Type, "USER", StringComparison.OrdinalIgnoreCase);
            VerifiedUserIdPresent = !string.IsNullOrWhiteSpace(data.UserId);
            TokenExpired = !IsFutureUnixTimestamp(data.ExpiresAt);
            ExpiresAtDiagnostic = GetExpiresAtDiagnostic(data.ExpiresAt);
            ScopesReturned = FormatScopes(data.Scopes);
            CaptureMetaError(data.Error, accessToken, expectedAppId);
        }

        public string YesNo(bool value) => value ? "YES" : "NO";

        public string YesNo(bool? value) => value switch
        {
            true => "YES",
            false => "NO",
            null => "UNKNOWN"
        };

        public string TrueFalse(bool? value) => value switch
        {
            true => "TRUE",
            false => "FALSE",
            null => "UNKNOWN"
        };

        private static string GetExpiresAtDiagnostic(long? expiresAt)
        {
            if (expiresAt is null)
            {
                return "MISSING";
            }

            if (expiresAt <= 0)
            {
                return "ZERO_OR_NEGATIVE";
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(expiresAt.Value) > DateTimeOffset.UtcNow
                    ? "FUTURE"
                    : "EXPIRED";
            }
            catch (ArgumentOutOfRangeException)
            {
                return "OUT_OF_RANGE";
            }
        }

        private void CaptureMetaError(
            FacebookDebugTokenError? error,
            string accessToken,
            string expectedAppId)
        {
            if (error is null)
            {
                return;
            }

            MetaErrorCode = error.Code?.ToString() ?? "UNKNOWN";
            MetaErrorType = SafeShortValue(error.Type);
            MetaErrorMessage = SafeMetaErrorMessage(error.Message, accessToken, expectedAppId);
        }

        private static string FormatScopes(IReadOnlyCollection<string>? scopes)
        {
            if (scopes is null)
            {
                return "MISSING";
            }

            var safeScopes = scopes
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (safeScopes.Length == 0)
            {
                return "NONE";
            }

            return safeScopes.All(IsSafeScopeName)
                ? string.Join(",", safeScopes)
                : "PRESENT_UNSAFE";
        }

        private static bool IsSafeScopeName(string scope) =>
            scope.Length <= 64 &&
            scope.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '_' or '-' or '.');

        private static string SafeShortValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NONE";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= 80 && IsSafeDiagnosticText(trimmed)
                ? trimmed
                : "PRESENT_UNSAFE";
        }

        private static string SafeMetaErrorMessage(
            string? message,
            string accessToken,
            string expectedAppId)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "NONE";
            }

            var trimmed = message.Trim();
            if (ContainsSensitiveValue(trimmed, accessToken) ||
                ContainsSensitiveValue(trimmed, expectedAppId) ||
                !IsSafeDiagnosticText(trimmed))
            {
                return "PRESENT_UNSAFE";
            }

            return trimmed;
        }

        private static bool ContainsSensitiveValue(string message, string sensitiveValue) =>
            !string.IsNullOrWhiteSpace(sensitiveValue) &&
            message.Contains(sensitiveValue, StringComparison.Ordinal);

        private static bool IsSafeDiagnosticText(string value)
        {
            if (value.Length > 240)
            {
                return false;
            }

            return !Regex.IsMatch(value, @"[A-Za-z0-9_-]{24,}") &&
                !Regex.IsMatch(value, @"[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*");
        }
    }

    private sealed class FacebookIncomingTokenDiagnostic
    {
        private const string ExpectedFacebookOpaquePrefix = "EAA";

        public bool TokenPresent { get; private init; }

        public int TokenLength { get; private init; }

        public bool ContainsDotSeparators { get; private init; }

        public int SeparatorCount { get; private init; }

        public bool LooksJwtShaped { get; private init; }

        public string OpaqueTokenStyle { get; private init; } = "UNKNOWN";

        public string Classification { get; private init; } = "UNKNOWN";

        public static FacebookIncomingTokenDiagnostic Capture(string? accessToken)
        {
            var trimmedToken = accessToken?.Trim();
            if (string.IsNullOrEmpty(trimmedToken))
            {
                return new FacebookIncomingTokenDiagnostic();
            }

            var separatorCount = trimmedToken.Count(character => character == '.');
            var looksJwtShaped = HasJwtShape(trimmedToken, separatorCount);
            var opaqueTokenStyle = ClassifyOpaqueTokenStyle(trimmedToken, separatorCount);

            return new FacebookIncomingTokenDiagnostic
            {
                TokenPresent = true,
                TokenLength = trimmedToken.Length,
                ContainsDotSeparators = separatorCount > 0,
                SeparatorCount = separatorCount,
                LooksJwtShaped = looksJwtShaped,
                OpaqueTokenStyle = opaqueTokenStyle,
                Classification = looksJwtShaped
                    ? "JWT-LIKE"
                    : separatorCount == 0
                        ? "OPAQUE"
                        : "UNKNOWN"
            };
        }

        private static bool HasJwtShape(string token, int separatorCount)
        {
            if (separatorCount != 2)
            {
                return false;
            }

            var segments = token.Split('.');
            return segments.Length == 3 &&
                segments[0].Length > 0 &&
                segments[1].Length > 0 &&
                segments.All(IsBase64UrlSegment);
        }

        private static bool IsBase64UrlSegment(string segment) =>
            segment.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_');

        private static string ClassifyOpaqueTokenStyle(string token, int separatorCount)
        {
            if (separatorCount != 0)
            {
                return "NO";
            }

            return token.StartsWith(ExpectedFacebookOpaquePrefix, StringComparison.Ordinal)
                ? "YES"
                : "NO";
        }
    }
#endif
}
