using System.Security.Claims;
using backend.Authentication;
using backend.Models.Users;
using backend.Repositories;
using backend.Services;
using backend.Services.Authentication.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(
    IUserProfileService userProfileService,
    IUserProfileRepository? userProfileRepository = null,
    ITripSessionRepository? tripSessionRepository = null,
    IFavoriteTripRepository? favoriteTripRepository = null,
    IWebHostEnvironment? hostingEnvironment = null,
    IConfiguration? configuration = null,
    ILocalAuthenticationService? localAuthenticationService = null) : ControllerBase
{
    private const string GuestRole = "Guest";
    private const long MaxProfileImageBytes = 5 * 1024 * 1024;

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<UserProfileResponse>> GetCurrent(
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var profile = await userProfileService.GetCurrentUserProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return NotFound(Error($"User profile {userId} was not found."));
        }

        var lastPasswordChangedAt = localAuthenticationService is null
            ? null
            : await localAuthenticationService.GetCredentialUpdatedAtAsync(userId, cancellationToken);
        var profileWithSecurityMetadata = profile with
        {
            LastPasswordChangedAt = lastPasswordChangedAt is { } timestamp
                ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
                : null,
        };

        // Direct controller unit tests construct this controller with only the profile service.
        // Normal application DI supplies all repositories/services below.
        if (userProfileRepository is null ||
            tripSessionRepository is null ||
            favoriteTripRepository is null)
        {
            return Ok(profileWithSecurityMetadata);
        }

        var storedProfile = await userProfileRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (storedProfile is null)
        {
            return NotFound(Error($"User profile {userId} was not found."));
        }

        var tripsTaken = await tripSessionRepository.CountCompletedByUserAsync(userId, cancellationToken);
        var favorites = await favoriteTripRepository.GetByUserAsync(userId, cancellationToken);

        return Ok(profileWithSecurityMetadata with
        {
            Email = storedProfile.Email,
            TripsTaken = tripsTaken,
            FavoritesCount = favorites.Count,
        });
    }

    [HttpPut("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<UserProfileResponse>> UpdateCurrent(
        [FromBody] UpdateUserProfileRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(Error("Request body is required."));
        }

        var hasEditableProfileFields = request.FirstName is not null ||
            request.LastName is not null ||
            request.PhoneNumber is not null ||
            request.ProfileImageUrl is not null;

        // The authentication handler already resolved the profile and attached its role. This
        // avoids adding any extra database/service lookup to the normal registered-user path.
        if (hasEditableProfileFields && User.IsInRole(GuestRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Error("Guest profiles cannot be edited. Create an account for permanent profile settings."));
        }

        UserProfileMutationResult result;
        if (hasEditableProfileFields)
        {
            result = await userProfileService.UpdateCurrentUserProfileAsync(
                userId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.ProfileImageUrl,
                cancellationToken);

            if (result.Status == UserProfileMutationStatus.Success &&
                request.PreferredLanguage is not null)
            {
                result = await userProfileService.UpdatePreferredLanguageAsync(
                    userId,
                    request.PreferredLanguage,
                    cancellationToken);
            }
        }
        else if (request.PreferredLanguage is not null)
        {
            result = await userProfileService.UpdatePreferredLanguageAsync(
                userId,
                request.PreferredLanguage,
                cancellationToken);
        }
        else
        {
            result = UserProfileMutationResult.ValidationFailed(
                ["At least one editable profile field is required."]);
        }

        var error = new UserProfileErrorResponse(result.Errors);
        return result.Status switch
        {
            UserProfileMutationStatus.Success => Ok(result.Profile),
            UserProfileMutationStatus.NotFound => NotFound(error),
            _ => BadRequest(error),
        };
    }

    [HttpPost("me/profile-image")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    [RequestSizeLimit(MaxProfileImageBytes + 256 * 1024)]
    public async Task<ActionResult<UserProfileResponse>> UploadProfileImage(
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (User.IsInRole(GuestRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Error("Guest profiles cannot have a profile picture. Create an account to customize your profile."));
        }

        if (image is null || image.Length <= 0)
        {
            return BadRequest(Error("Choose an image to upload."));
        }

        if (image.Length > MaxProfileImageBytes)
        {
            return BadRequest(Error("Profile pictures must be 5 MB or smaller."));
        }

        await using var buffer = new MemoryStream((int)image.Length);
        await image.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var imageType = DetectImageType(bytes);
        if (imageType is null)
        {
            return BadRequest(Error("Profile pictures must be JPEG, PNG, or WebP images."));
        }

        if (hostingEnvironment is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                Error("Profile picture storage is unavailable in this environment."));
        }

        var storageRoot = configuration?["ProfileImages:StoragePath"]?.Trim();
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            storageRoot = Path.Combine(hostingEnvironment.ContentRootPath, "profile-images");
        }

        Directory.CreateDirectory(storageRoot);
        var fileName = $"{userId:N}-{Guid.NewGuid():N}.{imageType.Value.Extension}";
        var filePath = Path.Combine(storageRoot, fileName);
        await System.IO.File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

        var publicUrl = $"{Request.Scheme}://{Request.Host}/api/users/profile-images/{fileName}";
        var result = await userProfileService.UpdateCurrentUserProfileAsync(
            userId,
            null,
            null,
            null,
            publicUrl,
            cancellationToken);

        if (result.Status != UserProfileMutationStatus.Success)
        {
            System.IO.File.Delete(filePath);
            var error = new UserProfileErrorResponse(result.Errors);
            return result.Status == UserProfileMutationStatus.NotFound
                ? NotFound(error)
                : BadRequest(error);
        }

        return Ok(result.Profile);
    }

    [HttpGet("profile-images/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetProfileImage(string fileName)
    {
        if (hostingEnvironment is null ||
            string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return NotFound();
        }

        var imageType = ImageTypeFromFileName(fileName);
        if (imageType is null)
        {
            return NotFound();
        }

        var storageRoot = configuration?["ProfileImages:StoragePath"]?.Trim();
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            storageRoot = Path.Combine(hostingEnvironment.ContentRootPath, "profile-images");
        }

        var filePath = Path.Combine(storageRoot, fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        return PhysicalFile(filePath, imageType.Value.ContentType, enableRangeProcessing: false);
    }

    [HttpDelete("me")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> DeleteCurrent(CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (User.IsInRole(GuestRole))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                Error("Guest access expires automatically and does not have a permanent account to delete."));
        }

        if (userProfileRepository is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                Error("Account deletion is unavailable in this environment."));
        }

        var deactivated = await userProfileRepository.DeactivateAsync(userId, cancellationToken);
        return deactivated
            ? NoContent()
            : NotFound(Error($"User profile {userId} was not found."));
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static (string Extension, string ContentType)? DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ("jpg", "image/jpeg");
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return ("png", "image/png");
        }

        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return ("webp", "image/webp");
        }

        return null;
    }

    private static (string Extension, string ContentType)? ImageTypeFromFileName(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ("jpg", "image/jpeg"),
            ".png" => ("png", "image/png"),
            ".webp" => ("webp", "image/webp"),
            _ => null,
        };

    private static UserProfileErrorResponse Error(string message) => new([message]);
}
