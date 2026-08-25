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
    public List<string> AvoidTransportModes { get; set; } = [];
    public string? ResponseType { get; set; }
}

public sealed record AssistantRequest(
    string Message,
    double? OriginLatitude = null,
    double? OriginLongitude = null,
    Guid? TripSessionId = null,
    string? DestinationId = null,
    Guid? ConversationId = null,
    string? OperationId = null);

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
    IReadOnlyList<AssistantConversationTurn> RecentTurns);

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
    DateTime? LastLocationAt);

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
    IReadOnlyList<backend.Models.Destinations.DestinationSearchResult>? Destinations = null,
    AssistantNavigationState? Navigation = null,
    backend.Models.Destinations.DestinationSearchResult? Destination = null,
    Guid? ConversationId = null,
    string? Surface = null,
    AssistantAction? Action = null);
