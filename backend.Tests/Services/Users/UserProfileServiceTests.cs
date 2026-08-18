using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Users;

public sealed class UserProfileServiceTests
{
    [Fact]
    public async Task GetCurrentUserProfileAsync_WhenProfileExists_ReturnsProfileResponse()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = CreateProfile(userId);
        context.Repository
            .Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await context.Service.GetCurrentUserProfileAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("Ana", result.FirstName);
        Assert.Equal("Santos", result.LastName);
        Assert.Equal("Passenger", result.Role);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_WhenProfileMissing_ReturnsNull()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Repository
            .Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await context.Service.GetCurrentUserProfileAsync(userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenRequestIsValid_UpdatesEditableFields()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        UserProfile? capturedProfile = null;

        context.Repository
            .Setup(repository => repository.UpdateEditableFieldsAsync(
                userId,
                "Ana Maria",
                "Santos",
                "+63 900 000 0000",
                "https://example.test/avatar.png",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string? firstName, string? lastName, string? phoneNumber, string? profileImageUrl, CancellationToken _) =>
            {
                capturedProfile = CreateProfile(id);
                capturedProfile.FirstName = firstName;
                capturedProfile.LastName = lastName;
                capturedProfile.PhoneNumber = phoneNumber;
                capturedProfile.ProfileImageUrl = profileImageUrl;
                return capturedProfile;
            });

        var result = await context.Service.UpdateCurrentUserProfileAsync(
            userId,
            "  Ana Maria  ",
            "  Santos  ",
            "  +63 900 000 0000  ",
            "  https://example.test/avatar.png  ");

        Assert.Equal(UserProfileMutationStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal("Ana Maria", result.Profile?.FirstName);
        Assert.Equal("Santos", result.Profile?.LastName);
        Assert.Equal("+63 900 000 0000", result.Profile?.PhoneNumber);
        Assert.Equal("https://example.test/avatar.png", result.Profile?.ProfileImageUrl);
        Assert.NotNull(capturedProfile);
    }

    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenProfileMissing_ReturnsNotFound()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Repository
            .Setup(repository => repository.UpdateEditableFieldsAsync(
                userId,
                "Ana",
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await context.Service.UpdateCurrentUserProfileAsync(
            userId,
            "Ana",
            null,
            null,
            null);

        Assert.Equal(UserProfileMutationStatus.NotFound, result.Status);
        Assert.Null(result.Profile);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenNoEditableFieldsAreSupplied_ReturnsValidationFailed()
    {
        var context = CreateContext();

        var result = await context.Service.UpdateCurrentUserProfileAsync(
            Guid.NewGuid(),
            null,
            null,
            null,
            null);

        Assert.Equal(UserProfileMutationStatus.ValidationFailed, result.Status);
        context.Repository.Verify(
            repository => repository.UpdateEditableFieldsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateCurrentUserProfileAsync_WhenFieldIsTooLong_ReturnsValidationFailed()
    {
        var context = CreateContext();

        var result = await context.Service.UpdateCurrentUserProfileAsync(
            Guid.NewGuid(),
            new string('A', 101),
            null,
            null,
            null);

        Assert.Equal(UserProfileMutationStatus.ValidationFailed, result.Status);
        Assert.Contains("First name must be 100 characters or fewer.", result.Errors);
        context.Repository.Verify(
            repository => repository.UpdateEditableFieldsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestContext CreateContext()
    {
        var repository = new Mock<IUserProfileRepository>(MockBehavior.Strict);
        return new TestContext(
            new UserProfileService(repository.Object),
            repository);
    }

    private static UserProfile CreateProfile(Guid userId) =>
        new()
        {
            UserId = userId,
            ExternalAuthProvider = "google",
            ExternalAuthId = "google-subject",
            Email = "ana@example.test",
            FirstName = "Ana",
            LastName = "Santos",
            PhoneNumber = "+63 900 000 0000",
            Role = "Passenger",
            ProfileImageUrl = "https://example.test/avatar.png",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        };

    private sealed record TestContext(
        UserProfileService Service,
        Mock<IUserProfileRepository> Repository);
}
