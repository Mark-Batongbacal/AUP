using backend.Services.Transportation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/transport-routes")]
public sealed class TransportRoutesController(
    ITransportRouteService transportRouteService,
    IRoutePointService routePointService,
    IRouteGeneratorService routeGeneratorService) : ControllerBase
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

    [HttpGet("latest/polyline")]
    public async Task<ActionResult<TransportRoutePolylineDto>> GetLatestPolyline(
        CancellationToken cancellationToken)
    {
        var route = await transportRouteService.GetLatestRouteWithPolylineAsync(cancellationToken);
        if (route is null)
        {
            return NotFound(new RoutePointErrorResponseDto(["No route with a polyline was found."]));
        }

        return Ok(new TransportRoutePolylineDto(
            route.RouteId,
            route.RouteCode,
            route.RouteName,
            6,
            route.EncodedPolyline!));
    }

    [HttpPost]
    public async Task<ActionResult<CreatedTransportRouteDto>> CreateRoute(
        [FromBody] CreateJeepneyRouteCommand command,
        CancellationToken cancellationToken)
    {
        TransportRouteCreationResult result;
        try
        {
            var generatedPoints = command.Points is { Count: >= 2 }
                ? await routeGeneratorService.GenerateAsync(command.Points, cancellationToken)
                : command.Points;

            result = await transportRouteService.CreateJeepneyRouteAsync(
                command with
                {
                    Points = generatedPoints?.ToList(),
                    Waypoints = command.Points
                },
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new RoutePointErrorResponseDto(
                [$"Valhalla could not generate the route: {exception.Message}"]));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new RoutePointErrorResponseDto(
                [exception.Message]));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new RoutePointErrorResponseDto([exception.Message]));
        }

        if (result.Status == TransportRouteCreationStatus.Success)
        {
            var route = result.Route!;
            var response = new CreatedTransportRouteDto(
                route.RouteId,
                route.RouteCode,
                route.RouteName,
                route.OriginName,
                route.DestinationName,
                route.EncodedPolyline,
                route.RoutePoints.OrderBy(point => point.PointOrder)
                    .Select(point => new RoutePointResponseDto(
                        point.RoutePointId,
                        point.PointOrder,
                        point.Latitude,
                        point.Longitude))
                    .ToList());

            return CreatedAtAction(
                nameof(GetRoutePoints),
                new { routeId = route.RouteId },
                response);
        }

        var error = new RoutePointErrorResponseDto(result.Errors);
        return result.Status switch
        {
            TransportRouteCreationStatus.DuplicateRouteCode => Conflict(error),
            TransportRouteCreationStatus.JeepneyModeNotFound =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, error),
            _ => BadRequest(error)
        };
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

public sealed record TransportRoutePolylineDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    int Precision,
    string Polyline);

public sealed record CreatedTransportRouteDto(
    long RouteId,
    string RouteCode,
    string RouteName,
    string OriginName,
    string DestinationName,
    string? EncodedPolyline,
    IReadOnlyList<RoutePointResponseDto> Points);

public sealed record RoutePointsResponseDto(
    long RouteId,
    IReadOnlyList<RoutePointResponseDto> Points);

public sealed record RoutePointResponseDto(
    long RoutePointId,
    int PointOrder,
    double Latitude,
    double Longitude);

public sealed record RoutePointErrorResponseDto(IReadOnlyList<string> Errors);
