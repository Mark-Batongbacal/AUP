using backend.Models.Users;

namespace backend.Services;

public interface IUserProfileService
{
    Task<UserProfileRegistrationResult> RegisterLocalProfileAsync(
        string userName,
        string firstName,
        string lastName,
        string? phoneNumber,
        CancellationToken cancellationToken = default);

    Task<UserProfileAuthenticationResult> CreateGuestProfileAsync(
        CancellationToken cancellationToken = default);

    Task<UserProfileAuthenticationResult?> CreateOrUpdateExternalProfileAsync(
        string provider,
        string providerSubject,
        string? displayName,
        string? email,
        CancellationToken cancellationToken = default);

    Task<UserProfileAuthenticationResult?> GetAuthenticatedUserProfileAsync(
        string credentialOwner,
        CancellationToken cancellationToken = default);

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

    Task<UserProfileMutationResult> UpdatePreferredLanguageAsync(
        Guid userId,
        string? preferredLanguage,
        CancellationToken cancellationToken = default);
}

public enum UserProfileRegistrationStatus
{
    Success,
    ValidationFailed,
    Duplicate,
}

public sealed record UserProfileRegistrationResult(
    UserProfileRegistrationStatus Status,
    IReadOnlyList<string> Errors,
    UserProfileAuthenticationResult? Authentication)
{
    public static UserProfileRegistrationResult Success(UserProfileAuthenticationResult authentication) =>
        new(UserProfileRegistrationStatus.Success, [], authentication);

    public static UserProfileRegistrationResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(UserProfileRegistrationStatus.ValidationFailed, errors, null);

    public static UserProfileRegistrationResult Duplicate(string userName) =>
        new(
            UserProfileRegistrationStatus.Duplicate,
            [$"A user profile with this email already exists."],
            null);
}

public sealed record UserProfileAuthenticationResult(
    Guid UserId,
    string CredentialOwner,
    UserProfileResponse Profile);

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
