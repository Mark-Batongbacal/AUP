using backend.Models.Database;
using backend.Repositories;
using backend.Services;

namespace backend.Tests.Services.Users;

public sealed class ProfilePictureUpdateTests
{
    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenImageIsOmitted_PreservesExistingImage()
    {
        var userId = Guid.NewGuid();
        var repository = new InMemoryUserProfileRepository(new UserProfile
        {
            UserId = userId,
            Email = "user@example.test",
            FirstName = "Existing",
            LastName = "User",
            PhoneNumber = "+63 900 000 0000",
            Role = "Passenger",
            ProfileImageUrl = "https://example.test/profile.jpg",
            PreferredLanguage = "English",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        var service = new UserProfileService(repository);

        var result = await service.UpdateCurrentUserProfileAsync(
            userId,
            "Updated",
            null,
            "+63 911 111 1111",
            null);

        Assert.Equal(UserProfileMutationStatus.Success, result.Status);
        Assert.Equal("Updated", repository.Profile.FirstName);
        Assert.Equal("User", repository.Profile.LastName);
        Assert.Equal("+63 911 111 1111", repository.Profile.PhoneNumber);
        Assert.Equal("https://example.test/profile.jpg", repository.Profile.ProfileImageUrl);
    }

    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenOnlyImageChanges_PreservesExistingProfileFields()
    {
        var userId = Guid.NewGuid();
        var repository = new InMemoryUserProfileRepository(new UserProfile
        {
            UserId = userId,
            Email = "user@example.test",
            FirstName = "Existing",
            LastName = "User",
            PhoneNumber = "+63 900 000 0000",
            Role = "Passenger",
            ProfileImageUrl = null,
            PreferredLanguage = "English",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        var service = new UserProfileService(repository);

        var result = await service.UpdateCurrentUserProfileAsync(
            userId,
            null,
            null,
            null,
            "https://example.test/new-profile.jpg");

        Assert.Equal(UserProfileMutationStatus.Success, result.Status);
        Assert.Equal("Existing", repository.Profile.FirstName);
        Assert.Equal("User", repository.Profile.LastName);
        Assert.Equal("+63 900 000 0000", repository.Profile.PhoneNumber);
        Assert.Equal("https://example.test/new-profile.jpg", repository.Profile.ProfileImageUrl);
    }

    private sealed class InMemoryUserProfileRepository(UserProfile profile) : IUserProfileRepository
    {
        public UserProfile Profile { get; private set; } = profile;

        public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(Profile.UserId == userId ? Profile : null);

        public Task<UserProfile?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(Profile.UserId == userId && Profile.IsActive ? Profile : null);

        public Task<UserProfile?> GetByExternalAuthAsync(string provider, string externalAuthId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(null);

        public Task<UserProfile?> GetActiveByExternalAuthAsync(string provider, string externalAuthId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(null);

        public Task<UserProfile?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(Profile.Email == email && Profile.IsActive ? Profile : null);

        public Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(Profile.Email == email ? Profile : null);

        public Task<UserProfile> AddOrUpdateAsync(UserProfile value, CancellationToken cancellationToken = default)
        {
            Profile = value;
            return Task.FromResult(Profile);
        }

        public Task<UserProfile?> UpdateEditableFieldsAsync(
            Guid userId,
            string? firstName,
            string? lastName,
            string? phoneNumber,
            string? profileImageUrl,
            CancellationToken cancellationToken = default)
        {
            if (Profile.UserId != userId || !Profile.IsActive)
            {
                return Task.FromResult<UserProfile?>(null);
            }

            Profile.FirstName = firstName;
            Profile.LastName = lastName;
            Profile.PhoneNumber = phoneNumber;
            Profile.ProfileImageUrl = profileImageUrl;
            Profile.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult<UserProfile?>(Profile);
        }

        public Task<bool> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Profile.UserId == userId);
    }
}
