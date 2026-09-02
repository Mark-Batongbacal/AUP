using backend.Models.Routing;

namespace backend.Services.Assistant;

public enum AssistantSurface
{
    Planning,
    ActiveTrip
}

public enum AssistantIntentType
{
    PlanRoute,
    SearchPlace,
    UpdateTripConstraints,
    ChangeDestination,
    ExplainRoute,
    StartNavigation,
    NavigationQuestion,
    Lost,
    CancelTrip,
    ConfirmAction,
    RejectAction,
    GeneralChat,
    Unknown
}

public sealed class AssistantIntent
{
    public AssistantIntentType Intent { get; set; }
    public string? DestinationQuery { get; set; }
    public string? OriginQuery { get; set; }
    public decimal? BudgetPesos { get; set; }
    public string? Preference { get; set; }
    public double? MaxWalkingMeters { get; set; }
    public AssistantWalkingPreference? WalkingPreference { get; set; }
    public List<string> AvoidTransportModes { get; set; } = [];
    public string? ResponseType { get; set; }
}

public sealed record AssistantRequest(
    string? Message,
    double? OriginLatitude = null,
    double? OriginLongitude = null,
    Guid? TripSessionId = null,
    string? DestinationId = null,
    Guid? ConversationId = null,
    string? OperationId = null,
    string? DestinationSelectionToken = null,
    string? SelectedDestinationCandidateId = null);

public sealed record ActiveTripAssistantRequest(
    string Message,
    string? DestinationId = null,
    Guid? ConversationId = null,
    string? OperationId = null);

public sealed record AssistantConversationTurn(string Sender, string Message);

public sealed record AssistantConversationContext(
    Guid ConversationId,
    string? LastDestinationQuery,
    decimal? LastBudgetPesos,
    IReadOnlyList<AssistantConversationTurn> RecentTurns,
    AssistantPlanningState? PlanningState = null);

public enum AssistantWalkingPreference
{
    Less,
    Normal,
    More
}

/// <summary>
/// Server-owned conversation state. It is persisted as JSON on the
/// conversation, never copied to permanent user-profile preferences.
/// </summary>
public sealed record AssistantPlanningState(
    AssistantResolvedDestination? Destination = null,
    decimal? MaxFarePesos = null,
    string? OptimizationPreference = null,
    double? MaxWalkingMeters = null,
    AssistantWalkingPreference WalkingPreference = AssistantWalkingPreference.Normal,
    IReadOnlyList<string>? AvoidTransportModes = null,
    AssistantPendingDestinationResolution? PendingDestination = null,
    double? OriginLatitude = null,
    double? OriginLongitude = null);

public sealed record AssistantResolvedDestination(
    string ProviderId,
    string Name,
    double Latitude,
    double Longitude,
    string Category,
    string? Address = null);

public sealed record AssistantPendingDestinationResolution(
    string SelectionToken,
    DateTime ExpiresAtUtc,
    IReadOnlyList<AssistantPendingDestinationCandidate> Candidates);

public sealed record AssistantPendingDestinationCandidate(
    string CandidateId,
    string ProviderId,
    string Name,
    double Latitude,
    double Longitude,
    string Category,
    string? Address = null);

/// <summary>
/// Provider-neutral destination card returned to the planning UI. CandidateId
/// is only meaningful with the accompanying conversation and selection token.
/// </summary>
public sealed record AssistantDestinationCandidate(
    string CandidateId,
    string Name,
    double Latitude,
    double Longitude,
    string Category,
    string? Address = null);

public sealed record AssistantActiveTripContext(
    Guid TripSessionId,
    string NavigationState,
    string? NavigationStatus,
    string? DestinationName,
    double DestinationLatitude,
    double DestinationLongitude,
    int CurrentLegIndex,
    string? CurrentMode,
    string? CurrentRouteName,
    double? RemainingDistanceMeters,
    decimal ApproxFareSpent,
    decimal EstimatedRemainingFare,
    decimal? OriginalBudgetPesos,
    string? OriginalPreference,
    string? NextInstruction,
    double? LastLatitude,
    double? LastLongitude,
    double? LastAccuracyMeters,
    DateTime? LastLocationAt,
    string LocationReliability,
    double? LocationAgeSeconds,
    bool CanUseLocationForReroute);

public sealed record AssistantContext(
    AssistantSurface Surface,
    string Message,
    AssistantConversationContext Conversation,
    AssistantActiveTripContext? ActiveTrip = null);

public sealed record AssistantJourney(
    Guid JourneyId,
    string RecommendationType,
    double FarePesos,
    double DurationSeconds,
    double WalkingMeters,
    IReadOnlyList<AssistantJourneyLeg> Legs,
    JeepneyTripPlan Plan);

public sealed record AssistantJourneyLeg(string Mode, string? RouteName);

public sealed record AssistantAction(
    string Type,
    bool RequiresConfirmation,
    Guid? TripSessionId = null,
    decimal? BudgetPesos = null,
    string? Preference = null,
    double? MaxWalkingMeters = null,
    IReadOnlyList<string>? AvoidTransportModes = null);

public sealed record AssistantNavigationState(
    Guid TripSessionId,
    string TripState,
    int CurrentLegIndex,
    string? CurrentMode,
    string? CurrentRouteName,
    string? NextInstruction,
    double? RemainingDistanceMeters,
    decimal ApproxFareSpent,
    decimal EstimatedRemainingFare,
    string? Status);

public sealed record AssistantResponse(
    string Status,
    string Message,
    IReadOnlyList<AssistantJourney>? Journeys = null,
    IReadOnlyList<AssistantDestinationCandidate>? Destinations = null,
    AssistantNavigationState? Navigation = null,
    AssistantDestinationCandidate? Destination = null,
    Guid? ConversationId = null,
    string? Surface = null,
    AssistantAction? Action = null,
    string? DestinationSelectionToken = null);
