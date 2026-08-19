using System.Security.Claims;
using backend.Services.Navigation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/navigation")]
public sealed class NavigationController(INavigationFacadeService navigation) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        StartNavigationRequest request, CancellationToken cancellationToken) =>
        Result(await navigation.StartAsync(UserId(), request.RecommendationId, cancellationToken), true);

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken) =>
        Result(await navigation.GetActiveAsync(UserId(), cancellationToken));

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

    [HttpPost("{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken) =>
        Result(await navigation.CancelAsync(UserId(), sessionId, cancellationToken));

    [HttpPost("{sessionId:guid}/reroute")]
    public async Task<IActionResult> Reroute(Guid sessionId, RerouteRequest request,
        CancellationToken cancellationToken) =>
        Result(await navigation.RerouteAsync(UserId(), sessionId, request.Reason, cancellationToken));

    private IActionResult Result(NavigationOperation operation, bool created = false)
    {
        if (operation.Snapshot is not null)
            return created ? StatusCode(StatusCodes.Status201Created, operation.Snapshot) : Ok(operation.Snapshot);
        return operation.Error is "TRIP_SESSION_NOT_FOUND" or "JOURNEY_NOT_FOUND" or "NO_ACTIVE_TRIP"
            ? NotFound(new { error = operation.Error })
            : Conflict(new { error = operation.Error });
    }

    private Guid UserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}
