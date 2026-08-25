using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Services.Authentication.Facebook;

namespace backend.Tests.Services;

public sealed class FacebookLimitedLoginOidcTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 25, 5, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(FacebookOptions.DefaultOidcIssuer)]
    [InlineData(FacebookOptions.AlternateOidcIssuer)]
    public async Task ValidateAsync_WithLimitedLoginSigningKeys_AcceptsKnownFacebookIssuer(
        string issuer)
    {
        using var signingKey = RSA.Create(2048);
        const string keyId = "limited-login-test-key";
        var handler = new RecordingHandler(_ => JsonResponse(CreateJwks(signingKey, keyId)));
        var validator = new FacebookOidcTokenValidator(
            new HttpClient(handler),
            timeProvider: new FixedTimeProvider(Now));
        var token = CreateToken(signingKey, keyId, issuer);

        var user = await validator.ValidateAsync(
            token,
            "configured-facebook-app-id",
            "nonce-value");

        Assert.Equal("limited-facebook-subject", user.Subject);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("limited.facebook.com", request.RequestUri?.Host);
        Assert.Equal("/.well-known/oauth/openid/jwks/", request.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ValidateAsync_WithUntrustedIssuer_RejectsToken()
    {
        using var signingKey = RSA.Create(2048);
        const string keyId = "limited-login-test-key";
        var handler = new RecordingHandler(_ => JsonResponse(CreateJwks(signingKey, keyId)));
        var validator = new FacebookOidcTokenValidator(
            new HttpClient(handler),
            timeProvider: new FixedTimeProvider(Now));
        var token = CreateToken(signingKey, keyId, "https://malicious.example.test");

        await Assert.ThrowsAsync<FacebookOidcTokenValidationException>(() =>
            validator.ValidateAsync(
                token,
                "configured-facebook-app-id",
                "nonce-value"));
    }

    private static object CreateJwks(RSA signingKey, string keyId)
    {
        var parameters = signingKey.ExportParameters(false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kid = keyId,
                    kty = "RSA",
                    alg = "RS256",
                    use = "sig",
                    n = Base64UrlEncode(parameters.Modulus!),
                    e = Base64UrlEncode(parameters.Exponent!)
                }
            }
        };
    }

    private static string CreateToken(RSA signingKey, string keyId, string issuer)
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
            ["aud"] = "configured-facebook-app-id",
            ["sub"] = "limited-facebook-subject",
            ["exp"] = Now.AddHours(1).ToUnixTimeSeconds(),
            ["iat"] = Now.ToUnixTimeSeconds(),
            ["nonce"] = "nonce-value",
            ["name"] = "Limited Login User",
            ["email"] = "limited@example.test"
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signedData = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}");
        var signature = signingKey.SignData(
            signedData,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{encodedHeader}.{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class RecordingHandler(
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
