using backend.Models.Database;
using backend.Models.Drivers;
using backend.Services;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/drivers")]
public sealed class DriversController(IDriverService driverService) : ControllerBase
{
    [HttpGet("{driverId:guid}")]
    public async Task<ActionResult<DriverDetailsDto>> GetById(
        [FromRoute] Guid driverId,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var driver = await driverService.GetDriverDetailsAsync(driverId, cancellationToken);
        return driver is null
            ? NotFound(Error($"Driver {driverId} was not found."))
            : Ok(driver);
    }

    [HttpGet("{driverId:guid}/vehicle")]
    public async Task<ActionResult<DriverVehicleDto>> GetVehicle(
        [FromRoute] Guid driverId,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var driver = await driverService.GetDriverDetailsAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound(Error($"Driver {driverId} was not found."));
        }

        var currentVehicle = driver.CurrentAvailabilitySession?.Vehicle ??
            (driver.ActiveVehicles.Count == 1 ? driver.ActiveVehicles[0] : null);
        return currentVehicle is null
            ? NotFound(Error($"Driver {driverId} does not have a current vehicle."))
            : Ok(currentVehicle);
    }

    [HttpGet("{driverId:guid}/availability")]
    public async Task<ActionResult<DriverAvailabilityResponseDto>> GetAvailability(
        [FromRoute] Guid driverId,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var driver = await driverService.GetDriverDetailsAsync(driverId, cancellationToken);
        return driver is null
            ? NotFound(Error($"Driver {driverId} was not found."))
            : Ok(new DriverAvailabilityResponseDto(
                driver.DriverId,
                driver.IsAvailable,
                driver.CurrentAvailabilitySession));
    }

    [HttpPost("{driverId:guid}/availability/start")]
    public async Task<ActionResult<DriverAvailabilitySessionDto>> StartAvailability(
        [FromRoute] Guid driverId,
        [FromBody] StartDriverAvailabilityRequest? request,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var validationErrors = ValidateStartAvailabilityRequest(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new DriverErrorResponseDto(validationErrors));
        }

        var driver = await driverService.GetDriverDetailsAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound(Error($"Driver {driverId} was not found."));
        }

        if (driver.CurrentAvailabilitySession is not null)
        {
            return Conflict(Error($"Driver {driverId} already has an active availability session."));
        }

        var session = await driverService.StartAvailabilitySessionAsync(
            driverId,
            request!.VehicleId,
            request.DestinationStopId,
            request.DestinationName,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.AvailableSeats,
            request.MaximumDetourMeters,
            request.StartedAt,
            cancellationToken);

        return session is null
            ? BadRequest(Error("Availability could not be started with the supplied request."))
            : CreatedAtAction(
                nameof(GetAvailability),
                new { driverId },
                MapAvailabilitySession(session));
    }

    [HttpPost("{driverId:guid}/availability/stop")]
    public async Task<IActionResult> StopAvailability(
        [FromRoute] Guid driverId,
        [FromBody] StopDriverAvailabilityRequest? request,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var driver = await driverService.GetDriverDetailsAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return NotFound(Error($"Driver {driverId} was not found."));
        }

        if (driver.CurrentAvailabilitySession is null)
        {
            return Conflict(Error($"Driver {driverId} does not have an active availability session."));
        }

        var stopped = await driverService.EndAvailabilitySessionAsync(
            driverId,
            request?.EndedAt,
            cancellationToken);

        return stopped
            ? NoContent()
            : Conflict(Error("Availability could not be stopped from the current state."));
    }

    [HttpPut("{driverId:guid}/location")]
    public async Task<ActionResult<DriverLocationDto>> UpdateLocation(
        [FromRoute] Guid driverId,
        [FromBody] UpdateDriverLocationRequest? request,
        CancellationToken cancellationToken)
    {
        if (driverId == Guid.Empty)
        {
            return BadRequest(Error("Driver id is required."));
        }

        var validationErrors = ValidateLocationRequest(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new DriverErrorResponseDto(validationErrors));
        }

        var location = await driverService.UpdateDriverLocationAsync(
            driverId,
            request!.Latitude!.Value,
            request.Longitude!.Value,
            request.HeadingDegrees,
            request.SpeedKph,
            request.AccuracyMeters,
            request.UpdatedAt,
            cancellationToken);

        return location is null
            ? NotFound(Error($"Driver {driverId} was not found."))
            : Ok(MapLocation(location));
    }

    private static List<string> ValidateStartAvailabilityRequest(StartDriverAvailabilityRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (request.VehicleId == Guid.Empty)
        {
            errors.Add("Vehicle id must be a non-empty GUID when supplied.");
        }

        if (request.DestinationStopId <= 0)
        {
            errors.Add("Destination stop id must be greater than zero when supplied.");
        }

        if (request.AvailableSeats <= 0)
        {
            errors.Add("Available seats must be greater than zero.");
        }

        if (request.MaximumDetourMeters < 0)
        {
            errors.Add("Maximum detour meters must be zero or greater.");
        }

        AddOptionalCoordinateErrors(
            errors,
            request.DestinationLatitude,
            request.DestinationLongitude,
            "Destination");

        return errors;
    }

    private static List<string> ValidateLocationRequest(UpdateDriverLocationRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (!request.Latitude.HasValue)
        {
            errors.Add("Latitude is required.");
        }
        else if (!IsLatitude(request.Latitude.Value))
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (!request.Longitude.HasValue)
        {
            errors.Add("Longitude is required.");
        }
        else if (!IsLongitude(request.Longitude.Value))
        {
            errors.Add("Longitude must be between -180 and 180.");
        }

        if (request.HeadingDegrees is < 0 or > 360)
        {
            errors.Add("Heading degrees must be between 0 and 360 when supplied.");
        }

        if (request.SpeedKph < 0)
        {
            errors.Add("Speed must be zero or greater when supplied.");
        }

        if (request.AccuracyMeters < 0)
        {
            errors.Add("Accuracy must be zero or greater when supplied.");
        }

        return errors;
    }

    private static void AddOptionalCoordinateErrors(
        ICollection<string> errors,
        double? latitude,
        double? longitude,
        string label)
    {
        if (!latitude.HasValue && !longitude.HasValue)
        {
            return;
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            errors.Add($"{label} latitude and longitude must be supplied together.");
            return;
        }

        if (!IsLatitude(latitude.Value))
        {
            errors.Add($"{label} latitude must be between -90 and 90.");
        }

        if (!IsLongitude(longitude.Value))
        {
            errors.Add($"{label} longitude must be between -180 and 180.");
        }
    }

    private static DriverLocationDto MapLocation(DriverLocation location) =>
        new(
            location.DriverId,
            location.Latitude,
            location.Longitude,
            location.HeadingDegrees,
            location.SpeedKph,
            location.AccuracyMeters,
            location.UpdatedAt);

    private static DriverAvailabilitySessionDto MapAvailabilitySession(DriverAvailabilitySession session) =>
        new(
            session.SessionId,
            session.DriverId,
            session.VehicleId,
            session.Vehicle is null ? null : MapVehicle(session.Vehicle),
            session.DestinationStopId,
            null,
            session.DestinationName,
            session.DestinationLatitude,
            session.DestinationLongitude,
            session.AvailableSeats,
            session.MaximumDetourMeters,
            session.Status,
            session.StartedAt,
            session.EndedAt);

    private static DriverVehicleDto MapVehicle(DriverVehicle vehicle) =>
        new(
            vehicle.VehicleId,
            vehicle.DriverId,
            vehicle.TransportModeId,
            vehicle.TransportMode is null
                ? null
                : new TransportModeSummaryDto(
                    vehicle.TransportMode.TransportModeId,
                    vehicle.TransportMode.Code,
                    vehicle.TransportMode.Name,
                    vehicle.TransportMode.IsMotorized,
                    vehicle.TransportMode.AllowsLiveDriver,
                    vehicle.TransportMode.IconName),
            vehicle.PlateNumber,
            vehicle.BodyNumber,
            vehicle.Color,
            vehicle.Capacity,
            vehicle.IsActive,
            vehicle.CreatedAt);

    private static bool IsLatitude(double value) =>
        double.IsFinite(value) && value is >= -90 and <= 90;

    private static bool IsLongitude(double value) =>
        double.IsFinite(value) && value is >= -180 and <= 180;

    private static DriverErrorResponseDto Error(string message) => new([message]);
}
