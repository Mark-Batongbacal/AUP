using System.Text.Json;
using backend.Models.Database;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Localization;
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
    IConfiguration? configuration = null,
    IUserProfileRepository? userProfiles = null)
    : ITukiAssistantService
{
    private static readonly TimeSpan QwenTimeout = TimeSpan.FromSeconds(15);
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    private readonly ChatClient? _voiceClient = CreateQwenClient(configuration);

    public async Task<AssistantResponse> RespondAsync(Guid userId, AssistantRequest request, CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("AI");
        if (string.IsNullOrWhiteSpace(request.Message))
            return new("INVALID_REQUEST", "Message cannot be empty.");

        var language = await ResolveLanguageAsync(userId, cancellationToken);
        var normalizedMessage = request.Message.Trim();
        if (IsGreeting(normalizedMessage))
        {
            _telemetry.Event("AIIntentParsed", outcome: "Greeting");
            return new(
                "GREETING",
                Text(language, "Uy! Saan tayo pupunta?", "Hey! Where are we headed?"));
        }

        AssistantIntent intent;
        var deterministicIntent = NavigationIntent(normalizedMessage, request.TripSessionId);
        try
        {
            using (_telemetry.Measure("AI.Intent"))
            {
                intent = deterministicIntent ?? await intentExtractor.ExtractAsync(normalizedMessage, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "AI intent extraction failed");
            _telemetry.Event("AIResponseFailed");
            return new(
                "AI_UNAVAILABLE",
                Text(
                    language,
                    "Temporary unavailable si Tuki AI ngayon, pero gumagana pa rin ang search, routing, at active navigation.",
                    "Tuki AI is temporarily unavailable, but search, routing, and active navigation still work normally."));
        }

        intent.TripSessionId ??= request.TripSessionId;
        _telemetry.Event("AIIntentParsed", outcome: intent.Intent.ToString());
        var response = intent.Intent switch
        {
            AssistantIntentType.PlanRoute => await PlanAsync(userId, intent, request, language, cancellationToken),
            AssistantIntentType.Lost or AssistantIntentType.NavigationQuestion =>
                await NavigationStatusAsync(userId, intent, language, cancellationToken),
            AssistantIntentType.CancelTrip => new AssistantResponse(
                "ACTION_REQUIRED",
                Text(
                    language,
                    "I-confirm mo muna kung gusto mong i-cancel yung trip, tapos gamitin natin yung cancel command.",
                    "Confirm that you want to cancel the trip, then use the trip cancellation command.")),
            AssistantIntentType.StartNavigation => new AssistantResponse(
                "ACTION_REQUIRED",
                Text(
                    language,
                    "Pumili muna tayo ng saved route bago simulan ang navigation.",
                    "Select a saved route before starting navigation.")),
            _ => new AssistantResponse(
                "CLARIFICATION_REQUIRED",
                Text(
                    language,
                    "Saan tayo pupunta? Pwede ka ring magtanong tungkol sa active trip natin.",
                    "Where are we headed? You can also ask me about your active trip."))
        };

        return await ApplyTukiVoiceAsync(normalizedMessage, response, language, cancellationToken);
    }

    private async Task<AssistantResponse> ApplyTukiVoiceAsync(
        string userMessage,
        AssistantResponse response,
        string language,
        CancellationToken cancellationToken)
    {
        if (_voiceClient is null ||
            response.Status is "AI_UNAVAILABLE" or "INVALID_REQUEST" ||
            UsesCanonicalTukiVoice(response.Status))
            return response;

        var payload = JsonSerializer.Serialize(new
        {
            userMessage,
            response.Status,
            canonicalResponse = response.Message,
            language
        });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(QwenTimeout);

        try
        {
            using var measurement = _telemetry.Measure("AI.Voice");
            var completion = await _voiceClient.CompleteChatAsync(
            [
                new SystemChatMessage(VoicePrompt(language)),
                new UserChatMessage(payload)
            ], cancellationToken: timeout.Token);

            var styled = completion.Value.Content.FirstOrDefault()?.Text?.Trim();
            return string.IsNullOrWhiteSpace(styled) ? response : response with { Message = styled };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Qwen Tuki voice exceeded {TimeoutSeconds}s; using canonical response",
                QwenTimeout.TotalSeconds);
            _telemetry.Event("AIVoiceFallback", outcome: "Timeout");
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Qwen Tuki voice generation failed; using canonical assistant response");
            _telemetry.Event("AIVoiceFallback");
            return response;
        }
    }

    private async Task<string> ResolveLanguageAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userProfiles is null || userId == Guid.Empty)
            return TukiLanguage.English;

        try
        {
            var profile = await userProfiles.GetActiveByUserIdAsync(userId, cancellationToken);
            return TukiLanguage.Normalize(profile?.PreferredLanguage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not load assistant language preference; defaulting to English");
            return TukiLanguage.English;
        }
    }

    private static string VoicePrompt(string language) =>
        TukiLanguage.IsFilipino(language)
            ? """
                You are Tuki, a cheerful Filipino commute buddy and friendly toucan.
                Rewrite ONLY the supplied canonical response into natural conversational Filipino/Taglish.
                Be warm, concise, and clear. Sound like a Filipino friend commuting with the user.
                Prefer tayo/natin/mo/ka. Never use kami for the user's trip.
                Common English commute words such as route, destination, ETA, cheapest, fastest, jeep, tricycle, and TODA are okay.
                Avoid formal Filipino such as iyong, patungo sa, kinakailangan mong, and magpatuloy sa paglalakad.
                Maximum 2 short sentences.

                Preserve every fact, number, place, route, fare, distance, time, direction, transport mode, and trip state.
                Never invent information or change numbers. Keep clarification questions as clarification questions.
                Return plain text only.
                """
            : """
                You are Tuki, a cheerful commute buddy and friendly toucan.
                Rewrite ONLY the supplied canonical response into natural conversational English.
                Be warm, concise, friendly, and clear. Sound like a helpful friend commuting with the user, not customer support.
                Maximum 2 short sentences.

                Preserve every fact, number, place, route, fare, distance, time, direction, transport mode, and trip state.
                Never invent information or change numbers. Keep clarification questions as clarification questions.
                Return plain text only.
                """;

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

    private static bool UsesCanonicalTukiVoice(string status) => status is
        "GREETING" or
        "CLARIFICATION_REQUIRED" or
        "DESTINATION_AMBIGUOUS" or
        "JOURNEYS_AVAILABLE" or
        "NO_ACTIVE_TRIP";

    private static bool IsGreeting(string message)
    {
        var normalized = message.Trim().TrimEnd('!', '?', '.', ',').ToLowerInvariant();
        return normalized is
            "hello" or "hi" or "hey" or "yo" or "yoo" or "uy" or
            "hello tuki" or "hi tuki" or "hey tuki" or "yo tuki" or
            "kumusta" or "kamusta" or "kumusta tuki" or "kamusta tuki";
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
            normalized.Contains("missed the stop", StringComparison.Ordinal) ||
            normalized.Contains("nasaan ako", StringComparison.Ordinal) ||
            normalized.Contains("naliligaw", StringComparison.Ordinal) ||
            normalized.Contains("tama ba daan", StringComparison.Ordinal) ||
            normalized.Contains("tama ba yung daan", StringComparison.Ordinal) ||
            normalized.Contains("tama ba route", StringComparison.Ordinal) ||
            normalized.Contains("lumagpas", StringComparison.Ordinal);
        return navigationQuestion
            ? new AssistantIntent { Intent = AssistantIntentType.NavigationQuestion, TripSessionId = tripSessionId }
            : null;
    }

    private async Task<AssistantResponse> PlanAsync(
        Guid userId,
        AssistantIntent intent,
        AssistantRequest request,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.DestinationQuery))
            return new(
                "CLARIFICATION_REQUIRED",
                Text(language, "Saan tayo pupunta?", "Where are we headed?"));

        var resolved = await destinationSearch.SearchAsync(
            intent.DestinationQuery,
            new(request.OriginLatitude, request.OriginLongitude),
            cancellationToken);
        if (resolved.Error is not null)
            return new(
                resolved.Error,
                resolved.Message ?? Text(language, "Hindi nag-work yung destination search.", "Destination search failed."));
        if (resolved.Results.Count == 0)
            return new(
                "DESTINATION_NOT_FOUND",
                Text(language, "Hindi ko mahanap yung destination na yun.", "I couldn't find that destination."));

        var destination = ResolveDestination(resolved.Results, intent.DestinationQuery, request.DestinationId);
        if (destination is null)
        {
            if (!string.IsNullOrWhiteSpace(request.DestinationId))
                return new(
                    "DESTINATION_SELECTION_INVALID",
                    Text(
                        language,
                        "Hindi kasama sa current results yung napiling destination.",
                        "The selected destination is not one of the current search results."),
                    Destinations: resolved.Results);
            return new(
                "DESTINATION_AMBIGUOUS",
                Text(
                    language,
                    $"May ilang results para sa {intent.DestinationQuery}. Alin dito yung gusto mong puntahan?",
                    $"I found a few results for {intent.DestinationQuery}. Which one is your destination?"),
                Destinations: resolved.Results);
        }

        if (request.OriginLatitude is not { } originLat || request.OriginLongitude is not { } originLon)
            return new(
                "ORIGIN_REQUIRED",
                Text(
                    language,
                    "I-share mo yung current location mo o maglagay ng starting point.",
                    "Share your current location or specify a starting point."));

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
            return new(
                "NO_JOURNEY_WITHIN_CONSTRAINTS",
                intent.BudgetPesos is { } budget
                    ? Text(
                        language,
                        $"Wala akong mahanap na Tuki route na pasok sa ₱{budget:0.##}.",
                        $"I couldn't find a Tuki route within ₱{budget:0.##}.")
                    : Text(
                        language,
                        "Wala akong mahanap na supported route para dito.",
                        "Tuki couldn't find a supported route for this trip."));

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
            return new(
                "JOURNEY_PERSISTENCE_FAILED",
                Text(
                    language,
                    "Nakuha ko yung routes pero hindi ko sila ma-save ngayon.",
                    "I calculated the routes but couldn't save them right now."));
        }

        var journeys = persisted.Select(item => Map(item.Recommendation.RecommendationId, item.Plan)).ToList();
        return new(
            "JOURNEYS_AVAILABLE",
            Text(
                language,
                $"Ayun! May {journeys.Count} route options tayo papuntang {destination.Name}.",
                $"Got it! We have {journeys.Count} route options to {destination.Name}."),
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

    private async Task<AssistantResponse> NavigationStatusAsync(
        Guid userId,
        AssistantIntent intent,
        string language,
        CancellationToken cancellationToken)
    {
        var session = intent.TripSessionId is { } id
            ? await sessions.GetOwnedAsync(id, userId, cancellationToken)
            : await sessions.GetActiveOwnedAsync(userId, cancellationToken);
        if (session is null)
            return new(
                "NO_ACTIVE_TRIP",
                Text(language, "Wala tayong active trip ngayon.", "You don't have an active trip right now."));

        var next = (await instructions.GetForOwnedSessionAsync(session.TripSessionId, userId, cancellationToken))
            .FirstOrDefault(item => item.LegIndex >= session.CurrentLegIndex);
        var status = session.LastNavigationStatus ??
            (session.CurrentNavigationState == TripNavigationState.OffRoute ? "OFF_ROUTE" : "ON_ROUTE");
        var message = status switch
        {
            "MISSED_ALIGHT" => Text(
                language,
                "Mukhang lumagpas tayo sa babaan. Pwede nating i-check ang reroute mula sa current location mo.",
                "Looks like we passed the planned stop. We can check a reroute from your current location."),
            "OFF_ROUTE" => Text(
                language,
                "Mukhang lumalayo tayo sa planned route. Pwede tayong mag-reroute.",
                "We're moving away from the planned route. We can reroute from here."),
            "UNCERTAIN_GPS" => Text(
                language,
                "Hindi sapat yung GPS accuracy ngayon para masabi kung nasa route pa tayo.",
                "Your GPS isn't accurate enough right now to tell whether you're still on route."),
            _ => next is null
                ? Text(language, "Nasa active trip pa tayo.", "You're still on the active trip.")
                : Text(
                    language,
                    $"Nasa route pa tayo. Next: {next.Text}",
                    $"You're still on route. Next: {next.Text}")
        };
        return new(status, message, Navigation: new
        {
            tripState = session.CurrentNavigationState.ToString(),
            session.CurrentLegIndex,
            nextInstruction = next?.Text,
            session.CurrentProgressMeters
        });
    }

    private static string Text(string language, string filipino, string english) =>
        TukiLanguage.IsFilipino(language) ? filipino : english;

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
