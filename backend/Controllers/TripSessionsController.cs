using System.Security.Claims;
using backend.Services.TripSessions;
using backend.Services.Navigation;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/tripsessions")]
public sealed class TripSessionsController(
    ITripSessionService service,
    INavigationInstructionService navigationInstructions,
    ILocationTrackingService locationTracking,
    IReroutingService rerouting) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateTripSessionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.CreateAsync(UserId(), request, cancellationToken), true);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetAsync(UserId(), id, cancellationToken));

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken) =>
        ToActionResult(await service.GetActiveAsync(UserId(), cancellationToken));

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await service.StartAsync(UserId(), id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await service.CancelAsync(UserId(), id, cancellationToken));

    [HttpPost("{id:guid}/boarding-confirmed")]
    public async Task<IActionResult> BoardingConfirmed(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await service.ConfirmBoardingAsync(UserId(), id, cancellationToken));

    [HttpPost("{id:guid}/alighting-confirmed")]
    public async Task<IActionResult> AlightingConfirmed(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await service.ConfirmAlightingAsync(UserId(), id, cancellationToken));

    [HttpGet("{id:guid}/instructions")]
    public async Task<IActionResult> Instructions(Guid id, CancellationToken cancellationToken)
    {
        var owned = await service.GetAsync(UserId(), id, cancellationToken);
        if (!owned.Succeeded) return NotFound(new { error = owned.Error });
        return Ok(await navigationInstructions.GetOwnedAsync(id, UserId(), cancellationToken));
    }

    [HttpPost("{id:guid}/location")]
    public async Task<IActionResult> Location(Guid id, LocationUpdate update, CancellationToken cancellationToken)
    {
        var result = await locationTracking.ProcessAsync(UserId(), id, update, cancellationToken);
        return result.Accepted ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/reroute")]
    public async Task<IActionResult> Reroute(Guid id, RerouteRequest request, CancellationToken cancellationToken)
    {
        var result = await rerouting.RerouteAsync(UserId(), id,
            new NavigationRerouteRequest(request.Reason), cancellationToken);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }

    private Guid UserId() => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private IActionResult ToActionResult(TripSessionOperation result, bool created = false)
    {
        if (result.Succeeded)
            return created
                ? CreatedAtAction(nameof(Get), new { id = result.Session!.TripSessionId }, result.Session)
                : Ok(result.Session);
        return result.Error is "TRIP_SESSION_NOT_FOUND" or "JOURNEY_NOT_FOUND" or "NO_ACTIVE_TRIP"
            ? NotFound(new { error = result.Error })
            : Conflict(new { error = result.Error });
    }
}

public sealed record RerouteRequest(string Reason = "OFF_ROUTE");
