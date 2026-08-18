using backend.Models.Database;
using backend.Models.Users;
using backend.Repositories;

namespace backend.Services;

public sealed class UserProfileService(IUserProfileRepository userProfileRepository) : IUserProfileService
{
    private const int MaxFirstNameLength = 100;
    private const int MaxLastNameLength = 100;
    private const int MaxPhoneNumberLength = 30;
    private const int MaxProfileImageUrlLength = 500;

    private readonly IUserProfileRepository _userProfileRepository = userProfileRepository;

    public async Task<UserProfileResponse?> GetCurrentUserProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var profile = await _userProfileRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<UserProfileMutationResult> UpdateCurrentUserProfileAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? profileImageUrl,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return UserProfileMutationResult.NotFound(userId);
        }

        var validation = Validate(
            firstName,
            lastName,
            phoneNumber,
            profileImageUrl);
        if (validation.Errors.Count > 0)
        {
            return UserProfileMutationResult.ValidationFailed(validation.Errors);
        }

        var profile = await _userProfileRepository.UpdateEditableFieldsAsync(
            userId,
            validation.FirstName,
            validation.LastName,
            validation.PhoneNumber,
            validation.ProfileImageUrl,
            cancellationToken);

        return profile is null
            ? UserProfileMutationResult.NotFound(userId)
            : UserProfileMutationResult.Success(Map(profile));
    }

    private static UserProfileValidationResult Validate(
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? profileImageUrl)
    {
        var errors = new List<string>();
        if (firstName is null &&
            lastName is null &&
            phoneNumber is null &&
            profileImageUrl is null)
        {
            errors.Add("At least one editable profile field is required.");
        }

        var normalizedFirstName = NormalizeOptionalText(firstName);
        var normalizedLastName = NormalizeOptionalText(lastName);
        var normalizedPhoneNumber = NormalizeOptionalText(phoneNumber);
        var normalizedProfileImageUrl = NormalizeOptionalText(profileImageUrl);

        AddLengthError(errors, normalizedFirstName, MaxFirstNameLength, "First name");
        AddLengthError(errors, normalizedLastName, MaxLastNameLength, "Last name");
        AddLengthError(errors, normalizedPhoneNumber, MaxPhoneNumberLength, "Phone number");
        AddLengthError(errors, normalizedProfileImageUrl, MaxProfileImageUrlLength, "Profile image URL");

        return new UserProfileValidationResult(
            errors,
            normalizedFirstName,
            normalizedLastName,
            normalizedPhoneNumber,
            normalizedProfileImageUrl);
    }

    private static void AddLengthError(
        ICollection<string> errors,
        string? value,
        int maxLength,
        string label)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors.Add($"{label} must be {maxLength} characters or fewer.");
        }
    }

    private static UserProfileResponse Map(UserProfile profile) =>
        new(
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.PhoneNumber,
            profile.Role,
            profile.ProfileImageUrl,
            profile.CreatedAt,
            profile.UpdatedAt);

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record UserProfileValidationResult(
        IReadOnlyList<string> Errors,
        string? FirstName,
        string? LastName,
        string? PhoneNumber,
        string? ProfileImageUrl);
}
