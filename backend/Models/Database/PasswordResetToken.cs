namespace backend.Models.Database;

public sealed class PasswordResetToken
{
    public Guid PasswordResetTokenId { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public UserProfile User { get; set; } = null!;
}
