using System.Security.Cryptography;
using System.Text;
using backend.Models.Database;
using backend.Services.Authentication.Login;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services.Authentication.ApiKey;

public sealed class SqlServerApiKeyService(
    TukiDbContext context,
    IOptions<LoginOptions> options) : IApiKeyService
{
    private readonly TukiDbContext _context = context;
    private readonly LoginOptions _options = options.Value;

    public IssuedApiKey Create(string userName) =>
        Create(userName, TimeSpan.FromHours(Math.Max(1, _options.ApiKeyLifetimeHours)));

    public IssuedApiKey Create(string userName, TimeSpan lifetime)
    {
        var credentialOwner = userName.Trim();
        if (string.IsNullOrWhiteSpace(credentialOwner))
        {
            throw new ArgumentException("Credential owner is required.", nameof(userName));
        }

        var safeLifetime = lifetime > TimeSpan.Zero ? lifetime : TimeSpan.FromHours(1);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(safeLifetime);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        _context.ApiKeySessions.Add(new ApiKeySession
        {
            KeyHash = HashKey(key),
            CredentialOwner = credentialOwner,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });
        _context.SaveChanges();

        return new IssuedApiKey(key, expiresAt);
    }

    public bool TryGetOwner(string apiKey, out string? userName)
    {
        userName = null;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var keyHash = HashKey(apiKey.Trim());
        var session = _context.ApiKeySessions
            .AsNoTracking()
            .FirstOrDefault(current => current.KeyHash == keyHash);
        if (session is null ||
            session.RevokedAt is not null ||
            session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        userName = session.CredentialOwner;
        return true;
    }

    private static string HashKey(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hash);
    }
}
