using System.Security.Claims;
using backend.Services.Navigation;
using backend.Services.Routing;
using backend.Services.Transportation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/navigation")]
public sealed class NavigationController(
    INavigationFacadeService navigation,
    IValhallaService valhalla,
    IRoutePointService routePoints,
    IOptions<RoutingOptions> routingOptions) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        StartNavigationRequest request, CancellationToken cancellationToken) =>
        Result(await navigation.StartAsync(UserId(), request.RecommendationId, cancellationToken), true);

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken) =>
        Result(await navigation.GetActiveAsync(UserId(), cancellationToken));

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken) =>
        Result(await navigation.GetAsync(UserId(), sessionId, cancellationToken));

    [HttpGet("geometry")]
    [AllowAnonymous]
    public async Task<IActionResult> Geometry(
        [FromQuery] double startLat,
        [FromQuery] double startLon,
        [FromQuery] double endLat,
        [FromQuery] double endLon,
        [FromQuery] string mode,
        [FromQuery] long? routeId,
        [FromQuery] double? startRouteProgressMeters,
        [FromQuery] double? endRouteProgressMeters,
        CancellationToken cancellationToken)
    {
        if (!ValidCoordinate(startLat, startLon) || !ValidCoordinate(endLat, endLon))
            return BadRequest(new { error = "INVALID_COORDINATES" });

        var normalizedMode = (mode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedMode == "JEEPNEY")
        {
            if (routeId is not > 0)
                return BadRequest(new { error = "JEEPNEY_ROUTE_REQUIRED" });

            var points = await routePoints.GetRoutePointsAsync(routeId.Value, cancellationToken);
            var ordered = points.OrderBy(item => item.PointOrder).ToList();
            if (ordered.Count < 2)
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "JEEPNEY_GEOMETRY_UNAVAILABLE" });

            var useProgressSlice = startRouteProgressMeters is >= 0 &&
                endRouteProgressMeters > startRouteProgressMeters;
            var startIndex = useProgressSlice
                ? IndexAtProgress(ordered, startRouteProgressMeters!.Value, includePrevious: true)
                : ClosestIndex(ordered, startLat, startLon);
            var endIndex = useProgressSlice
                ? IndexAtProgress(ordered, endRouteProgressMeters!.Value, includePrevious: false)
                : ClosestIndex(ordered, endLat, endLon);
            var from = Math.Min(startIndex, endIndex);
            var to = Math.Max(startIndex, endIndex);
            var geometry = ordered.Skip(from).Take(to - from + 1)
                .Select(item => new NavigationGeometryPoint(item.Latitude, item.Longitude))
                .ToList();
            if (startIndex > endIndex) geometry.Reverse();
            return Ok(new NavigationGeometryResponse(geometry));
        }

        var costing = normalizedMode is "TRICYCLE" or "TRIKE"
            ? routingOptions.Value.TrikeCostingModel
            : "pedestrian";
        var response = await valhalla.GetRouteAsync(
            startLat, startLon, endLat, endLon, costing, cancellationToken);
        var roadGeometry = response.Trip?.Legs
            .SelectMany(leg => leg.Points)
            .Where(point => point.Length >= 2)
            .Select(point => new NavigationGeometryPoint(point[1], point[0]))
            .ToList() ?? [];

        return roadGeometry.Count >= 2
            ? Ok(new NavigationGeometryResponse(roadGeometry))
            : StatusCode(StatusCodes.Status502BadGateway, new { error = "GEOMETRY_UNAVAILABLE" });
    }

    [HttpPost("{sessionId:guid}/location")]
    public async Task<IActionResult> Location(Guid sessionId, LocationUpdate update,
        CancellationToken cancellationToken) =>
        Result(await navigation.UpdateLocationAsync(UserId(), sessionId, update, cancellationToken));

    [HttpPost("{sessionId:guid}/boarding")]
    public async Task<IActionResult> Boarding(Guid sessionId, CancellationToken cancellationToken) =>
        Result(await navigation.ConfirmBoardingAsync(UserId(), sessionId, cancellationToken));

    [HttpPost("{sessionId:guid}/alighting")]
    public async Task<IActionResult> Alighting(Guid sessionId, CancellationToken cancellationToken) =>
        Result(await navigation.ConfirmAlightingAsync(UserId(), sessionId, cancellationToken));

    [HttpPost("{sessionId:guid}/alight-status")]
    public async Task<IActionResult> ResolveAlightStatus(
        Guid sessionId,
        ResolveAlightStatusRequest request,
        CancellationToken cancellationToken) =>
        Result(await navigation.ResolveAlightStatusAsync(
            UserId(), sessionId, request.AlreadyOff, cancellationToken));

    [HttpPost("{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken) =>
        Result(await navigation.CancelAsync(UserId(), sessionId, cancellationToken));

    [HttpPost("{sessionId:guid}/reroute")]
    public async Task<IActionResult> Reroute(Guid sessionId, NavigationRerouteRequest request,
        CancellationToken cancellationToken) =>
        Result(await navigation.RerouteAsync(UserId(), sessionId, request, cancellationToken));

    private IActionResult Result(NavigationOperation operation, bool created = false)
    {
        if (operation.Snapshot is not null)
            return created ? StatusCode(StatusCodes.Status201Created, operation.Snapshot) : Ok(operation.Snapshot);
        return operation.Error is "TRIP_SESSION_NOT_FOUND" or "JOURNEY_NOT_FOUND" or "NO_ACTIVE_TRIP"
            ? NotFound(new { error = operation.Error })
            : Conflict(new { error = operation.Error });
    }

    private static bool ValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static int ClosestIndex(IReadOnlyList<RoutePointDetailsDto> points, double latitude, double longitude)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < points.Count; index++)
        {
            var lat = points[index].Latitude - latitude;
            var lon = points[index].Longitude - longitude;
            var distance = lat * lat + lon * lon;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }
        return bestIndex;
    }

    private static int IndexAtProgress(
        IReadOnlyList<RoutePointDetailsDto> points,
        double targetMeters,
        bool includePrevious)
    {
        var progress = 0d;
        for (var index = 1; index < points.Count; index++)
        {
            progress += Geo.DistanceMeters(
                points[index - 1].Latitude,
                points[index - 1].Longitude,
                points[index].Latitude,
                points[index].Longitude);
            if (progress >= targetMeters)
                return includePrevious ? index - 1 : index;
        }
        return points.Count - 1;
    }

    private Guid UserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

public sealed record NavigationGeometryPoint(double Latitude, double Longitude);
public sealed record NavigationGeometryResponse(IReadOnlyList<NavigationGeometryPoint> Points);
