using backend.Models.Database;
using backend.Models.Users;
using backend.Repositories;
using backend.Services.Localization;

namespace backend.Services;

public sealed class UserProfileService(IUserProfileRepository userProfileRepository) : IUserProfileService
{
    private const string DefaultRole = "Passenger";
    private const string GuestRole = "Guest";
    private const string GuestProvider = "guest";
    private const int MaxCredentialOwnerLength = 255;
    private const int MaxProviderLength = 50;
    private const int MaxExternalAuthIdLength = 255;
    private const int MaxFirstNameLength = 100;
    private const int MaxLastNameLength = 100;
    private const int MaxPhoneNumberLength = 30;
    private const int MaxProfileImageUrlLength = 500;

    private readonly IUserProfileRepository _userProfileRepository = userProfileRepository;

    public async Task<UserProfileRegistrationResult> RegisterLocalProfileAsync(
        string userName,
        string firstName,
        string lastName,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateLocalRegistration(
            userName,
            firstName,
            lastName,
            phoneNumber);
        if (validation.Errors.Count > 0)
        {
            return UserProfileRegistrationResult.ValidationFailed(validation.Errors);
        }

        if (await _userProfileRepository.GetByEmailAsync(validation.UserName!, cancellationToken) is not null)
        {
            return UserProfileRegistrationResult.Duplicate(validation.UserName!);
        }

        var now = DateTime.UtcNow;
        var profile = await _userProfileRepository.AddOrUpdateAsync(new UserProfile
        {
            UserId = Guid.NewGuid(),
            Email = validation.UserName!,
            FirstName = validation.FirstName,
            LastName = validation.LastName,
            PhoneNumber = validation.PhoneNumber,
            Role = DefaultRole,
            PreferredLanguage = TukiLanguage.English,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        }, cancellationToken);

        return UserProfileRegistrationResult.Success(
            new UserProfileAuthenticationResult(
                profile.UserId,
                profile.Email,
                Map(profile)));
    }

    public async Task<UserProfileAuthenticationResult> CreateGuestProfileAsync(
        CancellationToken cancellationToken = default)
    {
        // Every guest receives a unique identity so active navigation, favorites and history are
        // isolated exactly like registered users. No password or real email is created.
        var guestSubject = Guid.NewGuid().ToString("N");
        var credentialOwner = CreateExternalCredentialOwner(GuestProvider, guestSubject);
        var now = DateTime.UtcNow;
        var profile = await _userProfileRepository.AddOrUpdateAsync(new UserProfile
        {
            UserId = Guid.NewGuid(),
            ExternalAuthProvider = GuestProvider,
            ExternalAuthId = guestSubject,
            Email = credentialOwner,
            FirstName = "Guest",
            LastName = null,
            PhoneNumber = null,
            Role = GuestRole,
            PreferredLanguage = TukiLanguage.English,
            IsActive = true,
            IsEmailVerified = false,
            CreatedAt = now,
            UpdatedAt = now,
        }, cancellationToken);

        return new UserProfileAuthenticationResult(
            profile.UserId,
            credentialOwner,
            Map(profile));
    }

    public async Task<UserProfileAuthenticationResult?> CreateOrUpdateExternalProfileAsync(
        string provider,
        string providerSubject,
        string? displayName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = NormalizeRequiredText(provider)?.ToLowerInvariant();
        var normalizedSubject = NormalizeRequiredText(providerSubject);
        if (normalizedProvider is null ||
            normalizedSubject is null ||
            normalizedProvider.Length > MaxProviderLength ||
            normalizedSubject.Length > MaxExternalAuthIdLength)
        {
            return null;
        }

        var credentialOwner = CreateExternalCredentialOwner(normalizedProvider, normalizedSubject);
        if (credentialOwner.Length > MaxCredentialOwnerLength)
        {
            return null;
        }

        var existingProfile = await _userProfileRepository.GetByExternalAuthAsync(
            normalizedProvider,
            normalizedSubject,
            cancellationToken)
            ?? await _userProfileRepository.GetByEmailAsync(credentialOwner, cancellationToken);

        var nameParts = SplitDisplayName(displayName);
        var now = DateTime.UtcNow;
        // Provider email is not stored separately because the schema has no provider-email field.
        var profile = await _userProfileRepository.AddOrUpdateAsync(new UserProfile
        {
            UserId = existingProfile?.UserId ?? Guid.NewGuid(),
            ExternalAuthProvider = normalizedProvider,
            ExternalAuthId = normalizedSubject,
            Email = credentialOwner,
            FirstName = existingProfile?.FirstName ?? nameParts.FirstName,
            LastName = existingProfile?.LastName ?? nameParts.LastName,
            PhoneNumber = existingProfile?.PhoneNumber,
            Role = existingProfile?.Role ?? DefaultRole,
            ProfileImageUrl = existingProfile?.ProfileImageUrl,
            PreferredLanguage = TukiLanguage.Normalize(existingProfile?.PreferredLanguage),
            IsActive = true,
            CreatedAt = existingProfile?.CreatedAt ?? now,
            UpdatedAt = now,
        }, cancellationToken);

        return new UserProfileAuthenticationResult(
            profile.UserId,
            credentialOwner,
            Map(profile));
    }

    public async Task<UserProfileAuthenticationResult?> GetAuthenticatedUserProfileAsync(
        string credentialOwner,
        CancellationToken cancellationToken = default)
    {
        var normalizedCredentialOwner = NormalizeRequiredText(credentialOwner);
        if (normalizedCredentialOwner is null)
        {
            return null;
        }

        UserProfile? profile = null;
        if (TryParseExternalCredentialOwner(
            normalizedCredentialOwner,
            out var provider,
            out var providerSubject))
        {
            profile = await _userProfileRepository.GetActiveByExternalAuthAsync(
                provider,
                providerSubject,
                cancellationToken);
        }

        profile ??= await _userProfileRepository.GetActiveByEmailAsync(
            normalizedCredentialOwner,
            cancellationToken);

        return profile is null
            ? null
            : new UserProfileAuthenticationResult(
                profile.UserId,
                normalizedCredentialOwner,
                Map(profile));
    }

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

    public async Task<UserProfileMutationResult> UpdatePreferredLanguageAsync(
        Guid userId,
        string? preferredLanguage,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return UserProfileMutationResult.NotFound(userId);
        }

        var requestedLanguage = preferredLanguage?.Trim();
        if (!string.Equals(requestedLanguage, TukiLanguage.English, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedLanguage, TukiLanguage.Filipino, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedLanguage, "Tagalog", StringComparison.OrdinalIgnoreCase))
        {
            return UserProfileMutationResult.ValidationFailed(
                ["Preferred language must be English or Filipino."]);
        }

        var profile = await _userProfileRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return UserProfileMutationResult.NotFound(userId);
        }

        profile.PreferredLanguage = TukiLanguage.Normalize(requestedLanguage);
        profile.UpdatedAt = DateTime.UtcNow;
        var saved = await _userProfileRepository.AddOrUpdateAsync(profile, cancellationToken);
        return UserProfileMutationResult.Success(Map(saved));
    }

    private static LocalRegistrationValidationResult ValidateLocalRegistration(
        string userName,
        string firstName,
        string lastName,
        string? phoneNumber)
    {
        var errors = new List<string>();
        var normalizedUserName = NormalizeRequiredText(userName);
        var normalizedFirstName = NormalizeRequiredText(firstName);
        var normalizedLastName = NormalizeRequiredText(lastName);
        var normalizedPhoneNumber = NormalizeOptionalText(phoneNumber);

        if (normalizedUserName is null)
        {
            errors.Add("User name is required.");
        }

        if (normalizedFirstName is null)
        {
            errors.Add("First name is required.");
        }

        if (normalizedLastName is null)
        {
            errors.Add("Last name is required.");
        }

        AddLengthError(errors, normalizedUserName, MaxCredentialOwnerLength, "User name");
        AddLengthError(errors, normalizedFirstName, MaxFirstNameLength, "First name");
        AddLengthError(errors, normalizedLastName, MaxLastNameLength, "Last name");
        AddLengthError(errors, normalizedPhoneNumber, MaxPhoneNumberLength, "Phone number");

        return new LocalRegistrationValidationResult(
            errors,
            normalizedUserName,
            normalizedFirstName,
            normalizedLastName,
            normalizedPhoneNumber);
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
            profile.UpdatedAt)
        {
            PreferredLanguage = TukiLanguage.Normalize(profile.PreferredLanguage)
        };

    private static string CreateExternalCredentialOwner(string provider, string providerSubject) =>
        $"{provider}:{providerSubject}";

    private static bool TryParseExternalCredentialOwner(
        string credentialOwner,
        out string provider,
        out string providerSubject)
    {
        provider = string.Empty;
        providerSubject = string.Empty;
        var separatorIndex = credentialOwner.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == credentialOwner.Length - 1)
        {
            return false;
        }

        provider = credentialOwner[..separatorIndex];
        providerSubject = credentialOwner[(separatorIndex + 1)..];
        return provider is "facebook" or "google" or GuestProvider;
    }

    private static NameParts SplitDisplayName(string? displayName)
    {
        var normalizedName = NormalizeOptionalText(displayName);
        if (normalizedName is null)
        {
            return new NameParts(null, null);
        }

        var parts = normalizedName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return new NameParts(
            parts.FirstOrDefault(),
            parts.Length > 1 ? parts[1] : null);
    }

    private static string? NormalizeRequiredText(string? value) =>
        NormalizeOptionalText(value);

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record LocalRegistrationValidationResult(
        IReadOnlyList<string> Errors,
        string? UserName,
        string? FirstName,
        string? LastName,
        string? PhoneNumber);

    private sealed record NameParts(string? FirstName, string? LastName);

    private sealed record UserProfileValidationResult(
        IReadOnlyList<string> Errors,
        string? FirstName,
        string? LastName,
        string? PhoneNumber,
        string? ProfileImageUrl);
}
