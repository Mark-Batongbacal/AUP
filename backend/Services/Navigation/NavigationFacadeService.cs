using backend.Models.Database;
using backend.Repositories;
using backend.Services.TripSessions;
using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public interface INavigationFacadeService
{
    Task<NavigationOperation> StartAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> UpdateLocationAsync(Guid userId, Guid sessionId, LocationUpdate update, CancellationToken cancellationToken = default);
    Task<NavigationOperation> ConfirmBoardingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> ConfirmAlightingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> ResolveAlightStatusAsync(Guid userId, Guid sessionId, bool alreadyOff, CancellationToken cancellationToken = default);
    Task<NavigationOperation> CancelAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> RerouteAsync(Guid userId, Guid sessionId, NavigationRerouteRequest request, CancellationToken cancellationToken = default);
}

public sealed class NavigationFacadeService(
    ITripSessionService tripSessions,
    ITripSessionRepository sessions,
    IRouteRecommendationRepository recommendations,
    INavigationInstructionRepository instructions,
    ITripLandmarkCandidateRepository landmarks,
    ILocationTrackingService locationTracking,
    IReroutingService rerouting,
    INavigationSpeechService speech,
    IOptions<NavigationOptions> options,
    ILogger<NavigationFacadeService> logger) : INavigationFacadeService
{
    private readonly NavigationOptions _options = options.Value;

    public async Task<NavigationOperation> StartAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default)
    {
        var created = await tripSessions.CreateAsync(userId, new CreateTripSessionRequest(recommendationId), cancellationToken);
        if (!created.Succeeded) return Fail(created.Error!);
        var started = await tripSessions.StartAsync(userId, created.Session!.TripSessionId, cancellationToken);
        return started.Succeeded
            ? await BuildAsync(userId, started.Session!, "NAVIGATION_STARTED", [], cancellationToken)
            : Fail(started.Error!);
    }

    public async Task<NavigationOperation> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = await tripSessions.GetAsync(userId, sessionId, cancellationToken);
        return result.Succeeded
            ? await BuildAsync(userId, result.Session!, result.Session!.LastNavigationStatus ?? result.Session.CurrentNavigationState.ToString(), [], cancellationToken, allowDynamicSpeech: false)
            : Fail(result.Error!);
    }

    public async Task<NavigationOperation> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await tripSessions.GetActiveAsync(userId, cancellationToken);
        return active.Succeeded
            ? await BuildAsync(userId, active.Session!, active.Session!.LastNavigationStatus ?? "ACTIVE", [], cancellationToken, allowDynamicSpeech: false)
            : Fail(active.Error!);
    }

    public async Task<NavigationOperation> UpdateLocationAsync(Guid userId, Guid sessionId, LocationUpdate update, CancellationToken cancellationToken = default)
    {
        var result = await locationTracking.ProcessAsync(userId, sessionId, update, cancellationToken);
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return Fail("TRIP_SESSION_NOT_FOUND");
        return await BuildAsync(userId, session, result.Status, result.TriggeredInstructions ?? [], cancellationToken);
    }

    public async Task<NavigationOperation> ConfirmBoardingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId, await tripSessions.ConfirmBoardingAsync(userId, sessionId, cancellationToken), "BOARDING_CONFIRMED", cancellationToken);

    public async Task<NavigationOperation> ConfirmAlightingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId, await tripSessions.ConfirmAlightingAsync(userId, sessionId, cancellationToken), "ALIGHTING_CONFIRMED", cancellationToken);

    public async Task<NavigationOperation> ResolveAlightStatusAsync(
        Guid userId,
        Guid sessionId,
        bool alreadyOff,
        CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(
            userId,
            await tripSessions.ResolveAlightStatusAsync(userId, sessionId, alreadyOff, cancellationToken),
            alreadyOff ? "ALIGHTING_RECOVERED" : "MISSED_ALIGHT",
            cancellationToken);

    public async Task<NavigationOperation> CancelAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId, await tripSessions.CancelAsync(userId, sessionId, cancellationToken), "CANCELLED", cancellationToken);

    public async Task<NavigationOperation> RerouteAsync(Guid userId, Guid sessionId, NavigationRerouteRequest request, CancellationToken cancellationToken = default)
    {
        var result = await rerouting.RerouteAsync(userId, sessionId, request, cancellationToken);
        if (!result.Succeeded) return Fail(result.Status);
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        return session is null
            ? Fail("TRIP_SESSION_NOT_FOUND")
            : await BuildAsync(userId, session, result.Status, [], cancellationToken);
    }

    private async Task<NavigationOperation> FromSessionOperationAsync(Guid userId, TripSessionOperation operation, string status, CancellationToken cancellationToken) =>
        operation.Succeeded ? await BuildAsync(userId, operation.Session!, status, [], cancellationToken) : Fail(operation.Error!);

    private async Task<NavigationOperation> BuildAsync(
        Guid userId,
        TripSession session,
        string status,
        IReadOnlyList<NavigationInstruction> triggered,
        CancellationToken cancellationToken,
        bool allowDynamicSpeech = true)
    {
        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var leg = legs.FirstOrDefault(item => item.LegOrder == session.CurrentLegIndex);
        var allInstructions = await instructions.GetForOwnedSessionAsync(session.TripSessionId, userId, cancellationToken);
        var legLandmarks = leg is null
            ? new List<TripLandmarkCandidate>()
            : await landmarks.GetForLegAsync(session.TripSessionId, leg.LegOrder, cancellationToken);
        var legInstructions = leg is null
            ? new List<NavigationInstructionDetailSnapshot>()
            : allInstructions
                .Where(item => item.Audience == NavigationInstructionAudience.Passenger && item.LegIndex == leg.LegOrder)
                .OrderBy(item => item.Sequence)
                .Select(MapInstructionDetail)
                .ToList();
        var legLandmarkPackage = legLandmarks
            .Select(MapLandmark)
            .Where(item => item is not null)
            .Cast<NavigationLandmarkSnapshot>()
            .ToList();
        var boardLandmark = legLandmarks.FirstOrDefault(item => item.Role == LandmarkRole.BoardReference);
        var alightLandmark = legLandmarks.FirstOrDefault(item => item.Role == LandmarkRole.AlightReference);
        var progressLandmark = triggered.LastOrDefault(item => item.Type == NavigationInstructionType.LandmarkNotice);
        var selected = SelectInstruction(session, leg, allInstructions, triggered);
        var following = SelectFollowingInstruction(session, selected, allInstructions);
        var speechType = EventInstructionType(session, status, selected, progressLandmark);
        var instructionType = speechType is "OffRoute" or "MissedAlight" or "AlightStatusUnknown" or "Cancelled" or "Arrived"
            ? speechType
            : selected?.Type.ToString() ?? "Continue";
        var activeLandmark = progressLandmark is not null
            ? new NavigationLandmarkSnapshot(progressLandmark.Text.Replace("You just passed ", "", StringComparison.OrdinalIgnoreCase).TrimEnd('.'), "", "PROGRESS_REFERENCE", "ALONG_ROUTE", progressLandmark.Latitude ?? 0, progressLandmark.Longitude ?? 0, 0, progressLandmark.DistanceFromRouteStartMeters)
            : instructionType is "BoardJeepney" or "BoardTricycle" ? MapLandmark(boardLandmark)
            : instructionType is "PrepareToAlight" or "AlightJeepney" or "AlightTricycle" ? MapLandmark(alightLandmark) : null;
        var remaining = NavigationTripRules.RemainingMeters(session, leg);
        var routeName = leg?.Route?.RouteName ?? leg?.Instructions;
        var mode = leg?.TransportMode?.Code ?? "UNKNOWN";
        var structuredInstruction = new NavigationInstructionSnapshot(
            instructionType,
            routeName,
            mode,
            remaining,
            selected?.RequiresConfirmation ?? instructionType is "BoardJeepney" or "BoardTricycle" or "AlightJeepney" or "AlightTricycle",
            selected?.Text);
        var followingSnapshot = MapInstruction(following, legs);
        var eventKey = string.Join(':', session.RecommendationId, session.CurrentLegIndex, speechType, activeLandmark?.Name ?? "none");
        var sameEvent = session.LastSpeechEventKey == eventKey;
        var noNewMeaningfulEvent = speechType == "Continue" && status != "BOARDING_CONFIRMED" &&
            session.LastSpeechEventKey?.StartsWith($"{session.RecommendationId}:{session.CurrentLegIndex}:", StringComparison.Ordinal) == true;
        var useDynamicDistance = remaining.GetValueOrDefault() > 0 &&
            (speechType is "Continue" or "PrepareToAlight");
        var speechContext = new NavigationSpeechContext(
            speechType,
            session.CurrentNavigationState.ToString(),
            mode,
            routeName,
            activeLandmark?.Name,
            activeLandmark?.Role,
            activeLandmark?.Relation,
            remaining,
            status,
            useDynamicDistance);
        var spokenTemplate = (sameEvent || noNewMeaningfulEvent) && !string.IsNullOrWhiteSpace(session.LastSpokenInstruction)
            ? session.LastSpokenInstruction
            : allowDynamicSpeech
                ? await GenerateSpeechAsync(session, eventKey, speechContext, cancellationToken)
                : !string.IsNullOrWhiteSpace(session.LastSpokenInstruction)
                    ? session.LastSpokenInstruction
                    : DeterministicNavigationSpeech.Phrase(speechContext);
        spokenTemplate = NavigationSpeechTemplate.Normalize(spokenTemplate, speechContext);
        var spoken = NavigationSpeechTemplate.Render(spokenTemplate, remaining);

        var snapshot = new NavigationSnapshot(
            session.TripSessionId, session.CurrentNavigationState.ToString(), session.CurrentLegIndex, MapLeg(leg), structuredInstruction, spoken,
            remaining, session.CurrentProgressMeters,
            leg is null ? null : new NavigationStopInfo(routeName, leg.StartLatitude, leg.StartLongitude, MapLandmark(boardLandmark)),
            leg is null ? null : new NavigationStopInfo(routeName, leg.EndLatitude, leg.EndLongitude, MapLandmark(alightLandmark)),
            activeLandmark,
            session.CurrentNavigationState == TripNavigationState.WaitingToBoard,
            NavigationTripRules.CanConfirmAlighting(session, leg, _options),
            session.CurrentNavigationState == TripNavigationState.OffRoute,
            status, TriggeredEvents(triggered, speechType, status), session.LastLatitude, session.LastLongitude,
            session.ApproxFareSpent, NavigationTripRules.EstimatedRemainingFare(session, legs),
            followingSnapshot, BuildTripSummary(session, legs),
            SpokenInstructionTemplate: spokenTemplate,
            CurrentLegInstructions: legInstructions,
            CurrentLegLandmarks: legLandmarkPackage,
            RecommendationId: session.RecommendationId);
        return new(snapshot);
    }

    private async Task<string> GenerateSpeechAsync(TripSession session, string eventKey, NavigationSpeechContext context, CancellationToken cancellationToken)
    {
        string spoken;
        try { spoken = await speech.PhraseAsync(context, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "AI navigation speech unavailable for session {TripSessionId}; using fallback", session.TripSessionId);
            spoken = DeterministicNavigationSpeech.Phrase(context);
        }
        spoken = NavigationSpeechTemplate.Normalize(spoken, context);
        session.LastSpeechEventKey = eventKey;
        session.LastSpokenInstruction = spoken;
        session.UpdatedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(session, cancellationToken);
        return spoken;
    }

    private static NavigationInstruction? SelectInstruction(TripSession session, RecommendationLeg? leg, IReadOnlyList<NavigationInstruction> all, IReadOnlyList<NavigationInstruction> triggered)
    {
        if (triggered.Count > 0) return triggered[^1];
        var legInstructions = all.Where(item => item.Audience == NavigationInstructionAudience.Passenger && (leg is null || item.LegIndex == leg.LegOrder)).ToList();
        var desired = session.CurrentNavigationState switch
        {
            TripNavigationState.WaitingToBoard => new[] { NavigationInstructionType.BoardJeepney, NavigationInstructionType.BoardTricycle },
            TripNavigationState.ApproachingAlightPoint => new[] { NavigationInstructionType.PrepareToAlight },
            TripNavigationState.Arrived => new[] { NavigationInstructionType.Arrived },
            TripNavigationState.OnJeepney or TripNavigationState.OnTricycle => new[] { NavigationInstructionType.Continue },
            _ => Array.Empty<NavigationInstructionType>()
        };
        var instruction = legInstructions.FirstOrDefault(item => desired.Contains(item.Type)) ??
            legInstructions.FirstOrDefault(item => item.DistanceFromLegStartMeters is null || item.DistanceFromLegStartMeters >= session.CurrentProgressMeters);
        if (instruction is not null) return instruction;
        return session.CurrentNavigationState == TripNavigationState.Arrived ? all.FirstOrDefault(item => item.Type == NavigationInstructionType.Arrived) : null;
    }

    private static NavigationInstruction? SelectFollowingInstruction(
        TripSession session,
        NavigationInstruction? selected,
        IReadOnlyList<NavigationInstruction> all)
    {
        if (session.CurrentNavigationState is TripNavigationState.Arrived or TripNavigationState.Cancelled)
            return null;

        var passenger = all
            .Where(item => item.Audience == NavigationInstructionAudience.Passenger && item.Type != NavigationInstructionType.LandmarkNotice)
            .OrderBy(item => item.Sequence)
            .ToList();

        if (selected is not null)
            return passenger.FirstOrDefault(item => item.Sequence > selected.Sequence);

        return passenger.FirstOrDefault(item => item.LegIndex > session.CurrentLegIndex) ??
               passenger.FirstOrDefault(item => item.LegIndex == session.CurrentLegIndex);
    }

    private static NavigationInstructionSnapshot? MapInstruction(
        NavigationInstruction? instruction,
        IReadOnlyList<RecommendationLeg> legs)
    {
        if (instruction is null) return null;
        var instructionLeg = legs.FirstOrDefault(item => item.LegOrder == instruction.LegIndex);
        var routeName = instructionLeg?.Route?.RouteName ?? instructionLeg?.Instructions;
        var mode = instructionLeg?.TransportMode?.Code ?? "UNKNOWN";
        return new NavigationInstructionSnapshot(
            instruction.Type.ToString(), routeName, mode, null, instruction.RequiresConfirmation, instruction.Text);
    }

    private static NavigationInstructionDetailSnapshot MapInstructionDetail(NavigationInstruction instruction) => new(
        instruction.Sequence,
        instruction.Type.ToString(),
        instruction.LegIndex,
        instruction.Text,
        instruction.StreetName,
        instruction.Latitude,
        instruction.Longitude,
        instruction.DistanceFromLegStartMeters,
        instruction.TriggerDistanceMeters,
        instruction.RequiresConfirmation);

    private static NavigationTripSummarySnapshot? BuildTripSummary(
        TripSession session,
        IReadOnlyList<RecommendationLeg> legs)
    {
        if (session.CurrentNavigationState != TripNavigationState.Arrived) return null;

        int? durationMinutes = null;
        if (session.StartedAt is { } started && session.CompletedAt is { } completed && completed >= started)
            durationMinutes = Math.Max(0, (int)Math.Round((completed - started).TotalMinutes));

        var transitLegs = legs.Count(item =>
        {
            var mode = item.TransportMode?.Code?.ToUpperInvariant();
            return mode is "JEEPNEY" or "TRICYCLE" or "TRIKE";
        });

        return new NavigationTripSummarySnapshot(
            string.IsNullOrWhiteSpace(session.DestinationName) ? "Destination" : session.DestinationName,
            durationMinutes,
            session.ApproxFareSpent,
            transitLegs,
            Math.Max(0, transitLegs - 1));
    }

    private static string EventInstructionType(TripSession session, string status, NavigationInstruction? selected, NavigationInstruction? progress) => status switch
    {
        "MISSED_ALIGHT" => "MissedAlight",
        "ALIGHT_STATUS_UNKNOWN" => "AlightStatusUnknown",
        "OFF_ROUTE" => "OffRoute",
        "REROUTE_SUCCEEDED" => "Rerouted",
        _ when progress is not null => "LandmarkNotice",
        _ when session.CurrentNavigationState == TripNavigationState.Arrived => "Arrived",
        _ when session.CurrentNavigationState == TripNavigationState.Cancelled => "Cancelled",
        _ => selected?.Type.ToString() ?? "Continue"
    };

    private static NavigationLegSnapshot? MapLeg(RecommendationLeg? leg) => leg is null ? null : new(
        leg.LegOrder, leg.TransportMode?.Code ?? "UNKNOWN", leg.RouteId, leg.Route?.RouteName,
        leg.FromName, leg.ToName, leg.StartLatitude, leg.StartLongitude, leg.EndLatitude, leg.EndLongitude,
        (double?)leg.DistanceMeters, leg.EstimatedFare,
        leg.StartRouteProgressMeters, leg.EndRouteProgressMeters, leg.StartsAlreadyOnboard);

    private static NavigationLandmarkSnapshot? MapLandmark(TripLandmarkCandidate? item) => item is null ? null : new(
        item.Name,
        item.Category,
        Role(item.Role),
        Relation(item.Relation),
        item.Latitude,
        item.Longitude,
        item.DistanceFromTargetMeters,
        item.DistanceFromRouteStartMeters,
        item.TriggerBeforeMeters,
        item.TriggerAfterMeters);

    private static IReadOnlyList<NavigationTriggeredEvent> TriggeredEvents(IReadOnlyList<NavigationInstruction> triggered, string speechType, string status)
    {
        var events = triggered.Select(item => new NavigationTriggeredEvent(item.Type.ToString(),
            item.Type == NavigationInstructionType.LandmarkNotice
                ? item.Text.Replace("You just passed ", "", StringComparison.OrdinalIgnoreCase).TrimEnd('.') : null)).ToList();
        if (status is "MISSED_ALIGHT" or "ALIGHT_STATUS_UNKNOWN" or "OFF_ROUTE" or "REROUTE_SUCCEEDED" || speechType is "Arrived" or "Cancelled")
            events.Add(new NavigationTriggeredEvent(speechType));
        return events;
    }

    private static string Role(LandmarkRole role) => role switch
    {
        LandmarkRole.BoardReference => "BOARD_REFERENCE",
        LandmarkRole.AlightReference => "ALIGHT_REFERENCE",
        _ => "PROGRESS_REFERENCE"
    };

    private static string Relation(LandmarkRelation relation) => relation switch
    {
        LandmarkRelation.NearBoardPoint => "NEAR_BOARD_POINT",
        LandmarkRelation.BeforeAlight => "BEFORE_ALIGHT",
        _ => "ALONG_ROUTE"
    };

    private static NavigationOperation Fail(string error) => new(null, error);
}
