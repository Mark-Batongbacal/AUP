using backend.Models.Users;

namespace backend.Services;

public interface IUserProfileService
{
    Task<UserProfileResponse?> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserProfileMutationResult> UpdateCurrentUserProfileAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? profileImageUrl,
        CancellationToken cancellationToken = default);
}

public enum UserProfileMutationStatus
{
    Success,
    ValidationFailed,
    NotFound,
}

public sealed record UserProfileMutationResult(
    UserProfileMutationStatus Status,
    IReadOnlyList<string> Errors,
    UserProfileResponse? Profile)
{
    public static UserProfileMutationResult Success(UserProfileResponse profile) =>
        new(UserProfileMutationStatus.Success, [], profile);

    public static UserProfileMutationResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(UserProfileMutationStatus.ValidationFailed, errors, null);

    public static UserProfileMutationResult NotFound(Guid userId) =>
        new(
            UserProfileMutationStatus.NotFound,
            [$"User profile {userId} was not found."],
            null);
}
