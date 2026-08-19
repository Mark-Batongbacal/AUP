using System.Security.Claims;
using backend.Models.Trips;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/favorite-trips")]
public sealed class FavoriteTripsController(IFavoriteTripService favoriteTripService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FavoriteTripDto>>> GetFavorites(CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var favorites = await favoriteTripService.GetFavoritesByUserAsync(userId, cancellationToken);
        return Ok(favorites);
    }

    [HttpGet("{favoriteTripId:guid}")]
    public async Task<ActionResult<FavoriteTripDto>> GetById(
        [FromRoute] Guid favoriteTripId,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var favorite = await favoriteTripService.GetFavoriteByIdAsync(userId, favoriteTripId, cancellationToken);
        return favorite is null
            ? NotFound(Error($"Favorite trip {favoriteTripId} was not found."))
            : Ok(favorite);
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteTripDto>> AddFavorite(
        [FromBody] AddFavoriteTripRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request is null || !request.RecommendationId.HasValue || request.RecommendationId.Value == Guid.Empty)
        {
            return BadRequest(Error("Recommendation id is required."));
        }

        var result = await favoriteTripService.AddFavoriteAsync(
            userId,
            request.RecommendationId.Value,
            request.Note,
            cancellationToken);

        return result.Status switch
        {
            FavoriteTripAddStatus.RecommendationNotFound =>
                NotFound(Error($"Route recommendation {request.RecommendationId.Value} was not found.")),
            FavoriteTripAddStatus.AlreadyFavorited => Ok(result.Favorite),
            _ => CreatedAtAction(nameof(GetById), new { favoriteTripId = result.Favorite!.FavoriteTripId }, result.Favorite),
        };
    }

    [HttpDelete("{favoriteTripId:guid}")]
    public async Task<IActionResult> RemoveFavorite(
        [FromRoute] Guid favoriteTripId,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var removed = await favoriteTripService.RemoveFavoriteAsync(userId, favoriteTripId, cancellationToken);
        return removed
            ? NoContent()
            : NotFound(Error($"Favorite trip {favoriteTripId} was not found."));
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static TripErrorResponseDto Error(string message) => new([message]);
}
