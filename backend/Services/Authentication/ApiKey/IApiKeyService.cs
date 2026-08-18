using System.Collections.Concurrent;
using System.Security.Cryptography;
using backend.Services.Authentication.Login;
using Microsoft.Extensions.Options;

namespace backend.Services.Authentication.ApiKey;

public interface IApiKeyService
{
    IssuedApiKey Create(string userName);
    bool TryGetOwner(string apiKey, out string? userName);
}

public sealed record IssuedApiKey(string Value, DateTimeOffset ExpiresAt);

public sealed class InMemoryApiKeyService(IOptions<LoginOptions> options) : IApiKeyService
{
    private readonly ConcurrentDictionary<string, ApiKeyEntry> _keys = new();
    private readonly LoginOptions _options = options.Value;

    public IssuedApiKey Create(string userName)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _options.ApiKeyLifetimeHours));
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        _keys[key] = new ApiKeyEntry(userName, expiresAt);
        return new IssuedApiKey(key, expiresAt);
    }

    public bool TryGetOwner(string apiKey, out string? userName)
    {
        userName = null;
        if (!_keys.TryGetValue(apiKey, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _keys.TryRemove(apiKey, out _);
            return false;
        }

        userName = entry.UserName;
        return true;
    }

    private sealed record ApiKeyEntry(string UserName, DateTimeOffset ExpiresAt);
}
