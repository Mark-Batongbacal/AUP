using backend.Models.Database;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/tricycle-points")]
public sealed class TricyclePointsController(
    ITricyclePointService tricyclePointService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TricyclePointResponseDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        var points = await tricyclePointService.GetAllActivePointsAsync(cancellationToken);
        return Ok(points.Select(Map).ToList());
    }

    [HttpGet("{tricyclePointId:long}")]
    public async Task<ActionResult<TricyclePointResponseDto>> GetById(
        [FromRoute] long tricyclePointId,
        CancellationToken cancellationToken)
    {
        var point = await tricyclePointService.GetPointByIdAsync(
            tricyclePointId,
            cancellationToken);

        return point is null
            ? NotFound(new TricyclePointErrorResponseDto(
                [$"Tricycle point {tricyclePointId} was not found."]))
            : Ok(Map(point));
    }

    [HttpPost]
    public async Task<ActionResult<TricyclePointResponseDto>> Create(
        [FromBody] CreateTricyclePointRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Coordinates is not { Count: 2 })
        {
            return BadRequest(new TricyclePointErrorResponseDto(
                ["Coordinates must contain exactly two values: [latitude, longitude]."]));
        }

        var result = await tricyclePointService.AddVerifiedTricyclePointAsync(
            request.PointCode,
            request.PointName,
            request.Coordinates[0],
            request.Coordinates[1],
            request.RadiusMeters,
            request.StopId,
            request.Description,
            request.Address,
            request.OperatorName,
            request.BaseFare,
            request.FarePerKilometer,
            request.AverageWaitingTimeSeconds,
            request.ServiceStartTime,
            request.ServiceEndTime,
            request.IsActive,
            cancellationToken);

        if (result.Status == TricyclePointMutationStatus.Success)
        {
            var point = result.TricyclePoint!;
            return CreatedAtAction(
                nameof(GetById),
                new { tricyclePointId = point.TricyclePointId },
                Map(point));
        }

        var error = new TricyclePointErrorResponseDto(result.Errors);
        return result.Status switch
        {
            TricyclePointMutationStatus.Duplicate => Conflict(error),
            _ => BadRequest(error),
        };
    }

    private static TricyclePointResponseDto Map(TricyclePoint point) => new(
        point.TricyclePointId,
        point.StopId,
        point.PointCode,
        point.PointName,
        point.Description,
        point.Address,
        point.OperatorName,
        point.CenterLatitude,
        point.CenterLongitude,
        point.RadiusMeters,
        point.BaseFare,
        point.FarePerKilometer,
        point.AverageWaitingTimeSeconds,
        point.ServiceStartTime,
        point.ServiceEndTime,
        point.IsActive);
}

public sealed record CreateTricyclePointRequestDto(
    string PointCode,
    string PointName,
    IReadOnlyList<double>? Coordinates,
    int RadiusMeters,
    long? StopId = null,
    string? Description = null,
    string? Address = null,
    string? OperatorName = null,
    decimal? BaseFare = null,
    decimal? FarePerKilometer = null,
    int? AverageWaitingTimeSeconds = null,
    TimeOnly? ServiceStartTime = null,
    TimeOnly? ServiceEndTime = null,
    bool IsActive = true);

public sealed record TricyclePointResponseDto(
    long TricyclePointId,
    long? StopId,
    string PointCode,
    string PointName,
    string? Description,
    string? Address,
    string? OperatorName,
    double CenterLatitude,
    double CenterLongitude,
    int RadiusMeters,
    decimal? BaseFare,
    decimal? FarePerKilometer,
    int? AverageWaitingTimeSeconds,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    bool IsActive);

public sealed record TricyclePointErrorResponseDto(IReadOnlyList<string> Errors);
