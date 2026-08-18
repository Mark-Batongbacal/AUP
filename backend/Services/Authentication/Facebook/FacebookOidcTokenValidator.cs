using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Services.Authentication.Facebook;

public sealed class FacebookOidcTokenValidator : IFacebookOidcTokenValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly string _issuer;
    private readonly Uri _jwksUri;
    private readonly TimeProvider _timeProvider;

    public FacebookOidcTokenValidator(
        HttpClient httpClient,
        string issuer = FacebookOptions.DefaultOidcIssuer,
        string jwksUri = FacebookOptions.DefaultOidcJwksUri,
        TimeProvider? timeProvider = null)
    {
        if (!Uri.TryCreate(jwksUri, UriKind.Absolute, out var parsedJwksUri) ||
            parsedJwksUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Facebook OIDC JWKS URI must be an absolute HTTPS URI.", nameof(jwksUri));
        }

        _httpClient = httpClient;
        _issuer = issuer;
        _jwksUri = parsedJwksUri;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<FacebookOidcUserInfo> ValidateAsync(
        string idToken,
        string appId,
        string nonce,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) ||
            string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(nonce))
        {
            throw new FacebookOidcTokenValidationException();
        }

        var parsedToken = ParseToken(idToken);
        var jwks = await GetJwksAsync(cancellationToken);
        var key = jwks.Keys.FirstOrDefault(candidate =>
            string.Equals(candidate.KeyId, parsedToken.Header.KeyId, StringComparison.Ordinal));

        if (key is null || !VerifySignature(parsedToken, key))
        {
            throw new FacebookOidcTokenValidationException();
        }

        ValidateClaims(parsedToken.Payload, appId, nonce);

        return new FacebookOidcUserInfo(
            parsedToken.Payload.Subject!,
            parsedToken.Payload.Name,
            parsedToken.Payload.Email);
    }

    private static ParsedFacebookOidcToken ParseToken(string idToken)
    {
        var parts = idToken.Trim().Split('.');
        if (parts.Length != 3 ||
            parts.Any(part => string.IsNullOrWhiteSpace(part)))
        {
            throw new FacebookOidcTokenValidationException();
        }

        try
        {
            var header = JsonSerializer.Deserialize<FacebookOidcJwtHeader>(
                Base64UrlDecode(parts[0]),
                JsonOptions);
            var payload = JsonSerializer.Deserialize<FacebookOidcJwtPayload>(
                Base64UrlDecode(parts[1]),
                JsonOptions);

            if (header is null ||
                payload is null ||
                !string.Equals(header.Algorithm, "RS256", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(header.KeyId))
            {
                throw new FacebookOidcTokenValidationException();
            }

            return new ParsedFacebookOidcToken(
                parts[0],
                parts[1],
                parts[2],
                header,
                payload);
        }
        catch (FacebookOidcTokenValidationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new FacebookOidcTokenValidationException(ex);
        }
    }

    private async Task<FacebookJwksResponse> GetJwksAsync(CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(_jwksUri, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FacebookOidcTokenValidationUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FacebookOidcTokenValidationUnavailableException(ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new FacebookOidcTokenValidationUnavailableException();
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                var jwks = await JsonSerializer.DeserializeAsync<FacebookJwksResponse>(
                    body,
                    JsonOptions,
                    cancellationToken);

                if (jwks?.Keys is null || jwks.Keys.Count == 0)
                {
                    throw new FacebookOidcTokenValidationUnavailableException();
                }

                return jwks;
            }
            catch (FacebookOidcTokenValidationUnavailableException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw new FacebookOidcTokenValidationUnavailableException(ex);
            }
        }
    }

    private static bool VerifySignature(
        ParsedFacebookOidcToken token,
        FacebookJsonWebKey key)
    {
        if (!string.Equals(key.KeyType, "RSA", StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(key.Algorithm) &&
                !string.Equals(key.Algorithm, "RS256", StringComparison.Ordinal)) ||
            string.IsNullOrWhiteSpace(key.Modulus) ||
            string.IsNullOrWhiteSpace(key.Exponent))
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(key.Modulus),
                Exponent = Base64UrlDecode(key.Exponent)
            });

            var signedData = Encoding.ASCII.GetBytes($"{token.EncodedHeader}.{token.EncodedPayload}");
            var signature = Base64UrlDecode(token.EncodedSignature);
            return rsa.VerifyData(
                signedData,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private void ValidateClaims(
        FacebookOidcJwtPayload payload,
        string appId,
        string nonce)
    {
        var now = _timeProvider.GetUtcNow();

        if (!string.Equals(payload.Issuer, _issuer, StringComparison.Ordinal) ||
            !AudienceContains(payload.Audience, appId) ||
            string.IsNullOrWhiteSpace(payload.Subject) ||
            !string.Equals(payload.Nonce, nonce, StringComparison.Ordinal) ||
            !IsFutureUnixTimestamp(payload.ExpiresAt, now) ||
            !IsIssuedAtValid(payload.IssuedAt, now) ||
            !IsNotBeforeValid(payload.NotBefore, now))
        {
            throw new FacebookOidcTokenValidationException();
        }
    }

    private static bool AudienceContains(JsonElement audience, string appId)
    {
        return audience.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audience.GetString(), appId, StringComparison.Ordinal),
            JsonValueKind.Array => audience.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), appId, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool IsFutureUnixTimestamp(long? timestamp, DateTimeOffset now)
    {
        if (timestamp is null or <= 0)
        {
            return false;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value) > now.Subtract(ClockSkew);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsIssuedAtValid(long? timestamp, DateTimeOffset now)
    {
        if (timestamp is null or <= 0)
        {
            return false;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value) <= now.Add(ClockSkew);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsNotBeforeValid(long? timestamp, DateTimeOffset now)
    {
        if (timestamp is null)
        {
            return true;
        }

        if (timestamp <= 0)
        {
            return false;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value) <= now.Add(ClockSkew);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            0 => padded,
            2 => $"{padded}==",
            3 => $"{padded}=",
            _ => throw new FormatException("Invalid base64url value.")
        };

        return Convert.FromBase64String(padded);
    }

    private sealed record ParsedFacebookOidcToken(
        string EncodedHeader,
        string EncodedPayload,
        string EncodedSignature,
        FacebookOidcJwtHeader Header,
        FacebookOidcJwtPayload Payload);

    private sealed class FacebookOidcJwtHeader
    {
        [JsonPropertyName("alg")]
        public string? Algorithm { get; init; }

        [JsonPropertyName("kid")]
        public string? KeyId { get; init; }
    }

    private sealed class FacebookOidcJwtPayload
    {
        [JsonPropertyName("iss")]
        public string? Issuer { get; init; }

        [JsonPropertyName("aud")]
        public JsonElement Audience { get; init; }

        [JsonPropertyName("sub")]
        public string? Subject { get; init; }

        [JsonPropertyName("exp")]
        public long? ExpiresAt { get; init; }

        [JsonPropertyName("iat")]
        public long? IssuedAt { get; init; }

        [JsonPropertyName("nbf")]
        public long? NotBefore { get; init; }

        [JsonPropertyName("nonce")]
        public string? Nonce { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }
    }

    private sealed class FacebookJwksResponse
    {
        [JsonPropertyName("keys")]
        public List<FacebookJsonWebKey> Keys { get; init; } = [];
    }

    private sealed class FacebookJsonWebKey
    {
        [JsonPropertyName("kid")]
        public string? KeyId { get; init; }

        [JsonPropertyName("kty")]
        public string? KeyType { get; init; }

        [JsonPropertyName("alg")]
        public string? Algorithm { get; init; }

        [JsonPropertyName("n")]
        public string? Modulus { get; init; }

        [JsonPropertyName("e")]
        public string? Exponent { get; init; }
    }
}
