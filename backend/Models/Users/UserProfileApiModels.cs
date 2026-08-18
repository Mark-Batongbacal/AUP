namespace backend.Models.Users;

public sealed record UpdateUserProfileRequest(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? ProfileImageUrl = null);

public sealed record UserProfileResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string Role,
    string? ProfileImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UserProfileErrorResponse(IReadOnlyList<string> Errors);
