using System.Text.Json;
using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Routing;
using backend.Services.Telemetry;
using OpenAI;
using OpenAI.Chat;

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
    ITukiTelemetry? telemetry = null,
    IConfiguration? configuration = null)
    : ITukiAssistantService
{
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    private readonly ChatClient? _voiceClient = CreateQwenClient(configuration);

    public async Task<AssistantResponse> RespondAsync(Guid userId, AssistantRequest request, CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("AI");
        if (string.IsNullOrWhiteSpace(request.Message)) return new("INVALID_REQUEST", "Message cannot be empty.");
        AssistantIntent intent;
        var normalizedMessage = request.Message.Trim();
        var deterministicIntent = NavigationIntent(normalizedMessage, request.TripSessionId);
        try
        {
            intent = deterministicIntent ?? await intentExtractor.ExtractAsync(normalizedMessage, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "AI intent extraction failed");
            _telemetry.Event("AIResponseFailed");
            return new("AI_UNAVAILABLE", "The assistant is temporarily unavailable. Search, routing, and active navigation still work normally.");
        }

        intent.TripSessionId ??= request.TripSessionId;
        _telemetry.Event("AIIntentParsed", outcome: intent.Intent.ToString());
        var response = intent.Intent switch
        {
            AssistantIntentType.PlanRoute => await PlanAsync(userId, intent, request, cancellationToken),
            AssistantIntentType.Lost or AssistantIntentType.NavigationQuestion => await NavigationStatusAsync(userId, intent, cancellationToken),
            AssistantIntentType.CancelTrip => new AssistantResponse("ACTION_REQUIRED", "Use the trip cancellation command after confirming you want to cancel."),
            AssistantIntentType.StartNavigation => new AssistantResponse("ACTION_REQUIRED", "Select a stored journey before starting navigation."),
            _ => new AssistantResponse("CLARIFICATION_REQUIRED", "Tell me the destination or ask about your active trip.")
        };

        return await ApplyTukiVoiceAsync(normalizedMessage, response, cancellationToken);
    }

    private async Task<AssistantResponse> ApplyTukiVoiceAsync(
        string userMessage,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        if (_voiceClient is null || response.Status is "AI_UNAVAILABLE" or "INVALID_REQUEST")
            return response;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                userMessage,
                response.Status,
                canonicalResponse = response.Message
            });
            var completion = await _voiceClient.CompleteChatAsync(
            [
                new SystemChatMessage("""
                    You are Tuki, a cheerful Filipino commute buddy and friendly toucan.
                    Rewrite ONLY the supplied canonical response into natural, conversational Filipino/Taglish.

                    STYLE:
                    - Sound like a Filipino friend commuting with the user.
                    - Warm, concise, energetic, and natural.
                    - Never sound formal, translated, robotic, or like customer support.
                    - Prefer everyday phrasing using "tayo", "natin", "mo", and "ka".
                    - Common English words like route, destination, ETA, cheapest, fastest, jeep, tricycle, and TODA are okay when natural.
                    - Keep navigation instructions extremely clear and short.
                    - Maximum 2 short sentences.

                    AVOID:
                    - "iyong"
                    - "patungo sa"
                    - "kinakailangan mong"
                    - "magpatuloy sa paglalakad"
                    - "lakad na 50m ka na lang"
                    - "papunta sa iyong destination"
                    - overly formal Filipino

                    PREFER NATURAL PHRASES:
                    - "Lakad pa tayo nang 50m."
                    - "50m na lang."
                    - "Malapit na tayo!"
                    - "Diretso lang tayo."
                    - "Mga 100m pa, tapos kaliwa tayo."
                    - "Sakay tayo ng jeep dito."
                    - "Sa Checkpoint tayo bababa."
                    - "Mga 8:42 tayo makakarating."
                    - "Aabot pa tayo."

                    EXAMPLES:

                    DESTINATION CLARIFICATION:
                    - When multiple places match the user's search, speak naturally.
                    - Never say phrases like:
                    "maraming X yung matching"
                    "alin tayo puntahan?"
                    "multiple matching destinations"
                    "maraming destination ang nag-match"

                    - Prefer:
                    "May ilang results para sa [place]. Alin dito yung gusto mong puntahan?"
                    "May ilang places na lumabas para sa [place]. Alin dito yung destination mo?"

                    Example:

                    BAD:
                    "Maraming 'AUF' yung matching. Alin tayo puntahan?"

                    GOOD:
                    "May ilang results para sa AUF. Alin dito yung gusto mong puntahan?"

                    Canonical:
                    "Continue walking for 50 meters."
                    Tuki:
                    "Sige, lakad pa tayo nang 50m. Konti na lang!"

                    Canonical:
                    "Your destination is 80 meters away."
                    Tuki:
                    "Malapit na tayo! Mga 80m na lang."

                    Canonical:
                    "Turn left in 100 meters."
                    Tuki:
                    "Mga 100m pa, tapos kaliwa tayo."

                    Canonical:
                    "Get off at Checkpoint."
                    Tuki:
                    "Sa Checkpoint tayo bababa."

                    Canonical:
                    "The cheapest route costs 43 pesos and takes 38 minutes."
                    Tuki:
                    "₱43 yung cheapest route, around 38 minutes ang biyahe."

                    Canonical:
                    "Which destination do you mean?"
                    Tuki:
                    "Saan dito yung gusto mong puntahan?"

                    GROUNDING:
                    - Preserve every fact, number, place, route, fare, distance, time, direction, transport mode, and trip state.
                    - Do not invent information.
                    - Do not change numbers.
                    - Do not claim an action happened unless the canonical response says so.
                    - If it asks for clarification, preserve the clarification.
                    - Prefer "route options tayo papuntang [place]" over phrases like
                    "journey options para sa [place]" or "supported journey options".
                    - Avoid backend/system terminology such as "journey option", "supported journey",
                    "available journey", or similar phrases when a simpler commuter phrase works.
                    Return ONLY the rewritten plain-text response.
                    """),
                new UserChatMessage(payload)
            ], cancellationToken: cancellationToken);

            var styled = completion.Value.Content.FirstOrDefault()?.Text?.Trim();
            return string.IsNullOrWhiteSpace(styled) ? response : response with { Message = styled };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Qwen Tuki voice generation failed; using canonical assistant response");
            _telemetry.Event("AIVoiceFallback");
            return response;
        }
    }

    private static ChatClient? CreateQwenClient(IConfiguration? configuration)
    {
        if (configuration is null)
            return null;

        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Qwen:ApiKeyEnvironmentVariable"] ??
            configuration["Nvidia:ApiKeyEnvironmentVariable"] ??
            "NVIDIA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        return new ChatClient(
            configuration["Qwen:Model"] ?? "nvidia/llama-3.3-nemotron-super-49b-v1.5",
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(configuration["Qwen:BaseUrl"] ??
                    configuration["Nvidia:BaseUrl"] ??
                    "https://integrate.api.nvidia.com/v1")
            });
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
            ? new AssistantIntent { Intent = AssistantIntentType.NavigationQuestion, TripSessionId = tripSessionId }
            : null;
    }

    private async Task<AssistantResponse> PlanAsync(Guid userId, AssistantIntent intent, AssistantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.DestinationQuery))
            return new("CLARIFICATION_REQUIRED", "Which destination do you mean?");

        var resolved = await destinationSearch.SearchAsync(
            intent.DestinationQuery,
            new(request.OriginLatitude, request.OriginLongitude),
            cancellationToken);
        if (resolved.Error is not null)
            return new(resolved.Error, resolved.Message ?? "Destination search failed.");
        if (resolved.Results.Count == 0)
            return new("DESTINATION_NOT_FOUND", "I could not find that destination.");

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
        try
        {
            plans = await routing.PlanTripsAsync(
                originLat, originLon,
                destination.Latitude, destination.Longitude,
                cancellationToken);
        }
        catch (RoutingValidationException exception)
        {
            return new(exception.ErrorCode, exception.Message);
        }

        var eligible = plans.Where(plan => intent.BudgetPesos is null || (decimal)plan.TotalFarePesos <= intent.BudgetPesos.Value);
        if (!string.IsNullOrWhiteSpace(intent.Preference))
            eligible = eligible.OrderByDescending(plan => plan.RecommendationType.Split(',')
                .Contains(intent.Preference, StringComparer.OrdinalIgnoreCase));
        var eligiblePlans = eligible.ToList();

        if (eligiblePlans.Count == 0)
            return new("NO_JOURNEY_WITHIN_CONSTRAINTS", intent.BudgetPesos is { } budget
                ? $"I found no Tuki journey within ₱{budget:0.##}."
                : "Tuki found no supported journey.");

        if (routing is IJourneyGeometryEnricher geometryEnricher)
            await geometryEnricher.EnrichSelectedPlanGeometryAsync(eligiblePlans, cancellationToken);

        IReadOnlyList<PersistedJourney> persisted;
        try
        {
            persisted = await persistence.PersistAsync(
                userId,
                originLat, originLon, destination.Name,
                destination.Latitude, destination.Longitude,
                intent.BudgetPesos, intent.Preference, eligiblePlans,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist assistant journeys");
            return new("JOURNEY_PERSISTENCE_FAILED", "Tuki calculated routes but could not save them.");
        }

        var journeys = persisted.Select(item => Map(item.Recommendation.RecommendationId, item.Plan)).ToList();
        return new(
            "JOURNEYS_AVAILABLE",
            $"Tuki found {journeys.Count} supported journey option(s) to {destination.Name}.",
            Journeys: journeys,
            Destination: destination);
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
        var status = session.LastNavigationStatus ??
            (session.CurrentNavigationState == TripNavigationState.OffRoute ? "OFF_ROUTE" : "ON_ROUTE");
        var message = status switch
        {
            "MISSED_ALIGHT" => "You appear to have passed the planned alighting point. Review a reroute from your current location.",
            "OFF_ROUTE" => "Tuki has confirmed sustained movement away from the expected route. You may request a reroute.",
            "UNCERTAIN_GPS" => "Your GPS is not accurate enough to determine whether you are still on route.",
            _ => next is null ? "You are still on the active trip." : $"You are still on route. Next: {next.Text}"
        };
        return new(status, message, Navigation: new
        {
            tripState = session.CurrentNavigationState.ToString(),
            session.CurrentLegIndex,
            nextInstruction = next?.Text,
            session.CurrentProgressMeters
        });
    }

    private static AssistantJourney Map(Guid recommendationId, JeepneyTripPlan plan) =>
        new(
            recommendationId,
            plan.RecommendationType,
            plan.TotalFarePesos,
            plan.TotalTimeSeconds,
            plan.Legs.Where(leg => leg.Mode == AccessMode.Walk).Sum(leg => leg.DistanceMeters),
            plan.Legs.Select(leg => new AssistantJourneyLeg(leg.Mode.ToString(), leg.RouteName)).ToList(),
            plan);
}
