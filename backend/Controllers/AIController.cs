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
            response = await AutoApplyActiveTripPreferenceAsync(
                userId,
                sessionId,
                response,
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
        response = await AutoApplyActiveTripPreferenceAsync(
            userId,
            sessionId,
            response,
            cancellationToken);
        return AssistantResult(response);
    }

    // Kept for backwards compatibility with older clients and with active-trip
    // changes outside the two auto-reroute preference families requested here.
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

    private async Task<AssistantResponse> AutoApplyActiveTripPreferenceAsync(
        Guid userId,
        Guid sessionId,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var action = response.Action;
        var isRequestedAutoReroutePreference =
            action?.MaxWalkingMeters is not null ||
            action?.AvoidTransportModes is { Count: > 0 };

        if (!isRequestedAutoReroutePreference ||
            !string.Equals(response.Status, "REPLAN_PROPOSAL", StringComparison.OrdinalIgnoreCase) ||
            response.Journeys is not { Count: > 0 })
            return response;

        // The active-trip assistant already filters and orders these journeys
        // using the interpreted walking/mode constraints. Apply the first valid
        // replacement only for the two requested auto-reroute preference families.
        var selected = response.Journeys[0];
        var result = await rerouting.ApplyRecommendationAsync(
            userId,
            sessionId,
            selected.JourneyId,
            cancellationToken);

        if (!result.Succeeded)
        {
            var message = result.Status == "NO_REROUTE_AVAILABLE"
                ? "There is no other available route for that preference. Your current trip stays unchanged."
                : $"I couldn't apply that preference right now ({result.Status}). Your current trip stays unchanged.";
            return response with
            {
                Status = result.Status,
                Message = message,
                Journeys = null,
                Action = null
            };
        }

        return response with
        {
            Status = "REROUTE_SUCCEEDED",
            Message = "Done. I rerouted your active trip using that preference.",
            Journeys = null,
            Action = null
        };
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
