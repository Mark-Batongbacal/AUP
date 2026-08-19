namespace backend.Models.Database;

public sealed class LocalUserCredential
{
    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public UserProfile User { get; set; } = null!;
}
