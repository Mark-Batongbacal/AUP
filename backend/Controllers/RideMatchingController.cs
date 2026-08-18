using System.Security.Claims;
using backend.Models.Database;
using backend.Models.RideMatching;
using backend.Services;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/ride-matching")]
public sealed class RideMatchingController(IRideMatchingService rideMatchingService) : ControllerBase
{
    private const string SearchingRequestStatus = "SEARCHING";
    private const string OfferedMatchStatus = "OFFERED";

    [HttpPost("requests")]
    public async Task<ActionResult<RideRequestDetailsDto>> CreateRideRequest(
        [FromBody] CreateRideRequestRequest? request,
        CancellationToken cancellationToken)
    {
        var passengerUserId = UserId();
        if (passengerUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var validationErrors = ValidateRideRequest(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new RideMatchingErrorResponseDto(validationErrors));
        }

        var created = await rideMatchingService.CreateRideRequestAsync(
            passengerUserId,
            request!.PickupName!,
            request.PickupLatitude!.Value,
            request.PickupLongitude!.Value,
            request.DropoffName!,
            request.DropoffLatitude!.Value,
            request.DropoffLongitude!.Value,
            request.PassengerCount,
            request.TransportModeId,
            request.MaxBudget,
            request.RequestedAt,
            request.ExpiresAt,
            cancellationToken);

        return created is null
            ? BadRequest(Error("Ride request could not be created with the supplied request."))
            : CreatedAtAction(
                nameof(GetRideRequest),
                new { requestId = created.RequestId },
                MapRideRequest(created));
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<ActionResult<RideRequestDetailsDto>> GetRideRequest(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            return BadRequest(Error("Ride request id is required."));
        }

        var request = await rideMatchingService.GetRideRequestByIdAsync(requestId, cancellationToken);
        return request is null
            ? NotFound(Error($"Ride request {requestId} was not found."))
            : Ok(MapRideRequest(request));
    }

    [HttpPost("requests/{requestId:guid}/match")]
    public async Task<ActionResult<RideMatchDetailsDto>> CreateRideMatch(
        [FromRoute] Guid requestId,
        [FromBody] CreateRideMatchRequest? request,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            return BadRequest(Error("Ride request id is required."));
        }

        var validationErrors = ValidateRideMatch(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new RideMatchingErrorResponseDto(validationErrors));
        }

        var rideRequest = await rideMatchingService.GetRideRequestByIdAsync(requestId, cancellationToken);
        if (rideRequest is null)
        {
            return NotFound(Error($"Ride request {requestId} was not found."));
        }

        if (!IsActiveSearchRequest(rideRequest))
        {
            return Conflict(Error($"Ride request {requestId} is not searching for a match."));
        }

        var match = await rideMatchingService.CreateRideMatchAsync(
            requestId,
            request!.DriverId!.Value,
            request.VehicleId,
            request.PickupDistanceMeters,
            request.DetourDistanceMeters,
            request.EstimatedPickupMinutes,
            request.EstimatedTripMinutes,
            request.EstimatedFare,
            request.MatchScore,
            request.OfferedAt,
            cancellationToken);

        return match is null
            ? Conflict(Error("The ride request could not be matched with the selected driver."))
            : CreatedAtAction(
                nameof(GetMatch),
                new { matchId = match.MatchId },
                MapRideMatch(match));
    }

    [HttpGet("matches/{matchId:guid}")]
    public async Task<ActionResult<RideMatchDetailsDto>> GetMatch(
        [FromRoute] Guid matchId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return BadRequest(Error("Ride match id is required."));
        }

        var match = await rideMatchingService.GetMatchDetailsAsync(matchId, cancellationToken);
        return match is null
            ? NotFound(Error($"Ride match {matchId} was not found."))
            : Ok(match);
    }

    [HttpPost("matches/{matchId:guid}/accept")]
    public async Task<IActionResult> AcceptMatch(
        [FromRoute] Guid matchId,
        [FromBody] AcceptRideMatchRequest? request,
        CancellationToken cancellationToken)
    {
        var match = await GetExistingMatchForAction(matchId, cancellationToken);
        if (match.Result is not null)
        {
            return match.Result;
        }

        if (!IsStatus(match.Value!.Status, OfferedMatchStatus))
        {
            return Conflict(Error($"Ride match {matchId} is not available to accept."));
        }

        var accepted = await rideMatchingService.AcceptMatchAsync(
            matchId,
            request?.AcceptedAt,
            cancellationToken);

        return accepted
            ? NoContent()
            : Conflict(Error("Ride match could not be accepted from the current state."));
    }

    [HttpPost("matches/{matchId:guid}/reject")]
    public async Task<IActionResult> RejectMatch(
        [FromRoute] Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await GetExistingMatchForAction(matchId, cancellationToken);
        if (match.Result is not null)
        {
            return match.Result;
        }

        if (!IsStatus(match.Value!.Status, OfferedMatchStatus))
        {
            return Conflict(Error($"Ride match {matchId} is not available to reject."));
        }

        var rejected = await rideMatchingService.RejectMatchAsync(matchId, cancellationToken);
        return rejected
            ? NoContent()
            : Conflict(Error("Ride match could not be rejected from the current state."));
    }

    [HttpPost("matches/{matchId:guid}/cancel")]
    public async Task<IActionResult> CancelMatch(
        [FromRoute] Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await GetExistingMatchForAction(matchId, cancellationToken);
        if (match.Result is not null)
        {
            return match.Result;
        }

        var cancelled = await rideMatchingService.CancelMatchAsync(matchId, cancellationToken);
        return cancelled
            ? NoContent()
            : Conflict(Error("Ride match could not be cancelled from the current state."));
    }

    private async Task<ActionResult<RideMatchDetailsDto>> GetExistingMatchForAction(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return BadRequest(Error("Ride match id is required."));
        }

        var match = await rideMatchingService.GetMatchDetailsAsync(matchId, cancellationToken);
        return match is null
            ? NotFound(Error($"Ride match {matchId} was not found."))
            : match;
    }

    private static List<string> ValidateRideRequest(CreateRideRequestRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        AddRequiredTextError(errors, request.PickupName, "Pickup name");
        AddRequiredTextError(errors, request.DropoffName, "Dropoff name");
        AddRequiredLatitudeError(errors, request.PickupLatitude, "Pickup latitude");
        AddRequiredLongitudeError(errors, request.PickupLongitude, "Pickup longitude");
        AddRequiredLatitudeError(errors, request.DropoffLatitude, "Dropoff latitude");
        AddRequiredLongitudeError(errors, request.DropoffLongitude, "Dropoff longitude");

        if (request.PassengerCount <= 0)
        {
            errors.Add("Passenger count must be greater than zero.");
        }

        if (request.TransportModeId <= 0)
        {
            errors.Add("Transport mode id must be greater than zero when supplied.");
        }

        if (request.MaxBudget < 0)
        {
            errors.Add("Maximum budget must be zero or greater when supplied.");
        }

        if (request.ExpiresAt.HasValue &&
            request.ExpiresAt.Value <= (request.RequestedAt ?? DateTime.UtcNow))
        {
            errors.Add("Expiration must be after the requested time.");
        }

        return errors;
    }

    private static List<string> ValidateRideMatch(CreateRideMatchRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
        {
            errors.Add("Driver id is required.");
        }

        if (request.VehicleId == Guid.Empty)
        {
            errors.Add("Vehicle id must be a non-empty GUID when supplied.");
        }

        AddNonNegativeDecimalError(errors, request.PickupDistanceMeters, "Pickup distance meters");
        AddNonNegativeDecimalError(errors, request.DetourDistanceMeters, "Detour distance meters");
        AddNonNegativeDecimalError(errors, request.EstimatedPickupMinutes, "Estimated pickup minutes");
        AddNonNegativeDecimalError(errors, request.EstimatedTripMinutes, "Estimated trip minutes");
        AddNonNegativeDecimalError(errors, request.EstimatedFare, "Estimated fare");
        AddNonNegativeDecimalError(errors, request.MatchScore, "Match score");

        return errors;
    }

    private static RideRequestDetailsDto MapRideRequest(PassengerRideRequest request) =>
        new(
            request.RequestId,
            request.PassengerUserId,
            request.TransportModeId,
            request.TransportMode is null
                ? null
                : new TransportModeSummaryDto(
                    request.TransportMode.TransportModeId,
                    request.TransportMode.Code,
                    request.TransportMode.Name,
                    request.TransportMode.IsMotorized,
                    request.TransportMode.AllowsLiveDriver,
                    request.TransportMode.IconName),
            request.PickupName,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropoffName,
            request.DropoffLatitude,
            request.DropoffLongitude,
            request.PassengerCount,
            request.MaxBudget,
            request.Status,
            request.RequestedAt,
            request.ExpiresAt,
            request.UpdatedAt);

    private static RideMatchDetailsDto MapRideMatch(RideMatch match) =>
        new(
            match.MatchId,
            match.RequestId,
            match.DriverId,
            match.SessionId,
            match.VehicleId,
            match.PickupDistanceMeters,
            match.DetourDistanceMeters,
            match.EstimatedPickupMinutes,
            match.EstimatedTripMinutes,
            match.EstimatedFare,
            match.MatchScore,
            match.Status,
            match.OfferedAt,
            match.AcceptedAt,
            match.CompletedAt,
            match.Request is null ? null : MapRideRequest(match.Request),
            match.Driver is null
                ? null
                : new DriverSummaryDto(
                    match.Driver.DriverId,
                    match.Driver.UserId,
                    match.Driver.LicenseNumber,
                    match.Driver.VerificationStatus,
                    match.Driver.HomeTerminalId,
                    match.Driver.AverageRating,
                    match.Driver.RatingCount,
                    match.Driver.IsAvailable,
                    match.Driver.CreatedAt,
                    match.Driver.UpdatedAt),
            null,
            null);

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static bool IsActiveSearchRequest(PassengerRideRequest request) =>
        IsStatus(request.Status, SearchingRequestStatus) &&
        (!request.ExpiresAt.HasValue || request.ExpiresAt.Value > DateTime.UtcNow);

    private static bool IsStatus(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static void AddRequiredTextError(ICollection<string> errors, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }
    }

    private static void AddRequiredLatitudeError(ICollection<string> errors, double? value, string label)
    {
        if (!value.HasValue)
        {
            errors.Add($"{label} is required.");
        }
        else if (!double.IsFinite(value.Value) || value.Value is < -90 or > 90)
        {
            errors.Add($"{label} must be between -90 and 90.");
        }
    }

    private static void AddRequiredLongitudeError(ICollection<string> errors, double? value, string label)
    {
        if (!value.HasValue)
        {
            errors.Add($"{label} is required.");
        }
        else if (!double.IsFinite(value.Value) || value.Value is < -180 or > 180)
        {
            errors.Add($"{label} must be between -180 and 180.");
        }
    }

    private static void AddNonNegativeDecimalError(ICollection<string> errors, decimal? value, string label)
    {
        if (value < 0)
        {
            errors.Add($"{label} must be zero or greater when supplied.");
        }
    }

    private static RideMatchingErrorResponseDto Error(string message) => new([message]);
}
