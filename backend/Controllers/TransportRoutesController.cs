using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/transport-routes")]
public sealed class TransportRoutesController(
    ITransportRouteService transportRouteService,
    IRoutePointService routePointService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransportRouteListItemDto>>> GetActiveRoutes(
        CancellationToken cancellationToken)
    {
        var routes = await transportRouteService.GetAllActiveRoutesAsync(cancellationToken);
        var result = routes
            .Select(route => new TransportRouteListItemDto(
                route.RouteId,
                route.RouteCode,
                route.RouteName,
                route.IsActive))
            .ToList();

        return Ok(result);
    }

    [HttpGet("{routeId:long}/points")]
    public async Task<ActionResult<RoutePointsResponseDto>> GetRoutePoints(
        [FromRoute] long routeId,
        CancellationToken cancellationToken)
    {
        if (routeId <= 0)
        {
            return BadRequest(new RoutePointErrorResponseDto(["Route id must be greater than zero."]));
        }

        var routePoints = await routePointService.GetRoutePointsAsync(routeId, cancellationToken);
        return Ok(new RoutePointsResponseDto(routeId, routePoints.Select(MapRoutePoint).ToList()));
    }

    [HttpPut("{routeId:long}/points")]
    public async Task<ActionResult<RoutePointsResponseDto>> ReplaceRoutePoints(
        [FromRoute] long routeId,
        [FromBody] List<List<double>>? routePoints,
        CancellationToken cancellationToken)
    {
        var result = await routePointService.ReplaceRoutePointsAsync(routeId, routePoints!, cancellationToken);

        return result.Status switch
        {
            RoutePointReplacementStatus.Success => Ok(new RoutePointsResponseDto(
                routeId,
                result.RoutePoints.Select(MapRoutePoint).ToList())),
            RoutePointReplacementStatus.RouteNotFound => NotFound(new RoutePointErrorResponseDto(result.Errors)),
            _ => BadRequest(new RoutePointErrorResponseDto(result.Errors)),
        };
    }

    private static RoutePointResponseDto MapRoutePoint(RoutePointDetailsDto routePoint) =>
        new(
            routePoint.RoutePointId,
            routePoint.PointOrder,
            routePoint.Latitude,
            routePoint.Longitude);
}

public sealed record TransportRouteListItemDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    bool IsActive);

public sealed record RoutePointsResponseDto(
    long RouteId,
    IReadOnlyList<RoutePointResponseDto> Points);

public sealed record RoutePointResponseDto(
    long RoutePointId,
    int PointOrder,
    double Latitude,
    double Longitude);

public sealed record RoutePointErrorResponseDto(IReadOnlyList<string> Errors);
