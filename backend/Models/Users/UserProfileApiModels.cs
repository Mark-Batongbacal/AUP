namespace backend.Models.Users;

public sealed record UpdateUserProfileRequest(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? ProfileImageUrl = null,
    string? PreferredLanguage = null);

public sealed record UserProfileResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string Role,
    string? ProfileImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public string? Email { get; init; }
    public int TripsTaken { get; init; }
    public int FavoritesCount { get; init; }
    public string PreferredLanguage { get; init; } = "English";
}

public sealed record UserProfileErrorResponse(IReadOnlyList<string> Errors);
