using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Routing;
using backend.Services.Telemetry;

namespace backend.Services.Assistant;

public interface ITukiAssistantService
{
    Task<AssistantResponse> RespondAsync(Guid userId, AssistantRequest request, CancellationToken cancellationToken = default);
}

public sealed class TukiAssistantService(
    IAssistantIntentExtractor intentExtractor, IDestinationSearchService destinationSearch,
    IRoutingService routing, ITripSessionRepository sessions,
    INavigationInstructionRepository instructions,
    IJourneyPlanPersistenceService persistence,
    ILogger<TukiAssistantService> logger,
    ITukiTelemetry? telemetry = null)
    : ITukiAssistantService
{
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    public async Task<AssistantResponse> RespondAsync(Guid userId, AssistantRequest request, CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("AI");
        if (string.IsNullOrWhiteSpace(request.Message)) return new("INVALID_REQUEST", "Message cannot be empty.");
        AssistantIntent intent;
        var normalizedMessage = request.Message.Trim();
        var deterministicIntent = NavigationIntent(normalizedMessage, request.TripSessionId);
        try
        {
            intent = deterministicIntent ??
                await intentExtractor.ExtractAsync(normalizedMessage, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "AI intent extraction failed");
            _telemetry.Event("AIResponseFailed");
            return new("AI_UNAVAILABLE", "The assistant is temporarily unavailable. Search, routing, and active navigation still work normally.");
        }
        intent.TripSessionId ??= request.TripSessionId;
        _telemetry.Event("AIIntentParsed", outcome: intent.Intent.ToString());
        return intent.Intent switch
        {
            AssistantIntentType.PlanRoute => await PlanAsync(userId, intent, request, cancellationToken),
            AssistantIntentType.Lost or AssistantIntentType.NavigationQuestion => await NavigationStatusAsync(userId, intent, cancellationToken),
            AssistantIntentType.CancelTrip => new("ACTION_REQUIRED", "Use the trip cancellation command after confirming you want to cancel."),
            AssistantIntentType.StartNavigation => new("ACTION_REQUIRED", "Select a stored journey before starting navigation."),
            _ => new("CLARIFICATION_REQUIRED", "Tell me the destination or ask about your active trip.")
        };
    }

    private static AssistantIntent? NavigationIntent(string message, Guid? tripSessionId)
    {
        var normalized = message.ToLowerInvariant();
        var navigationQuestion = normalized.Contains("right way", StringComparison.Ordinal) ||
            normalized.Contains("still on route", StringComparison.Ordinal) ||
            normalized.Contains("where am i", StringComparison.Ordinal) ||
            normalized.Contains("am i lost", StringComparison.Ordinal) ||
            normalized.Contains("i'm lost", StringComparison.Ordinal) ||
            normalized.Contains("i am lost", StringComparison.Ordinal) ||
            normalized.Contains("missed my stop", StringComparison.Ordinal) ||
            normalized.Contains("missed the stop", StringComparison.Ordinal);
        return navigationQuestion
            ? new AssistantIntent
            {
                Intent = AssistantIntentType.NavigationQuestion,
                TripSessionId = tripSessionId
            }
            : null;
    }

    private async Task<AssistantResponse> PlanAsync(Guid userId, AssistantIntent intent, AssistantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.DestinationQuery)) return new("CLARIFICATION_REQUIRED", "Which destination do you mean?");
        var resolved = await destinationSearch.SearchAsync(intent.DestinationQuery,
            new(request.OriginLatitude, request.OriginLongitude), cancellationToken);
        if (resolved.Error is not null) return new(resolved.Error, resolved.Message ?? "Destination search failed.");
        if (resolved.Results.Count == 0) return new("DESTINATION_NOT_FOUND", "I could not find that destination.");
        var destination = ResolveDestination(resolved.Results, intent.DestinationQuery, request.DestinationId);
        if (destination is null)
        {
            if (!string.IsNullOrWhiteSpace(request.DestinationId))
                return new("DESTINATION_SELECTION_INVALID", "The selected destination is not one of the current search results.", Destinations: resolved.Results);
            return new("DESTINATION_AMBIGUOUS", "I found multiple matching destinations. Please choose one.", Destinations: resolved.Results);
        }
        if (request.OriginLatitude is not { } originLat || request.OriginLongitude is not { } originLon)
            return new("ORIGIN_REQUIRED", "Share your current location or specify an origin.");
        List<JeepneyTripPlan> plans;
        try { plans = await routing.PlanTripsAsync(originLat, originLon, destination.Latitude, destination.Longitude, cancellationToken); }
        catch (RoutingValidationException exception) { return new(exception.ErrorCode, exception.Message); }
        var eligible = plans.Where(plan => intent.BudgetPesos is null || (decimal)plan.TotalFarePesos <= intent.BudgetPesos.Value);
        if (!string.IsNullOrWhiteSpace(intent.Preference))
            eligible = eligible.OrderByDescending(plan => plan.RecommendationType.Split(',').Contains(intent.Preference, StringComparer.OrdinalIgnoreCase));
        var eligiblePlans = eligible.ToList();
        if (eligiblePlans.Count == 0)
            return new("NO_JOURNEY_WITHIN_CONSTRAINTS", intent.BudgetPesos is { } budget
                ? $"I found no Tuki journey within ₱{budget:0.##}." : "Tuki found no supported journey.");
        IReadOnlyList<PersistedJourney> persisted;
        try
        {
            persisted = await persistence.PersistAsync(
                userId,
                originLat, originLon, destination.Name,
                destination.Latitude, destination.Longitude,
                intent.BudgetPesos, intent.Preference, eligiblePlans, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist assistant journeys");
            return new("JOURNEY_PERSISTENCE_FAILED", "Tuki calculated routes but could not save them.");
        }
        var journeys = persisted.Select(item => Map(
            item.Recommendation.RecommendationId, item.Plan)).ToList();
        return new("JOURNEYS_AVAILABLE", $"Tuki found {journeys.Count} supported journey option(s) to {destination.Name}.", journeys);
    }

    private static backend.Models.Destinations.DestinationSearchResult? ResolveDestination(
        IReadOnlyList<backend.Models.Destinations.DestinationSearchResult> results,
        string query,
        string? selectedId)
    {
        if (!string.IsNullOrWhiteSpace(selectedId))
            return results.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal));
        if (results.Count == 1) return results[0];
        var exactMatches = results.Where(item => string.Equals(
            item.Name.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return exactMatches.Count == 1 ? exactMatches[0] : null;
    }

    private async Task<AssistantResponse> NavigationStatusAsync(Guid userId, AssistantIntent intent, CancellationToken cancellationToken)
    {
        var session = intent.TripSessionId is { } id
            ? await sessions.GetOwnedAsync(id, userId, cancellationToken)
            : await sessions.GetActiveOwnedAsync(userId, cancellationToken);
        if (session is null) return new("NO_ACTIVE_TRIP", "You do not have an active trip.");
        var next = (await instructions.GetForOwnedSessionAsync(session.TripSessionId, userId, cancellationToken))
            .FirstOrDefault(item => item.LegIndex >= session.CurrentLegIndex);
        var status = session.LastNavigationStatus ?? (session.CurrentNavigationState == TripNavigationState.OffRoute ? "OFF_ROUTE" : "ON_ROUTE");
        var message = status switch
        {
            "MISSED_ALIGHT" => "You appear to have passed the planned alighting point. Review a reroute from your current location.",
            "OFF_ROUTE" => "Tuki has confirmed sustained movement away from the expected route. You may request a reroute.",
            "UNCERTAIN_GPS" => "Your GPS is not accurate enough to determine whether you are still on route.",
            _ => next is null ? "You are still on the active trip." : $"You are still on route. Next: {next.Text}"
        };
        return new(status, message, Navigation: new
        {
            tripState = session.CurrentNavigationState.ToString(), session.CurrentLegIndex,
            nextInstruction = next?.Text, session.CurrentProgressMeters
        });
    }

    private static AssistantJourney Map(Guid recommendationId, JeepneyTripPlan plan)
    {
        return new(recommendationId, plan.RecommendationType, plan.TotalFarePesos, plan.TotalTimeSeconds,
            plan.Legs.Where(leg => leg.Mode == AccessMode.Walk).Sum(leg => leg.DistanceMeters),
            plan.Legs.Select(leg => new AssistantJourneyLeg(leg.Mode.ToString(), leg.RouteName)).ToList());
    }
}
