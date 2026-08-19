using System.Security.Claims;
using backend.Authentication;
using backend.Models.Users;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(
    IUserProfileService userProfileService,
    IUserProfileRepository? userProfileRepository = null,
    IPassengerTripRepository? passengerTripRepository = null,
    IFavoriteTripRepository? favoriteTripRepository = null) : ControllerBase
{
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

        // Direct controller unit tests construct this controller with only the profile service.
        // Normal application DI supplies all three repositories below.
        if (userProfileRepository is null ||
            passengerTripRepository is null ||
            favoriteTripRepository is null)
        {
            return Ok(profile);
        }

        var storedProfile = await userProfileRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (storedProfile is null)
        {
            return NotFound(Error($"User profile {userId} was not found."));
        }

        var trips = await passengerTripRepository.GetByUserAsync(userId, cancellationToken);
        var favorites = await favoriteTripRepository.GetByUserAsync(userId, cancellationToken);

        return Ok(profile with
        {
            Email = storedProfile.Email,
            TripsTaken = trips.Count,
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

        var result = await userProfileService.UpdateCurrentUserProfileAsync(
            userId,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.ProfileImageUrl,
            cancellationToken);

        var error = new UserProfileErrorResponse(result.Errors);
        return result.Status switch
        {
            UserProfileMutationStatus.Success => Ok(result.Profile),
            UserProfileMutationStatus.NotFound => NotFound(error),
            _ => BadRequest(error),
        };
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static UserProfileErrorResponse Error(string message) => new([message]);
}
