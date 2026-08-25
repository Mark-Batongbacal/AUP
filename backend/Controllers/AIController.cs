using backend.Repositories;
using backend.Services.Assistant;
using backend.Services.Navigation;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AIController(
    ITukiAssistantService assistant,
    IReroutingService rerouting,
    IRouteRecommendationRepository recommendations) : ControllerBase
{
    // Planning / booking surface. The optional TripSessionId branch is kept only
    // for backwards compatibility with older mobile builds; current clients use
    // the explicit active-trip endpoint below.
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(
        [FromBody] AssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
            return Unauthorized();

        AssistantResponse response;
        if (request.TripSessionId is { } sessionId)
        {
            using var proposalScope = AssistantProposalScope.Begin(sessionId);
            response = await assistant.RespondActiveTripAsync(
                userId,
                sessionId,
                new ActiveTripAssistantRequest(
                    request.Message ?? string.Empty,
                    request.DestinationId,
                    request.ConversationId,
                    request.OperationId),
                cancellationToken);
        }
        else
        {
            response = await assistant.RespondPlanningAsync(
                userId,
                request,
                cancellationToken);
        }

        return AssistantResult(response);
    }

    // Active-trip conversational surface. The model never chooses the session ID;
    // the owned TripSession comes from this route and server-side state.
    [HttpPost("trip/{sessionId:guid}/ask")]
    public async Task<IActionResult> AskActiveTrip(
        Guid sessionId,
        [FromBody] ActiveTripAssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
            return Unauthorized();

        using var proposalScope = AssistantProposalScope.Begin(sessionId);
        var response = await assistant.RespondActiveTripAsync(
            userId,
            sessionId,
            request,
            cancellationToken);
        return AssistantResult(response);
    }

    // A route proposed by the active-trip assistant is inert until the passenger
    // selects that exact recommendation card. The metadata stamp prevents a recent
    // planning-screen recommendation from being applied to an unrelated live trip.
    [HttpPost("trip/{sessionId:guid}/replan/{recommendationId:guid}/confirm")]
    public async Task<IActionResult> ConfirmActiveTripReplan(
        Guid sessionId,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
            return Unauthorized();

        var recommendation = await recommendations.GetByIdAsync(
            recommendationId,
            cancellationToken);
        if (recommendation is null ||
            !AssistantProposalMetadata.IsForTrip(recommendation.Explanation, sessionId))
        {
            return NotFound(new { error = "REPLAN_PROPOSAL_NOT_FOUND" });
        }

        var result = await rerouting.ApplyRecommendationAsync(
            userId,
            sessionId,
            recommendationId,
            cancellationToken);

        if (result.Succeeded)
            return Ok(new
            {
                status = result.Status,
                recommendationId = result.RecommendationId
            });

        return result.Status is "TRIP_SESSION_NOT_FOUND" or "REPLAN_PROPOSAL_NOT_FOUND"
            ? NotFound(new { error = result.Status })
            : Conflict(new { error = result.Status });
    }

    private IActionResult AssistantResult(AssistantResponse response) =>
        response.Status switch
        {
            "INVALID_REQUEST" => BadRequest(response),
            "INVALID_CONVERSATION" => NotFound(response),
            "NO_ACTIVE_TRIP" or "TRIP_NOT_ACTIVE" => NotFound(response),
            _ => Ok(response)
        };

    private bool TryUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
