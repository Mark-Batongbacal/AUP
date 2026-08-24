namespace backend.Models.Database;

public sealed class ApiKeySession
{
    public long ApiKeySessionId { get; set; }

    public string KeyHash { get; set; } = null!;

    public string CredentialOwner { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
