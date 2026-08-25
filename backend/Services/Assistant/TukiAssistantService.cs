using backend.Models.Database;
using backend.Models.Destinations;
using backend.Models.Routing;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Localization;
using backend.Services.Navigation;
using backend.Services.Routing;
using backend.Services.Telemetry;
using backend.Services;
using System.Text.Json;

namespace backend.Services.Assistant;

public interface ITukiAssistantService
{
    Task<AssistantResponse> RespondAsync(
        Guid userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default);

    Task<AssistantResponse> RespondPlanningAsync(
        Guid userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default);

    Task<AssistantResponse> RespondActiveTripAsync(
        Guid userId,
        Guid tripSessionId,
        ActiveTripAssistantRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class TukiAssistantService(
    IAssistantIntentExtractor intentExtractor,
    IDestinationSearchService destinationSearch,
    IRoutingService routing,
    ITripSessionRepository sessions,
    INavigationInstructionRepository instructions,
    IJourneyPlanPersistenceService persistence,
    ILogger<TukiAssistantService> logger,
    ITukiTelemetry? telemetry = null,
    IConfiguration? configuration = null,
    IUserProfileRepository? userProfiles = null,
    IRouteRecommendationRepository? recommendations = null,
    IChatService? chat = null,
    IAssistantPlaceResolver? assistantPlaces = null)
    : ITukiAssistantService
{
    private const int RecentConversationTurnLimit = 8;
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    private readonly IRouteRecommendationRepository? _recommendations = recommendations;
    private readonly IChatService? _chat = chat;
    private readonly IAssistantPlaceResolver? _assistantPlaces = assistantPlaces;
    private static readonly JsonSerializerOptions PlanningStateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly TimeSpan PendingDestinationLifetime = TimeSpan.FromMinutes(15);

    // Kept for constructor compatibility with the previous implementation.
    private readonly IConfiguration? _configuration = configuration;

    public Task<AssistantResponse> RespondAsync(
        Guid userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TripSessionId is { } tripSessionId)
        {
            return RespondActiveTripAsync(
                userId,
                tripSessionId,
                new ActiveTripAssistantRequest(
                    request.Message ?? string.Empty,
                    request.DestinationId,
                    request.ConversationId,
                    request.OperationId),
                cancellationToken);
        }

        return RespondPlanningAsync(userId, request, cancellationToken);
    }

    public async Task<AssistantResponse> RespondPlanningAsync(
        Guid userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("AI.Planning");
        var language = await ResolveLanguageAsync(userId, cancellationToken);
        var conversation = await ResolveConversationAsync(
            userId, request.ConversationId, "Tuki trip planning", cancellationToken);
        if (conversation.Error is not null)
            return new(
                conversation.Error,
                Text(language, "Hindi valid yung conversation na binuksan.", "That conversation is not available."),
                Surface: SurfaceName(AssistantSurface.Planning));

        if (IsDestinationSelectionRequest(request))
        {
            var selection = await ContinuePendingDestinationSelectionAsync(
                userId, request, language, conversation.Context, cancellationToken);
            return WithMetadata(
                selection,
                conversation.Context.ConversationId,
                AssistantSurface.Planning);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
            return new("INVALID_REQUEST", "Message cannot be empty.", Surface: SurfaceName(AssistantSurface.Planning));

        var normalizedMessage = request.Message!.Trim();
        if (IsGreeting(normalizedMessage))
        {
            var greeting = new AssistantResponse(
                "GREETING",
                Text(language, "Uy! Saan tayo pupunta?", "Hey! Where are we headed?"),
                ConversationId: conversation.Context.ConversationId == Guid.Empty
                    ? null
                    : conversation.Context.ConversationId,
                Surface: SurfaceName(AssistantSurface.Planning));
            await PersistConversationAsync(
                conversation.Context.ConversationId,
                normalizedMessage,
                greeting,
                null,
                cancellationToken);
            return greeting;
        }

        var context = new AssistantContext(
            AssistantSurface.Planning,
            normalizedMessage,
            conversation.Context);

        AssistantIntent intent;
        try
        {
            using (_telemetry.Measure("AI.Intent"))
                intent = await intentExtractor.ExtractAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Planning assistant intent extraction failed");
            _telemetry.Event("AIResponseFailed", outcome: "Planning");
            return new(
                "AI_UNAVAILABLE",
                Text(
                    language,
                    "Temporary unavailable si Tuki AI ngayon, pero gumagana pa rin ang search at routing.",
                    "Tuki AI is temporarily unavailable, but search and routing still work normally."),
                ConversationId: conversation.Context.ConversationId == Guid.Empty
                    ? null
                    : conversation.Context.ConversationId,
                Surface: SurfaceName(AssistantSurface.Planning));
        }

        _telemetry.Event("AIIntentParsed", outcome: $"Planning:{intent.Intent}");

        AssistantResponse response = intent.Intent switch
        {
            AssistantIntentType.PlanRoute or
            AssistantIntentType.UpdateTripConstraints or
            AssistantIntentType.ChangeDestination =>
                await PlanAsync(
                    userId, intent, request, language, conversation.Context, cancellationToken),

            AssistantIntentType.SearchPlace =>
                await SearchPlaceAsync(intent, request, language, cancellationToken),

            AssistantIntentType.StartNavigation => new(
                "ACTION_REQUIRED",
                Text(
                    language,
                    "Pumili muna tayo ng route card bago simulan ang navigation.",
                    "Choose a route card before starting navigation."),
                Action: new AssistantAction("SELECT_ROUTE", true)),

            AssistantIntentType.GeneralChat => new(
                "GENERAL_CHAT",
                Text(
                    language,
                    "Game! Pwede kitang tulungan maghanap at mag-fine-tune ng commute route.",
                    "Sure. I can help you find and fine-tune a commute route.")),

            AssistantIntentType.CancelTrip => new(
                "NO_ACTIVE_TRIP",
                Text(
                    language,
                    "Wala tayong active trip sa planning screen. Pumili muna tayo ng route kung biyahe ang gusto mong ayusin.",
                    "There is no active trip on the planning screen. Choose a route first if you want to manage a journey.")),

            _ => new(
                "CLARIFICATION_REQUIRED",
                Text(
                    language,
                    "Saan tayo pupunta, o ano yung gusto mong baguhin sa route?",
                    "Where are we headed, or what would you like to change about the route?"))
        };

        response = WithMetadata(response, conversation.Context.ConversationId, AssistantSurface.Planning);
        await PersistConversationAsync(
            conversation.Context.ConversationId,
            normalizedMessage,
            response,
            intent,
            cancellationToken);
        return response;
    }

    public async Task<AssistantResponse> RespondActiveTripAsync(
        Guid userId,
        Guid tripSessionId,
        ActiveTripAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("AI.ActiveTrip");
        if (string.IsNullOrWhiteSpace(request.Message))
            return new("INVALID_REQUEST", "Message cannot be empty.", Surface: SurfaceName(AssistantSurface.ActiveTrip));

        var language = await ResolveLanguageAsync(userId, cancellationToken);
        var session = await sessions.GetOwnedAsync(tripSessionId, userId, cancellationToken);
        if (session is null)
            return new(
                "NO_ACTIVE_TRIP",
                Text(language, "Hindi ko mahanap yung active trip na yun.", "I couldn't find that active trip."),
                Surface: SurfaceName(AssistantSurface.ActiveTrip));

        if (session.CurrentNavigationState is TripNavigationState.Arrived or TripNavigationState.Cancelled)
            return new(
                "TRIP_NOT_ACTIVE",
                Text(language, "Tapos na yung trip na yun.", "That trip is no longer active."),
                Surface: SurfaceName(AssistantSurface.ActiveTrip));

        var conversation = await ResolveConversationAsync(
            userId, request.ConversationId, "Tuki active trip", cancellationToken);
        if (conversation.Error is not null)
            return new(
                conversation.Error,
                Text(language, "Hindi valid yung conversation na binuksan.", "That conversation is not available."),
                Surface: SurfaceName(AssistantSurface.ActiveTrip));

        var tripContext = await BuildActiveTripContextAsync(
            userId, session, cancellationToken);
        var normalizedMessage = request.Message.Trim();

        if (IsGreeting(normalizedMessage))
        {
            var greeting = WithMetadata(
                new AssistantResponse(
                    "GREETING",
                    Text(
                        language,
                        "Nandito lang ako. Tanong ka tungkol sa trip natin o sabihin mo kung may gusto kang baguhin.",
                        "I'm here. Ask about this trip or tell me if you want to change something."),
                    Navigation: NavigationState(tripContext)),
                conversation.Context.ConversationId,
                AssistantSurface.ActiveTrip);
            await PersistConversationAsync(
                conversation.Context.ConversationId,
                normalizedMessage,
                greeting,
                null,
                cancellationToken);
            return greeting;
        }

        var context = new AssistantContext(
            AssistantSurface.ActiveTrip,
            normalizedMessage,
            conversation.Context,
            tripContext);

        AssistantIntent intent;
        var deterministicIntent = NavigationIntent(normalizedMessage);
        try
        {
            using (_telemetry.Measure("AI.Intent"))
                intent = deterministicIntent ?? await intentExtractor.ExtractAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Active-trip assistant intent extraction failed");
            _telemetry.Event("AIResponseFailed", tripSessionId, "ActiveTrip");
            return WithMetadata(
                new AssistantResponse(
                    "AI_UNAVAILABLE",
                    Text(
                        language,
                        "Temporary unavailable si Tuki AI ngayon, pero tuloy pa rin ang navigation natin.",
                        "Tuki AI is temporarily unavailable, but your navigation is still running."),
                    Navigation: NavigationState(tripContext)),
                conversation.Context.ConversationId,
                AssistantSurface.ActiveTrip);
        }

        _telemetry.Event("AIIntentParsed", tripSessionId, $"ActiveTrip:{intent.Intent}");

        AssistantResponse response = intent.Intent switch
        {
            AssistantIntentType.NavigationQuestion or AssistantIntentType.Lost =>
                NavigationStatus(language, tripContext),

            AssistantIntentType.ExplainRoute =>
                await ExplainRouteAsync(language, session, tripContext, cancellationToken),

            AssistantIntentType.UpdateTripConstraints =>
                await PreviewActiveTripReplanAsync(
                    userId,
                    session,
                    tripContext,
                    intent,
                    destination: null,
                    language,
                    cancellationToken),

            AssistantIntentType.ChangeDestination =>
                await PreviewDestinationChangeAsync(
                    userId,
                    session,
                    tripContext,
                    intent,
                    request,
                    language,
                    cancellationToken),

            AssistantIntentType.PlanRoute when !string.IsNullOrWhiteSpace(intent.DestinationQuery) =>
                await PreviewDestinationChangeAsync(
                    userId,
                    session,
                    tripContext,
                    intent,
                    request,
                    language,
                    cancellationToken),

            AssistantIntentType.CancelTrip => new(
                "ACTION_REQUIRED",
                Text(
                    language,
                    "I-confirm mo muna sa End Trip action bago natin ihinto yung navigation.",
                    "Use the End Trip confirmation before stopping navigation."),
                Navigation: NavigationState(tripContext),
                Action: new AssistantAction("CANCEL_TRIP", true, tripSessionId)),

            AssistantIntentType.ConfirmAction => new(
                "ACTION_REQUIRED",
                Text(
                    language,
                    "Kung route proposal yung kino-confirm mo, piliin yung exact route card para siguradong yun ang ia-apply natin.",
                    "If you're confirming a route proposal, choose the exact route card so we apply the route you actually selected."),
                Navigation: NavigationState(tripContext)),

            AssistantIntentType.RejectAction => new(
                "NO_CHANGE",
                Text(language, "Okay, hindi natin babaguhin yung current route.", "Okay, we'll keep the current route."),
                Navigation: NavigationState(tripContext)),

            AssistantIntentType.GeneralChat => new(
                "GENERAL_CHAT",
                Text(
                    language,
                    "Nandito lang ako habang bumibiyahe tayo. Pwede mong itanong yung next step, babaan, gastos, o sabihin kung may constraint na nagbago.",
                    "I'm here during the trip. Ask about the next step, your stop, remaining cost, or tell me if a constraint changed."),
                Navigation: NavigationState(tripContext)),

            _ => new(
                "CLARIFICATION_REQUIRED",
                Text(
                    language,
                    "Ano yung gusto mong malaman o baguhin sa active trip natin?",
                    "What would you like to know or change about this active trip?"),
                Navigation: NavigationState(tripContext))
        };

        response = WithMetadata(response, conversation.Context.ConversationId, AssistantSurface.ActiveTrip);
        await PersistConversationAsync(
            conversation.Context.ConversationId,
            normalizedMessage,
            response,
            intent,
            cancellationToken);
        return response;
    }

    private async Task<AssistantResponse> PlanAsync(
        Guid userId,
        AssistantIntent intent,
        AssistantRequest request,
        string language,
        AssistantConversationContext conversation,
        CancellationToken cancellationToken)
    {
        var state = ApplyPlanningIntent(conversation.PlanningState, intent, request);
        var destination = state.Destination;

        // A new explicit place phrase supersedes the previously resolved
        // destination. Follow-up constraints intentionally leave it intact.
        var shouldResolveNewDestination = !string.IsNullOrWhiteSpace(intent.DestinationQuery) &&
            (state.Destination is null || intent.Intent == AssistantIntentType.ChangeDestination);
        if (shouldResolveNewDestination)
        {
            if (_assistantPlaces is null)
                return new(
                    "DESTINATION_SEARCH_UNAVAILABLE",
                    Text(language, "Temporary unavailable yung destination search.", "Destination search is temporarily unavailable."));

            IReadOnlyList<DestinationSearchResult> results;
            try
            {
                results = await _assistantPlaces.SearchAsync(
                    intent.DestinationQuery!,
                    new(state.OriginLatitude, state.OriginLongitude),
                    cancellationToken);
            }
            catch (DestinationProviderUnavailableException)
            {
                return new(
                    "DESTINATION_SEARCH_UNAVAILABLE",
                    Text(language, "Temporary unavailable yung destination search.", "Destination search is temporarily unavailable."));
            }

            if (results.Count == 0)
                return new(
                    "DESTINATION_NOT_FOUND",
                    Text(language, "Hindi ko mahanap yung destination na yun.", "I couldn't find that destination."));

            if (results.Count != 1)
            {
                var pending = CreatePendingDestinationResolution(results);
                state = state with { Destination = null, PendingDestination = pending };
                if (conversation.ConversationId != Guid.Empty &&
                    !await SavePlanningStateAsync(conversation.ConversationId, state, cancellationToken))
                    return ConversationStateUnavailable(language);

                return new(
                    "DESTINATION_AMBIGUOUS",
                    Text(
                        language,
                        $"May ilang results para sa {intent.DestinationQuery}. Alin dito yung gusto mong puntahan?",
                        $"I found a few results for {intent.DestinationQuery}. Which one is your destination?"),
                    Destinations: pending.Candidates.Select(ToCard).ToList(),
                    DestinationSelectionToken: pending.SelectionToken);
            }

            destination = ToResolvedDestination(results[0]);
            state = state with { Destination = destination, PendingDestination = null };
        }

        // Compatibility for existing conversations created before structured
        // planning state. New flows never re-search merely to process a card.
        if (destination is null && !string.IsNullOrWhiteSpace(conversation.LastDestinationQuery))
        {
            intent.DestinationQuery = conversation.LastDestinationQuery;
            return await PlanAsync(userId, intent, request, language,
                conversation with { PlanningState = state with { Destination = null } }, cancellationToken);
        }

        if (destination is null)
            return new(
                "CLARIFICATION_REQUIRED",
                Text(language, "Saan tayo pupunta?", "Where are we headed?"));

        if (state.OriginLatitude is not { } originLat ||
            state.OriginLongitude is not { } originLon)
        {
            return new(
                "ORIGIN_REQUIRED",
                Text(
                    language,
                    "Kailangan ko yung current location mo para makapag-compute ng route.",
                    "I need your current location to calculate the route."));
        }

        if (conversation.ConversationId != Guid.Empty &&
            !await SavePlanningStateAsync(conversation.ConversationId, state, cancellationToken))
            return ConversationStateUnavailable(language);

        return await PlanResolvedDestinationAsync(
            userId, originLat, originLon, destination, state, language, cancellationToken);
    }

    private async Task<AssistantResponse> PlanResolvedDestinationAsync(
        Guid userId,
        double originLat,
        double originLon,
        AssistantResolvedDestination destination,
        AssistantPlanningState state,
        string language,
        CancellationToken cancellationToken)
    {
        var preferences = ToRoutingPreferences(state);

        List<JeepneyTripPlan> plans;
        try
        {
            plans = preferences is null
                ? await routing.PlanTripsAsync(
                    originLat,
                    originLon,
                    destination.Latitude,
                    destination.Longitude,
                    cancellationToken)
                : await routing.PlanTripsAsync(
                    originLat,
                    originLon,
                    destination.Latitude,
                    destination.Longitude,
                    preferences,
                    cancellationToken);
        }
        catch (RoutingValidationException exception)
        {
            return new(exception.ErrorCode, exception.Message);
        }

        // Routing receives preferences before candidate confirmation and
        // objective selection. This final pass is only a defensive hard-limit
        // check over authoritative totals, not a second recommendation engine.
        var eligiblePlans = FilterPlansAgainstHardConstraints(
            plans,
            state.MaxFarePesos,
            state.MaxWalkingMeters,
            state.AvoidTransportModes ?? []);

        if (eligiblePlans.Count == 0)
            return NoPlansWithinConstraints(language, state.MaxFarePesos);

        if (routing is IJourneyGeometryEnricher geometryEnricher)
            await geometryEnricher.EnrichSelectedPlanGeometryAsync(eligiblePlans, cancellationToken);

        IReadOnlyList<PersistedJourney> persisted;
        try
        {
            persisted = await persistence.PersistAsync(
                userId,
                originLat,
                originLon,
                destination.Name,
                destination.Latitude,
                destination.Longitude,
                state.MaxFarePesos,
                state.OptimizationPreference,
                eligiblePlans,
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

        var journeys = persisted
            .Select(item => Map(item.Recommendation.RecommendationId, item.Plan))
            .ToList();

        return new(
            "JOURNEYS_AVAILABLE",
            Text(
                language,
                $"Ayun! May {journeys.Count} route options tayo papuntang {destination.Name}.",
                $"Got it! We have {journeys.Count} route options to {destination.Name}."),
            Journeys: journeys,
            Destination: ToCard(destination),
            Action: new AssistantAction(
                "SELECT_ROUTE",
                true,
                BudgetPesos: state.MaxFarePesos,
                Preference: state.OptimizationPreference,
                MaxWalkingMeters: state.MaxWalkingMeters,
                AvoidTransportModes: state.AvoidTransportModes));
    }

    private async Task<AssistantResponse> ContinuePendingDestinationSelectionAsync(
        Guid userId,
        AssistantRequest request,
        string language,
        AssistantConversationContext conversation,
        CancellationToken cancellationToken)
    {
        var pending = conversation.PlanningState?.PendingDestination;
        if (pending is null ||
            !string.Equals(pending.SelectionToken, request.DestinationSelectionToken,
                StringComparison.Ordinal) ||
            pending.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new(
                "DESTINATION_SELECTION_EXPIRED",
                Text(
                    language,
                    "Nag-expire na yung destination choices. Hanapin natin ulit yung place.",
                    "Those destination choices expired. Please search for the place again."));
        }

        var selected = pending.Candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.CandidateId, request.SelectedDestinationCandidateId,
                StringComparison.Ordinal));
        if (selected is null)
        {
            return new(
                "DESTINATION_SELECTION_INVALID",
                Text(
                    language,
                    "Hindi kasama sa pending choices yung napiling destination.",
                    "That destination is not one of the pending choices."));
        }

        var state = conversation.PlanningState! with
        {
            Destination = new AssistantResolvedDestination(
                selected.ProviderId,
                selected.Name,
                selected.Latitude,
                selected.Longitude,
                selected.Category,
                selected.Address),
            PendingDestination = null
        };
        if (!await SavePlanningStateAsync(conversation.ConversationId, state, cancellationToken))
            return ConversationStateUnavailable(language);

        if (state.OriginLatitude is not { } originLatitude ||
            state.OriginLongitude is not { } originLongitude)
        {
            return new(
                "ORIGIN_REQUIRED",
                Text(
                    language,
                    "Kailangan ko yung current location mo para makapag-compute ng route.",
                    "I need your current location to calculate the route."));
        }

        // This path deliberately has no intent extraction and no text search:
        // the stored candidate is the authoritative routing target.
        return await PlanResolvedDestinationAsync(
            userId,
            originLatitude,
            originLongitude,
            state.Destination,
            state,
            language,
            cancellationToken);
    }

    private static bool IsDestinationSelectionRequest(AssistantRequest request) =>
        !string.IsNullOrWhiteSpace(request.DestinationSelectionToken) ||
        !string.IsNullOrWhiteSpace(request.SelectedDestinationCandidateId);

    private static AssistantPlanningState ApplyPlanningIntent(
        AssistantPlanningState? existing,
        AssistantIntent intent,
        AssistantRequest request)
    {
        var state = existing ?? new AssistantPlanningState();
        var hasCompleteOrigin = request.OriginLatitude.HasValue &&
            request.OriginLongitude.HasValue;
        var modes = new HashSet<string>(
            state.AvoidTransportModes ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var mode in intent.AvoidTransportModes)
            modes.Add(mode);

        return state with
        {
            MaxFarePesos = intent.BudgetPesos ?? state.MaxFarePesos,
            OptimizationPreference = intent.Preference ?? state.OptimizationPreference,
            MaxWalkingMeters = intent.MaxWalkingMeters ?? state.MaxWalkingMeters,
            WalkingPreference = intent.WalkingPreference ?? state.WalkingPreference,
            AvoidTransportModes = modes.OrderBy(mode => mode, StringComparer.Ordinal).ToList(),
            OriginLatitude = hasCompleteOrigin
                ? request.OriginLatitude
                : state.OriginLatitude,
            OriginLongitude = hasCompleteOrigin
                ? request.OriginLongitude
                : state.OriginLongitude
        };
    }

    private static AssistantPendingDestinationResolution CreatePendingDestinationResolution(
        IReadOnlyList<DestinationSearchResult> results) =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.Add(PendingDestinationLifetime),
            results.Select(result => new AssistantPendingDestinationCandidate(
                Guid.NewGuid().ToString("N"),
                result.Id,
                result.Name,
                result.Latitude,
                result.Longitude,
                result.Category,
                result.Address)).ToList());

    private static AssistantResolvedDestination ToResolvedDestination(
        DestinationSearchResult result) =>
        new(result.Id, result.Name, result.Latitude, result.Longitude,
            result.Category, result.Address);

    private static AssistantDestinationCandidate ToCard(
        AssistantPendingDestinationCandidate candidate) =>
        new(candidate.CandidateId, candidate.Name, candidate.Latitude,
            candidate.Longitude, candidate.Category, candidate.Address);

    private static AssistantDestinationCandidate ToCard(
        AssistantResolvedDestination destination) =>
        new("resolved", destination.Name, destination.Latitude,
            destination.Longitude, destination.Category, destination.Address);

    private static AssistantDestinationCandidate ToCard(
        DestinationSearchResult destination) =>
        new(Guid.NewGuid().ToString("N"), destination.Name, destination.Latitude,
            destination.Longitude, destination.Category, destination.Address);

    private static JourneyPlanningPreferences? ToRoutingPreferences(
        AssistantPlanningState state)
    {
        var avoided = (state.AvoidTransportModes ?? [])
            .Select(ToAccessMode)
            .Where(mode => mode is not null)
            .Select(mode => mode!.Value)
            .ToHashSet();
        var preferences = new JourneyPlanningPreferences(
            state.MaxFarePesos,
            state.MaxWalkingMeters,
            state.WalkingPreference switch
            {
                AssistantWalkingPreference.Less => JourneyWalkingPreference.Less,
                AssistantWalkingPreference.More => JourneyWalkingPreference.More,
                _ => JourneyWalkingPreference.Normal
            },
            state.OptimizationPreference?.ToLowerInvariant() switch
            {
                "fastest" => JourneyOptimizationPreference.Fastest,
                "cheapest" => JourneyOptimizationPreference.Cheapest,
                "efficient" => JourneyOptimizationPreference.Efficient,
                _ => null
            },
            avoided);
        return preferences.MaxFarePesos is null &&
               preferences.MaxWalkingMeters is null &&
               preferences.OptimizationPreference is null &&
               preferences.WalkingPreference == JourneyWalkingPreference.Normal &&
               (preferences.AvoidTransportModes?.Count ?? 0) == 0
            ? null
            : preferences;
    }

    private static AccessMode? ToAccessMode(string mode) =>
        mode.ToUpperInvariant() switch
        {
            "WALK" => AccessMode.Walk,
            "TRICYCLE" => AccessMode.Trike,
            "JEEPNEY" => AccessMode.Jeepney,
            _ => null
        };

    private async Task<bool> SavePlanningStateAsync(
        Guid conversationId,
        AssistantPlanningState state,
        CancellationToken cancellationToken)
    {
        if (_chat is null || conversationId == Guid.Empty)
            return false;

        return await _chat.UpdatePlanningStateAsync(
            conversationId,
            JsonSerializer.Serialize(state, PlanningStateJsonOptions),
            cancellationToken);
    }

    private static AssistantResponse ConversationStateUnavailable(string language) =>
        new(
            "CONVERSATION_STATE_UNAVAILABLE",
            Text(
                language,
                "Hindi ko ma-save yung destination choices ngayon. Subukan ulit mamaya.",
                "I couldn't save the destination choices right now. Please try again."));

    private async Task<AssistantResponse> SearchPlaceAsync(
        AssistantIntent intent,
        AssistantRequest request,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.DestinationQuery))
            return new(
                "CLARIFICATION_REQUIRED",
                Text(language, "Anong lugar yung hinahanap mo?", "What place are you looking for?"));

        if (_assistantPlaces is null)
            return new(
                "DESTINATION_SEARCH_UNAVAILABLE",
                Text(language, "Temporary unavailable yung place search.", "Place search is temporarily unavailable."));

        IReadOnlyList<DestinationSearchResult> results;
        try
        {
            results = await _assistantPlaces.SearchAsync(
                intent.DestinationQuery,
                new(request.OriginLatitude, request.OriginLongitude),
                cancellationToken);
        }
        catch (DestinationProviderUnavailableException)
        {
            return new(
                "DESTINATION_SEARCH_UNAVAILABLE",
                Text(language, "Temporary unavailable yung place search.", "Place search is temporarily unavailable."));
        }

        if (results.Count == 0)
            return new(
                "DESTINATION_NOT_FOUND",
                Text(language, "Wala akong mahanap na matching place.", "I couldn't find a matching place."));

        return new(
            "PLACE_RESULTS",
            Text(
                language,
                $"May {results.Count} matching places akong nahanap.",
                $"I found {results.Count} matching places."),
            Destinations: results.Select(ToCard).ToList());
    }

    private async Task<AssistantResponse> PreviewDestinationChangeAsync(
        Guid userId,
        TripSession session,
        AssistantActiveTripContext tripContext,
        AssistantIntent intent,
        ActiveTripAssistantRequest request,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.DestinationQuery))
            return new(
                "CLARIFICATION_REQUIRED",
                Text(
                    language,
                    "Saan mo gustong palitan yung destination natin?",
                    "What do you want to change the destination to?"),
                Navigation: NavigationState(tripContext));

        var resolved = await destinationSearch.SearchAsync(
            intent.DestinationQuery,
            new(tripContext.LastLatitude, tripContext.LastLongitude),
            cancellationToken);

        if (resolved.Error is not null)
            return new(
                resolved.Error,
                resolved.Message ??
                Text(language, "Hindi nag-work yung destination search.", "Destination search failed."),
                Navigation: NavigationState(tripContext));

        if (resolved.Results.Count == 0)
            return new(
                "DESTINATION_NOT_FOUND",
                Text(language, "Hindi ko mahanap yung destination na yun.", "I couldn't find that destination."),
                Navigation: NavigationState(tripContext));

        var destination = ResolveDestination(
            resolved.Results,
            intent.DestinationQuery,
            request.DestinationId);

        if (destination is null)
            return new(
                "DESTINATION_AMBIGUOUS",
                Text(
                    language,
                    $"May ilang results para sa {intent.DestinationQuery}. Alin dito yung bagong destination?",
                    $"I found a few results for {intent.DestinationQuery}. Which one is the new destination?"),
                Destinations: resolved.Results.Select(ToCard).ToList(),
                Navigation: NavigationState(tripContext));

        return await PreviewActiveTripReplanAsync(
            userId,
            session,
            tripContext,
            intent,
            destination,
            language,
            cancellationToken);
    }

    private async Task<AssistantResponse> PreviewActiveTripReplanAsync(
        Guid userId,
        TripSession session,
        AssistantActiveTripContext tripContext,
        AssistantIntent intent,
        DestinationSearchResult? destination,
        string language,
        CancellationToken cancellationToken)
    {
        if (session.LastLatitude is not { } originLat ||
            session.LastLongitude is not { } originLon)
        {
            return new(
                "NO_RELIABLE_LOCATION",
                Text(
                    language,
                    "Wala pa akong reliable live location para gumawa ng bagong route proposal. Tuloy muna yung current navigation.",
                    "I don't have a reliable live location yet for a new route proposal. Your current navigation stays unchanged."),
                Navigation: NavigationState(tripContext));
        }

        var destinationName = destination?.Name ?? session.DestinationName ?? "Destination";
        var destinationLatitude = destination?.Latitude ?? session.DestinationLatitude;
        var destinationLongitude = destination?.Longitude ?? session.DestinationLongitude;

        var remainingOriginalBudget = session.OriginalBudget is { } originalBudget
            ? Math.Max(0, originalBudget - session.ApproxFareSpent)
            : (decimal?)null;
        var budget = intent.BudgetPesos ?? remainingOriginalBudget;
        var preference = intent.Preference ?? session.OriginalPreference;
        var routingPreferences = ToRoutingPreferences(new AssistantPlanningState(
            MaxFarePesos: budget,
            OptimizationPreference: preference,
            MaxWalkingMeters: intent.MaxWalkingMeters,
            WalkingPreference: intent.WalkingPreference ?? AssistantWalkingPreference.Normal,
            AvoidTransportModes: intent.AvoidTransportModes));

        List<JeepneyTripPlan> plans;
        try
        {
            plans = await routing.PlanTripsAsync(
                originLat,
                originLon,
                destinationLatitude,
                destinationLongitude,
                routingPreferences,
                cancellationToken);
        }
        catch (RoutingValidationException exception)
        {
            return new(
                exception.ErrorCode,
                exception.Message,
                Navigation: NavigationState(tripContext));
        }

        var eligiblePlans = FilterAndOrderPlans(
            plans,
            budget,
            preference,
            intent.MaxWalkingMeters,
            intent.AvoidTransportModes);

        if (eligiblePlans.Count == 0)
        {
            var none = NoPlansWithinConstraints(language, budget);
            return none with { Navigation = NavigationState(tripContext) };
        }

        if (routing is IJourneyGeometryEnricher geometryEnricher)
            await geometryEnricher.EnrichSelectedPlanGeometryAsync(eligiblePlans, cancellationToken);

        IReadOnlyList<PersistedJourney> persisted;
        try
        {
            persisted = await persistence.PersistAsync(
                userId,
                originLat,
                originLon,
                destinationName,
                destinationLatitude,
                destinationLongitude,
                budget,
                preference,
                eligiblePlans,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist active-trip replan proposals");
            return new(
                "JOURNEY_PERSISTENCE_FAILED",
                Text(
                    language,
                    "Nakuha ko yung possible routes pero hindi ko ma-save yung proposal ngayon. Hindi ko binago yung current trip.",
                    "I calculated possible routes but couldn't save the proposal. Your current trip was not changed."),
                Navigation: NavigationState(tripContext));
        }

        var journeys = persisted
            .Select(item => Map(item.Recommendation.RecommendationId, item.Plan))
            .ToList();

        var destinationResult = destination ?? new DestinationSearchResult(
            $"trip:{session.TripSessionId}",
            destinationName,
            destinationLatitude,
            destinationLongitude,
            "destination",
            "trip");

        return new(
            "REPLAN_PROPOSAL",
            Text(
                language,
                $"May {journeys.Count} updated route option{(journeys.Count == 1 ? string.Empty : "s")} ako. Piliin mo muna yung gusto mo bago natin palitan yung active route.",
                $"I found {journeys.Count} updated route option{(journeys.Count == 1 ? string.Empty : "s")}. Choose one before we replace the active route."),
            Journeys: journeys,
            Navigation: NavigationState(tripContext),
            Destination: ToCard(destinationResult),
            Action: new AssistantAction(
                "CONFIRM_REPLAN_ROUTE",
                true,
                session.TripSessionId,
                budget,
                preference,
                intent.MaxWalkingMeters,
                intent.AvoidTransportModes));
    }

    private AssistantResponse NavigationStatus(
        string language,
        AssistantActiveTripContext context)
    {
        var status = context.NavigationStatus ??
            (context.NavigationState == TripNavigationState.OffRoute.ToString()
                ? "OFF_ROUTE"
                : "ON_ROUTE");

        var message = status switch
        {
            "MISSED_ALIGHT" => Text(
                language,
                "Mukhang lumagpas tayo sa babaan. Pwede tayong gumawa ng bagong route proposal mula sa current location.",
                "Looks like we passed the planned stop. We can make a new route proposal from your current location."),

            "OFF_ROUTE" => Text(
                language,
                "Mukhang lumalayo tayo sa planned route. Yung backend navigation ang bahala sa recovery/reroute.",
                "We're moving away from the planned route. The backend navigation will handle recovery/rerouting."),

            "UNCERTAIN_GPS" => Text(
                language,
                "Hindi sapat yung GPS accuracy ngayon para masabi nang sigurado kung nasa route pa tayo.",
                "Your GPS isn't accurate enough right now to say confidently whether you're still on route."),

            _ when context.NextInstruction is not null && context.RemainingDistanceMeters is { } remaining =>
                Text(
                    language,
                    $"Nasa active trip pa tayo. Next: {context.NextInstruction} Mga {FormatDistance(remaining)} pa sa current leg.",
                    $"You're still on the active trip. Next: {context.NextInstruction} About {FormatDistance(remaining)} remains on this leg."),

            _ when context.NextInstruction is not null =>
                Text(
                    language,
                    $"Nasa active trip pa tayo. Next: {context.NextInstruction}",
                    $"You're still on the active trip. Next: {context.NextInstruction}"),

            _ => Text(
                language,
                "Nasa active trip pa tayo at wala akong bagong navigation event ngayon.",
                "You're still on the active trip and there is no new navigation event right now.")
        };

        return new(
            status,
            message,
            Navigation: NavigationState(context));
    }

    private async Task<AssistantResponse> ExplainRouteAsync(
        string language,
        TripSession session,
        AssistantActiveTripContext tripContext,
        CancellationToken cancellationToken)
    {
        if (_recommendations is null)
        {
            return new(
                "ROUTE_EXPLANATION",
                Text(
                    language,
                    "Yung current route natin galing sa deterministic routing engine; hindi si Gemini ang nag-compute ng route.",
                    "Your current route was calculated by the deterministic routing engine; Gemini did not compute it."),
                Navigation: NavigationState(tripContext));
        }

        var recommendation = await _recommendations.GetByIdAsync(
            session.RecommendationId,
            cancellationToken);

        if (recommendation is null)
            return new(
                "ROUTE_EXPLANATION_UNAVAILABLE",
                Text(
                    language,
                    "Hindi ko ma-load yung saved recommendation details ngayon.",
                    "I couldn't load the saved recommendation details right now."),
                Navigation: NavigationState(tripContext));

        var objective = FriendlyObjective(recommendation.RecommendationType, language);
        var explanation = Text(
            language,
            $"Pinili ng routing engine yung route na ito bilang {objective}: humigit-kumulang ₱{recommendation.TotalFare:0.##}, {recommendation.TotalMinutes:0.#} min, {recommendation.WalkingDistanceMeters:0}m na lakad, at {recommendation.TransferCount} transfer{(recommendation.TransferCount == 1 ? string.Empty : "s")}.",
            $"The routing engine selected this as the {objective} option: about ₱{recommendation.TotalFare:0.##}, {recommendation.TotalMinutes:0.#} min, {recommendation.WalkingDistanceMeters:0}m walking, and {recommendation.TransferCount} transfer{(recommendation.TransferCount == 1 ? string.Empty : "s")}.");

        return new(
            "ROUTE_EXPLANATION",
            explanation,
            Navigation: NavigationState(tripContext));
    }

    private async Task<AssistantActiveTripContext> BuildActiveTripContextAsync(
        Guid userId,
        TripSession session,
        CancellationToken cancellationToken)
    {
        List<RecommendationLeg> legs = [];
        if (_recommendations is not null)
        {
            legs = await _recommendations.GetOrderedLegsAsync(
                session.RecommendationId,
                cancellationToken);
        }

        var currentLeg = legs.FirstOrDefault(item => item.LegOrder == session.CurrentLegIndex);
        var nextInstruction = (await instructions.GetForOwnedSessionAsync(
                session.TripSessionId,
                userId,
                cancellationToken))
            .Where(item => item.Audience == NavigationInstructionAudience.Passenger)
            .FirstOrDefault(item =>
                item.LegIndex > session.CurrentLegIndex ||
                (item.LegIndex == session.CurrentLegIndex &&
                 (item.DistanceFromLegStartMeters is null ||
                  item.DistanceFromLegStartMeters >= session.CurrentProgressMeters)))
            ?.Text;

        var remainingDistance = currentLeg is null
            ? null
            : NavigationTripRules.RemainingMeters(session, currentLeg);

        var remainingFare = legs.Count == 0
            ? 0m
            : NavigationTripRules.EstimatedRemainingFare(session, legs);

        return new AssistantActiveTripContext(
            session.TripSessionId,
            session.CurrentNavigationState.ToString(),
            session.LastNavigationStatus,
            session.DestinationName,
            session.DestinationLatitude,
            session.DestinationLongitude,
            session.CurrentLegIndex,
            currentLeg?.TransportMode?.Code,
            currentLeg?.Route?.RouteName ?? currentLeg?.Instructions,
            remainingDistance,
            session.ApproxFareSpent,
            remainingFare,
            session.OriginalBudget,
            session.OriginalPreference,
            nextInstruction,
            session.LastLatitude,
            session.LastLongitude,
            session.LastAccuracyMeters,
            session.LastLocationAt);
    }

    private async Task<(AssistantConversationContext Context, string? Error)> ResolveConversationAsync(
        Guid userId,
        Guid? requestedConversationId,
        string defaultTitle,
        CancellationToken cancellationToken)
    {
        if (_chat is null)
            return (new AssistantConversationContext(Guid.Empty, null, null, []), null);

        Guid conversationId;
        ChatConversation? selectedConversation = null;
        if (requestedConversationId is { } requestedId)
        {
            var conversation = await _chat.GetConversationByIdAsync(requestedId, cancellationToken);
            if (conversation is null || conversation.UserId != userId)
                return (new AssistantConversationContext(Guid.Empty, null, null, []), "INVALID_CONVERSATION");
            conversationId = requestedId;
            selectedConversation = conversation;
        }
        else
        {
            var created = await _chat.CreateConversationAsync(
                userId,
                defaultTitle,
                cancellationToken);
            if (created is null)
                return (new AssistantConversationContext(Guid.Empty, null, null, []), "CONVERSATION_CREATE_FAILED");
            conversationId = created.ConversationId;
            selectedConversation = created;
        }

        var messages = await _chat.GetMessagesAsync(conversationId, cancellationToken);
        var recent = messages
            .OrderBy(item => item.CreatedAt)
            .TakeLast(RecentConversationTurnLimit)
            .Select(item => new AssistantConversationTurn(item.Sender, item.Message))
            .ToList();
        var lastDestination = messages
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.ExtractedDestination)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var lastBudget = messages
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.ExtractedBudget)
            .FirstOrDefault(value => value.HasValue);
        var planningState = DeserializePlanningState(selectedConversation?.PlanningStateJson);

        return (
            new AssistantConversationContext(
                conversationId,
                lastDestination,
                lastBudget,
                recent,
                planningState),
            null);
    }

    private static AssistantPlanningState? DeserializePlanningState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AssistantPlanningState>(
                json, PlanningStateJsonOptions);
        }
        catch (JsonException)
        {
            // A malformed legacy value must not crash planning. The next
            // successful planning turn replaces it with valid state.
            return null;
        }
    }

    private async Task PersistConversationAsync(
        Guid conversationId,
        string userMessage,
        AssistantResponse response,
        AssistantIntent? intent,
        CancellationToken cancellationToken)
    {
        if (_chat is null || conversationId == Guid.Empty)
            return;

        try
        {
            await _chat.AddMessageAsync(
                conversationId,
                "user",
                userMessage,
                intent?.BudgetPesos,
                intent?.OriginQuery,
                intent?.DestinationQuery,
                cancellationToken: cancellationToken);
            await _chat.AddMessageAsync(
                conversationId,
                "assistant",
                response.Message,
                intent?.BudgetPesos,
                intent?.OriginQuery,
                intent?.DestinationQuery,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not persist Tuki assistant conversation {ConversationId}",
                conversationId);
        }
    }

    private static AssistantResponse WithMetadata(
        AssistantResponse response,
        Guid conversationId,
        AssistantSurface surface) =>
        response with
        {
            ConversationId = conversationId == Guid.Empty ? null : conversationId,
            Surface = SurfaceName(surface)
        };

    private static AssistantNavigationState NavigationState(
        AssistantActiveTripContext context) =>
        new(
            context.TripSessionId,
            context.NavigationState,
            context.CurrentLegIndex,
            context.CurrentMode,
            context.CurrentRouteName,
            context.NextInstruction,
            context.RemainingDistanceMeters,
            context.ApproxFareSpent,
            context.EstimatedRemainingFare,
            context.NavigationStatus);

    private static List<JeepneyTripPlan> FilterAndOrderPlans(
        IEnumerable<JeepneyTripPlan> plans,
        decimal? budget,
        string? preference,
        double? maxWalkingMeters,
        IReadOnlyCollection<string> avoidModes)
    {
        IEnumerable<JeepneyTripPlan> eligible = FilterPlansAgainstHardConstraints(
            plans, budget, maxWalkingMeters, avoidModes);

        if (!string.IsNullOrWhiteSpace(preference))
        {
            eligible = eligible
                .OrderByDescending(plan =>
                    plan.RecommendationType
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(preference, StringComparer.OrdinalIgnoreCase))
                .ThenBy(plan => plan.GeneralizedCostPesos);
        }

        return eligible.ToList();
    }

    private static List<JeepneyTripPlan> FilterPlansAgainstHardConstraints(
        IEnumerable<JeepneyTripPlan> plans,
        decimal? budget,
        double? maxWalkingMeters,
        IReadOnlyCollection<string> avoidModes)
    {
        IEnumerable<JeepneyTripPlan> eligible = plans;

        if (budget is { } maxFare)
            eligible = eligible.Where(plan => (decimal)plan.TotalFarePesos <= maxFare);

        if (maxWalkingMeters is { } maxWalk)
            eligible = eligible.Where(plan => TotalWalkingMeters(plan) <= maxWalk);

        foreach (var mode in avoidModes)
            eligible = eligible.Where(plan => !UsesTransportMode(plan, mode));

        return eligible.ToList();
    }

    private static AssistantResponse NoPlansWithinConstraints(
        string language,
        decimal? budget) =>
        new(
            "NO_JOURNEY_WITHIN_CONSTRAINTS",
            budget is { } maxFare
                ? Text(
                    language,
                    $"Wala akong mahanap na Tuki route na pasok sa current constraints at ₱{maxFare:0.##}.",
                    $"I couldn't find a Tuki route that fits the current constraints and ₱{maxFare:0.##}.")
                : Text(
                    language,
                    "Wala akong mahanap na supported route na pasok sa current constraints.",
                    "I couldn't find a supported route that fits the current constraints."));

    private static DestinationSearchResult? ResolveDestination(
        IReadOnlyList<DestinationSearchResult> results,
        string query,
        string? selectedId)
    {
        if (!string.IsNullOrWhiteSpace(selectedId))
            return results.FirstOrDefault(item =>
                string.Equals(item.Id, selectedId, StringComparison.Ordinal));

        if (results.Count == 1)
            return results[0];

        var exactMatches = results
            .Where(item => string.Equals(
                item.Name.Trim(),
                query.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return exactMatches.Count == 1 ? exactMatches[0] : null;
    }

    private async Task<string> ResolveLanguageAsync(
        Guid userId,
        CancellationToken cancellationToken)
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
            logger.LogWarning(
                exception,
                "Could not load assistant language preference; defaulting to English");
            return TukiLanguage.English;
        }
    }

    private static bool IsGreeting(string message)
    {
        var normalized = message
            .Trim()
            .TrimEnd('!', '?', '.', ',')
            .ToLowerInvariant();

        return normalized is
            "hello" or "hi" or "hey" or "yo" or "yoo" or "uy" or
            "hello tuki" or "hi tuki" or "hey tuki" or "yo tuki" or
            "kumusta" or "kamusta" or "kumusta tuki" or "kamusta tuki";
    }

    private static AssistantIntent? NavigationIntent(string message)
    {
        var normalized = message.ToLowerInvariant();
        var navigationQuestion =
            normalized.Contains("right way", StringComparison.Ordinal) ||
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
            ? new AssistantIntent { Intent = AssistantIntentType.NavigationQuestion }
            : null;
    }

    private static double TotalWalkingMeters(JeepneyTripPlan plan) =>
        plan.Legs
            .Where(item => item.Mode == AccessMode.Walk)
            .Sum(item => item.DistanceMeters);

    private static bool UsesTransportMode(JeepneyTripPlan plan, string mode) =>
        mode.ToUpperInvariant() switch
        {
            "TRICYCLE" => plan.Legs.Any(item => item.Mode == AccessMode.Trike),
            "WALK" => plan.Legs.Any(item => item.Mode == AccessMode.Walk),
            "JEEPNEY" => plan.Legs.Any(item =>
                item.Mode != AccessMode.Walk &&
                item.Mode != AccessMode.Trike),
            _ => false
        };

    private static string FriendlyObjective(string recommendationType, string language)
    {
        var tags = recommendationType
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tags.Contains("efficient"))
            return TukiLanguage.IsFilipino(language) ? "best overall / balanced" : "best overall / balanced";
        if (tags.Contains("cheapest"))
            return TukiLanguage.IsFilipino(language) ? "pinakamura" : "cheapest";
        if (tags.Contains("fastest"))
            return TukiLanguage.IsFilipino(language) ? "pinakamabilis" : "fastest";
        return TukiLanguage.IsFilipino(language) ? "selected" : "selected";
    }

    private static string FormatDistance(double meters) =>
        meters >= 1_000
            ? $"{meters / 1_000d:0.#} km"
            : $"{Math.Max(0, Math.Round(meters / 10d) * 10):0}m";

    private static string SurfaceName(AssistantSurface surface) =>
        surface == AssistantSurface.ActiveTrip ? "ACTIVE_TRIP" : "PLANNING";

    private static string Text(string language, string filipino, string english) =>
        TukiLanguage.IsFilipino(language) ? filipino : english;

    private static AssistantJourney Map(Guid recommendationId, JeepneyTripPlan plan) =>
        new(
            recommendationId,
            plan.RecommendationType,
            plan.TotalFarePesos,
            plan.TotalTimeSeconds,
            plan.Legs
                .Where(leg => leg.Mode == AccessMode.Walk)
                .Sum(leg => leg.DistanceMeters),
            plan.Legs
                .Select(leg => new AssistantJourneyLeg(
                    leg.Mode.ToString(),
                    leg.RouteName))
                .ToList(),
            plan);
}
