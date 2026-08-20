using backend.Models.Database;
using backend.Repositories;
using backend.Services.TripSessions;

namespace backend.Services.Navigation;

public interface INavigationFacadeService
{
    Task<NavigationOperation> StartAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> UpdateLocationAsync(Guid userId, Guid sessionId, LocationUpdate update, CancellationToken cancellationToken = default);
    Task<NavigationOperation> ConfirmBoardingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> ConfirmAlightingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> CancelAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<NavigationOperation> RerouteAsync(Guid userId, Guid sessionId, string reason, CancellationToken cancellationToken = default);
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
    ILogger<NavigationFacadeService> logger) : INavigationFacadeService
{
    public async Task<NavigationOperation> StartAsync(
        Guid userId, Guid recommendationId, CancellationToken cancellationToken = default)
    {
        var created = await tripSessions.CreateAsync(userId,
            new CreateTripSessionRequest(recommendationId), cancellationToken);
        if (!created.Succeeded) return Fail(created.Error!);
        var started = await tripSessions.StartAsync(userId,
            created.Session!.TripSessionId, cancellationToken);
        return started.Succeeded
            ? await BuildAsync(userId, started.Session!, "NAVIGATION_STARTED", [], cancellationToken)
            : Fail(started.Error!);
    }

    public async Task<NavigationOperation> GetActiveAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await tripSessions.GetActiveAsync(userId, cancellationToken);
        return active.Succeeded
            ? await BuildAsync(userId, active.Session!,
                active.Session!.LastNavigationStatus ?? "ACTIVE", [], cancellationToken)
            : Fail(active.Error!);
    }

    public async Task<NavigationOperation> UpdateLocationAsync(
        Guid userId, Guid sessionId, LocationUpdate update,
        CancellationToken cancellationToken = default)
    {
        var result = await locationTracking.ProcessAsync(userId, sessionId, update, cancellationToken);
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        if (session is null) return Fail("TRIP_SESSION_NOT_FOUND");

        var status = result.Status;
        if (result.Accepted && result.Status is "OFF_ROUTE" or "MISSED_ALIGHT")
        {
            var reroute = await rerouting.RerouteAsync(userId, sessionId, result.Status, cancellationToken);
            if (reroute.Succeeded)
            {
                status = "REROUTE_SUCCEEDED";
                session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken) ?? session;
            }
        }
        return await BuildAsync(userId, session, status,
            result.TriggeredInstructions ?? [], cancellationToken);
    }

    public async Task<NavigationOperation> ConfirmBoardingAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId,
            await tripSessions.ConfirmBoardingAsync(userId, sessionId, cancellationToken),
            "BOARDING_CONFIRMED", cancellationToken);

    public async Task<NavigationOperation> ConfirmAlightingAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId,
            await tripSessions.ConfirmAlightingAsync(userId, sessionId, cancellationToken),
            "ALIGHTING_CONFIRMED", cancellationToken);

    public async Task<NavigationOperation> CancelAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default) =>
        await FromSessionOperationAsync(userId,
            await tripSessions.CancelAsync(userId, sessionId, cancellationToken),
            "CANCELLED", cancellationToken);

    public async Task<NavigationOperation> RerouteAsync(
        Guid userId, Guid sessionId, string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await rerouting.RerouteAsync(userId, sessionId, reason, cancellationToken);
        var session = await sessions.GetOwnedAsync(sessionId, userId, cancellationToken);
        return session is null ? Fail("TRIP_SESSION_NOT_FOUND") :
            await BuildAsync(userId, session, result.Status, [], cancellationToken);
    }

    private async Task<NavigationOperation> FromSessionOperationAsync(
        Guid userId, TripSessionOperation operation, string status,
        CancellationToken cancellationToken) => operation.Succeeded
        ? await BuildAsync(userId, operation.Session!, status, [], cancellationToken)
        : Fail(operation.Error!);

    private async Task<NavigationOperation> BuildAsync(
        Guid userId, TripSession session, string status,
        IReadOnlyList<NavigationInstruction> triggered,
        CancellationToken cancellationToken)
    {
        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var leg = legs.FirstOrDefault(item => item.LegOrder == session.CurrentLegIndex);
        var allInstructions = await instructions.GetForOwnedSessionAsync(
            session.TripSessionId, userId, cancellationToken);
        var legLandmarks = leg is null ? [] : await landmarks.GetForLegAsync(
            session.TripSessionId, leg.LegOrder, cancellationToken);
        var boardLandmark = legLandmarks.FirstOrDefault(item => item.Role == LandmarkRole.BoardReference);
        var alightLandmark = legLandmarks.FirstOrDefault(item => item.Role == LandmarkRole.AlightReference);
        var progressLandmark = triggered.LastOrDefault(item =>
            item.Type == NavigationInstructionType.LandmarkNotice);

        var selected = SelectInstruction(session, leg, allInstructions, triggered);
        var speechType = EventInstructionType(session, status, selected, progressLandmark);
        var instructionType = speechType is "OffRoute" or "MissedAlight" or "Cancelled" or "Arrived"
            ? speechType
            : selected?.Type.ToString() ?? "Continue";
        var activeLandmark = progressLandmark is not null
            ? new NavigationLandmarkSnapshot(progressLandmark.Text
                    .Replace("You just passed ", "", StringComparison.OrdinalIgnoreCase).TrimEnd('.'),
                "", "PROGRESS_REFERENCE", "ALONG_ROUTE",
                progressLandmark.Latitude ?? 0, progressLandmark.Longitude ?? 0, 0)
            : instructionType is "BoardJeepney" or "BoardTricycle"
                ? MapLandmark(boardLandmark)
                : instructionType is "PrepareToAlight" or "AlightJeepney" or "AlightTricycle"
                    ? MapLandmark(alightLandmark) : null;
        double? remaining = leg?.DistanceMeters is { } distance
            ? Math.Max(0, (double)distance - session.CurrentProgressMeters) : null;
        var routeName = leg?.Route?.RouteName ?? leg?.Instructions;
        var mode = leg?.TransportMode?.Code ?? "UNKNOWN";
        var structuredInstruction = new NavigationInstructionSnapshot(
            instructionType, routeName, mode, remaining,
            selected?.RequiresConfirmation ??
                instructionType is "BoardJeepney" or "BoardTricycle" or "AlightJeepney" or "AlightTricycle");

        var eventKey = string.Join(':', session.RecommendationId, session.CurrentLegIndex,
            speechType, activeLandmark?.Name ?? "none");
        var sameEvent = session.LastSpeechEventKey == eventKey;
        var noNewMeaningfulEvent = speechType == "Continue" &&
            status != "BOARDING_CONFIRMED" &&
            session.LastSpeechEventKey?.StartsWith(
                $"{session.RecommendationId}:{session.CurrentLegIndex}:",
                StringComparison.Ordinal) == true;
        var spoken = (sameEvent || noNewMeaningfulEvent) &&
            !string.IsNullOrWhiteSpace(session.LastSpokenInstruction)
            ? session.LastSpokenInstruction
            : await GenerateSpeechAsync(session, eventKey, new NavigationSpeechContext(
                speechType, session.CurrentNavigationState.ToString(), mode, routeName,
                activeLandmark?.Name, activeLandmark?.Role, activeLandmark?.Relation,
                remaining, status), cancellationToken);

        var snapshot = new NavigationSnapshot(
            session.TripSessionId,
            session.CurrentNavigationState.ToString(),
            session.CurrentLegIndex,
            MapLeg(leg),
            structuredInstruction,
            spoken,
            remaining,
            session.CurrentProgressMeters,
            leg is null ? null : new NavigationStopInfo(routeName, leg.StartLatitude,
                leg.StartLongitude, MapLandmark(boardLandmark)),
            leg is null ? null : new NavigationStopInfo(routeName, leg.EndLatitude,
                leg.EndLongitude, MapLandmark(alightLandmark)),
            activeLandmark,
            session.CurrentNavigationState == TripNavigationState.WaitingToBoard,
            session.CurrentNavigationState == TripNavigationState.ApproachingAlightPoint,
            session.CurrentNavigationState == TripNavigationState.OffRoute,
            status,
            TriggeredEvents(triggered, speechType, status),
            session.LastLatitude,
            session.LastLongitude);
        return new(snapshot);
    }

    private async Task<string> GenerateSpeechAsync(
        TripSession session, string eventKey, NavigationSpeechContext context,
        CancellationToken cancellationToken)
    {
        string spoken;
        try
        {
            spoken = await speech.PhraseAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "AI navigation speech unavailable for session {TripSessionId}; using fallback",
                session.TripSessionId);
            spoken = DeterministicNavigationSpeech.Phrase(context);
        }
        session.LastSpeechEventKey = eventKey;
        session.LastSpokenInstruction = spoken;
        session.UpdatedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(session, cancellationToken);
        return spoken;
    }

    private static NavigationInstruction? SelectInstruction(
        TripSession session, RecommendationLeg? leg,
        IReadOnlyList<NavigationInstruction> all,
        IReadOnlyList<NavigationInstruction> triggered)
    {
        if (triggered.Count > 0) return triggered[^1];
        var legInstructions = all.Where(item => item.Audience == NavigationInstructionAudience.Passenger &&
            (leg is null || item.LegIndex == leg.LegOrder)).ToList();
        var desired = session.CurrentNavigationState switch
        {
            TripNavigationState.WaitingToBoard => new[] { NavigationInstructionType.BoardJeepney, NavigationInstructionType.BoardTricycle },
            TripNavigationState.ApproachingAlightPoint => new[] { NavigationInstructionType.PrepareToAlight },
            TripNavigationState.Arrived => new[] { NavigationInstructionType.Arrived },
            TripNavigationState.OnJeepney or TripNavigationState.OnTricycle => new[] { NavigationInstructionType.Continue },
            _ => Array.Empty<NavigationInstructionType>()
        };
        var instruction =
            legInstructions.FirstOrDefault(item => desired.Contains(item.Type)) ??
            legInstructions.FirstOrDefault(item =>
                item.DistanceFromLegStartMeters is null ||
                item.DistanceFromLegStartMeters >= session.CurrentProgressMeters);

        if (instruction is not null)
            return instruction;

        return session.CurrentNavigationState == TripNavigationState.Arrived
            ? all.FirstOrDefault(item =>
                item.Type == NavigationInstructionType.Arrived)
            : null;
    }

    private static string EventInstructionType(TripSession session, string status,
        NavigationInstruction? selected, NavigationInstruction? progress) => status switch
    {
        "MISSED_ALIGHT" => "MissedAlight",
        "OFF_ROUTE" => "OffRoute",
        "REROUTE_SUCCEEDED" => "Rerouted",
        _ when progress is not null => "LandmarkNotice",
        _ when session.CurrentNavigationState == TripNavigationState.Arrived => "Arrived",
        _ when session.CurrentNavigationState == TripNavigationState.Cancelled => "Cancelled",
        _ => selected?.Type.ToString() ?? "Continue"
    };

    private static NavigationLegSnapshot? MapLeg(RecommendationLeg? leg) => leg is null ? null : new(
        leg.LegOrder, leg.TransportMode?.Code ?? "UNKNOWN", leg.Route?.RouteName,
        leg.FromName, leg.ToName, leg.StartLatitude, leg.StartLongitude,
        leg.EndLatitude, leg.EndLongitude, (double?)leg.DistanceMeters, leg.EstimatedFare);

    private static NavigationLandmarkSnapshot? MapLandmark(TripLandmarkCandidate? item) => item is null ? null : new(
        item.Name, item.Category, Role(item.Role), Relation(item.Relation), item.Latitude,
        item.Longitude, item.DistanceFromTargetMeters);

    private static IReadOnlyList<NavigationTriggeredEvent> TriggeredEvents(
        IReadOnlyList<NavigationInstruction> triggered, string speechType, string status)
    {
        var events = triggered.Select(item => new NavigationTriggeredEvent(item.Type.ToString(),
            item.Type == NavigationInstructionType.LandmarkNotice
                ? item.Text.Replace("You just passed ", "", StringComparison.OrdinalIgnoreCase).TrimEnd('.')
                : null)).ToList();
        if (status is "MISSED_ALIGHT" or "OFF_ROUTE" or "REROUTE_SUCCEEDED" ||
            speechType is "Arrived" or "Cancelled")
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
