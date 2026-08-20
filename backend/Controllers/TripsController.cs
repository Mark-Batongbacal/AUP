using System.Security.Claims;
using backend.Models.Database;
using backend.Models.Trips;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(ITripService tripService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PassengerTripHistoryItemDto>>> GetHistory(
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        return Ok(await tripService.GetPassengerTripHistoryAsync(
            userId,
            recentOnly: false,
            cancellationToken));
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<PassengerTripHistoryItemDto>>> GetRecent(
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        return Ok(await tripService.GetPassengerTripHistoryAsync(
            userId,
            recentOnly: true,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PassengerTripDetailsDto>> StartTrip(
        [FromBody] StartTripRequest? request,
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

        if (!request.RecommendationId.HasValue || request.RecommendationId.Value == Guid.Empty)
        {
            return BadRequest(Error("Recommendation id is required."));
        }

        var recommendation = await tripService.GetRecommendationByIdAsync(
            request.RecommendationId.Value,
            cancellationToken);
        if (recommendation is null)
        {
            return NotFound(Error($"Route recommendation {request.RecommendationId.Value} was not found."));
        }

        var search = await tripService.GetTripSearchByIdAsync(
            recommendation.TripSearchId,
            cancellationToken);
        if (search?.UserId != userId)
        {
            return NotFound(Error($"Route recommendation {request.RecommendationId.Value} was not found."));
        }

        var trip = await tripService.StartPassengerTripAsync(
            userId,
            request.RecommendationId.Value,
            request.StartedAt,
            cancellationToken);
        if (trip is null)
        {
            return BadRequest(Error("Trip could not be started with the supplied request."));
        }

        var details = await tripService.GetPassengerTripDetailsAsync(
            trip.PassengerTripId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { tripId = trip.PassengerTripId },
            details ?? MapPassengerTrip(trip));
    }

    [HttpGet("{tripId:guid}")]
    public async Task<ActionResult<PassengerTripDetailsDto>> GetById(
        [FromRoute] Guid tripId,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (tripId == Guid.Empty)
        {
            return BadRequest(Error("Trip id is required."));
        }

        var trip = await tripService.GetPassengerTripDetailsAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return NotFound(Error($"Trip {tripId} was not found."));
        }

        return trip.UserId == userId
            ? Ok(trip)
            : Forbid();
    }

    [HttpGet("{tripId:guid}/alerts")]
    public async Task<ActionResult<IReadOnlyList<TripAlertDto>>> GetAlerts(
        [FromRoute] Guid tripId,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (tripId == Guid.Empty)
        {
            return BadRequest(Error("Trip id is required."));
        }

        var trip = await tripService.GetPassengerTripDetailsAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return NotFound(Error($"Trip {tripId} was not found."));
        }

        return trip.UserId == userId
            ? Ok(trip.Alerts)
            : Forbid();
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static PassengerTripDetailsDto MapPassengerTrip(PassengerTrip trip) =>
        new(
            trip.PassengerTripId,
            trip.UserId,
            trip.RecommendationId,
            trip.CurrentLegOrder,
            trip.Status,
            trip.StartedAt,
            trip.CompletedAt,
            trip.CreatedAt,
            trip.UpdatedAt,
            null,
            []);

    private static TripErrorResponseDto Error(string message) => new([message]);
}
