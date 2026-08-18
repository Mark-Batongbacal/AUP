using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Services;

namespace backend.Tests.Services;

public sealed class FacebookOidcTokenValidatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAsync_WhenTokenIsValid_ReturnsVerifiedFacebookSubject()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken();

        var user = await validator.ValidateAsync(
            token,
            "configured-facebook-app-id",
            "nonce-value");

        Assert.Equal("verified-facebook-subject", user.Subject);
        Assert.Equal("Verified User", user.Name);
        Assert.Equal("verified@example.test", user.Email);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ValidateAsync_WhenJwksKeyAlgorithmIsMissing_ReturnsVerifiedFacebookSubject()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.JwksWithoutAlgorithm));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken();

        var user = await validator.ValidateAsync(
            token,
            "configured-facebook-app-id",
            "nonce-value");

        Assert.Equal("verified-facebook-subject", user.Subject);
    }

    [Fact]
    public async Task ValidateAsync_WhenAudienceIsWrong_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken(audience: "different-facebook-app-id");

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuerIsWrong_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken(issuer: "https://malicious.example.test");

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenIsExpired_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken(expiresAt: Now.AddMinutes(-10));

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureIsInvalid_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        using var otherTokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = otherTokens.CreateToken(keyId: tokens.KeyId);

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectIsMissing_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken(includeSubject: false);

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceDoesNotMatch_RejectsToken()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ => JsonResponse(tokens.Jwks));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken(nonce: "different-nonce");

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    [Fact]
    public async Task ValidateAsync_WhenJwksRequestFails_ReportsValidationUnavailable()
    {
        using var tokens = new TestFacebookOidcTokens(Now);
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var validator = CreateValidator(handler);
        var token = tokens.CreateToken();

        await Assert.ThrowsAsync<FacebookOidcTokenValidationUnavailableException>(() =>
            validator.ValidateAsync(token, "configured-facebook-app-id", "nonce-value"));
    }

    private static FacebookOidcTokenValidator CreateValidator(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            jwksUri: "https://facebook.example.test/.well-known/oauth/openid/jwks/",
            timeProvider: new FixedTimeProvider(Now));

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private sealed class TestFacebookOidcTokens : IDisposable
    {
        private readonly DateTimeOffset _now;
        private readonly RSA _signingKey = RSA.Create(2048);

        public TestFacebookOidcTokens(DateTimeOffset now)
        {
            _now = now;
        }

        public string KeyId { get; } = "test-facebook-key-id";

        public object Jwks => CreateJwks(includeAlgorithm: true);

        public object JwksWithoutAlgorithm => CreateJwks(includeAlgorithm: false);

        private object CreateJwks(bool includeAlgorithm)
        {
            var parameters = _signingKey.ExportParameters(false);
            var key = new Dictionary<string, object?>
            {
                ["kid"] = KeyId,
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["n"] = Base64UrlEncode(parameters.Modulus!),
                ["e"] = Base64UrlEncode(parameters.Exponent!)
            };

            if (includeAlgorithm)
            {
                key["alg"] = "RS256";
            }

            return new
            {
                keys = new[]
                {
                    key
                }
            };
        }

        public string CreateToken(
            string issuer = FacebookOptions.DefaultOidcIssuer,
            object? audience = null,
            DateTimeOffset? expiresAt = null,
            DateTimeOffset? issuedAt = null,
            string nonce = "nonce-value",
            string keyId = "test-facebook-key-id",
            bool includeSubject = true)
        {
            var header = new Dictionary<string, object?>
            {
                ["alg"] = "RS256",
                ["kid"] = keyId,
                ["typ"] = "JWT"
            };
            var payload = new Dictionary<string, object?>
            {
                ["iss"] = issuer,
                ["aud"] = audience ?? "configured-facebook-app-id",
                ["exp"] = (expiresAt ?? _now.AddHours(1)).ToUnixTimeSeconds(),
                ["iat"] = (issuedAt ?? _now).ToUnixTimeSeconds(),
                ["nonce"] = nonce,
                ["name"] = "Verified User",
                ["email"] = "verified@example.test"
            };

            if (includeSubject)
            {
                payload["sub"] = "verified-facebook-subject";
            }

            var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
            var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
            var signedData = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}");
            var signature = _signingKey.SignData(
                signedData,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            return $"{encodedHeader}.{encodedPayload}.{Base64UrlEncode(signature)}";
        }

        public void Dispose()
        {
            _signingKey.Dispose();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

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

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
