using System.Security.Claims;
using backend.Controllers;
using backend.Models.Database;
using backend.Models.Users;
using backend.Repositories;
using backend.Services;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Facebook;
using backend.Services.Authentication.Google;
using backend.Services.Authentication.Login;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Controllers;

public sealed class GuestAccessControllerTests
{
    [Fact]
    public async Task Guest_WhenRequested_CreatesIsolatedGuestProfileAndRequests24HourKey()
    {
        UserProfile? savedProfile = null;
        var profiles = new Mock<IUserProfileRepository>(MockBehavior.Strict);
        profiles
            .Setup(repository => repository.AddOrUpdateAsync(
                It.IsAny<UserProfile>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((profile, _) => savedProfile = profile)
            .Returns((UserProfile profile, CancellationToken _) => Task.FromResult(profile));

        string? credentialOwner = null;
        TimeSpan? requestedLifetime = null;
        var apiKeys = new Mock<IApiKeyService>(MockBehavior.Strict);
        apiKeys
            .Setup(service => service.Create(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Callback<string, TimeSpan>((owner, lifetime) =>
            {
                credentialOwner = owner;
                requestedLifetime = lifetime;
            })
            .Returns(new IssuedApiKey(
                "GUEST_TEST_KEY",
                DateTimeOffset.UtcNow.AddHours(24)));

        var controller = CreateAuthController(
            apiKeys.Object,
            new UserProfileService(profiles.Object));

        var response = await controller.Guest(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<LoginResponse>(ok.Value);
        var profile = Assert.IsType<UserProfile>(savedProfile);
        Assert.Equal("GUEST_TEST_KEY", body.ApiKey);
        Assert.Equal("Guest", profile.Role);
        Assert.Equal("Guest", profile.FirstName);
        Assert.Equal("guest", profile.ExternalAuthProvider);
        Assert.False(profile.IsEmailVerified);
        Assert.True(profile.IsActive);
        Assert.True(profile.Email.StartsWith("guest:", StringComparison.Ordinal));
        Assert.Equal(profile.Email, credentialOwner);
        Assert.Equal(TimeSpan.FromHours(24), requestedLifetime);
    }

    [Fact]
    public async Task UpdateCurrent_WhenGuestChangesProfileFields_ReturnsForbiddenWithoutMutation()
    {
        var userId = Guid.NewGuid();
        var profiles = new Mock<IUserProfileService>(MockBehavior.Strict);
        var controller = new UsersController(profiles.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                            new Claim(ClaimTypes.Role, "Guest")
                        ],
                        "ApiKey"))
                }
            }
        };

        var response = await controller.UpdateCurrent(
            new UpdateUserProfileRequest(FirstName: "Changed"),
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        profiles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateGuestProfileAsync_WhenCalledRepeatedly_CreatesUniqueGuestIdentities()
    {
        var savedProfiles = new List<UserProfile>();
        var profiles = new Mock<IUserProfileRepository>(MockBehavior.Strict);
        profiles
            .Setup(repository => repository.AddOrUpdateAsync(
                It.IsAny<UserProfile>(),
                It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((profile, _) => savedProfiles.Add(profile))
            .Returns((UserProfile profile, CancellationToken _) => Task.FromResult(profile));

        var service = new UserProfileService(profiles.Object);

        var first = await service.CreateGuestProfileAsync(CancellationToken.None);
        var second = await service.CreateGuestProfileAsync(CancellationToken.None);

        Assert.NotEqual(first.UserId, second.UserId);
        Assert.NotEqual(first.CredentialOwner, second.CredentialOwner);
        Assert.All(savedProfiles, profile =>
        {
            Assert.Equal("Guest", profile.Role);
            Assert.Equal("guest", profile.ExternalAuthProvider);
            Assert.StartsWith("guest:", profile.Email);
        });
    }

    [Fact]
    public void ApiKeyService_CustomLifetime_DoesNotChangeConfiguredNormalLifetime()
    {
        var service = new InMemoryApiKeyService(Options.Create(new LoginOptions
        {
            ApiKeyLifetimeHours = 8
        }));

        var before = DateTimeOffset.UtcNow;
        var normal = service.Create("normal-user");
        var guest = service.Create("guest:test", TimeSpan.FromHours(24));
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(normal.ExpiresAt, before.AddHours(8), after.AddHours(8));
        Assert.InRange(guest.ExpiresAt, before.AddHours(24), after.AddHours(24));
    }

    private static AuthController CreateAuthController(
        IApiKeyService apiKeys,
        IUserProfileService profiles) =>
        new(
            apiKeys,
            profiles,
            Options.Create(new LoginOptions()),
            Options.Create(new GoogleOptions()),
            Options.Create(new FacebookOptions()),
            null!,
            null!,
            null!,
            null!,
            null!);
}
