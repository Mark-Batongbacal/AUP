using backend.Models.Routing;

namespace backend.Services.Assistant;

public enum AssistantIntentType
{
    PlanRoute,
    ClarifyDestination,
    StartNavigation,
    NavigationQuestion,
    Lost,
    CancelTrip,
    Unknown
}

public sealed class AssistantIntent
{
    public AssistantIntentType Intent { get; set; }
    public string? DestinationQuery { get; set; }
    public string? OriginQuery { get; set; }
    public decimal? BudgetPesos { get; set; }
    public string? Preference { get; set; }
    public Guid? TripSessionId { get; set; }
}

public sealed record AssistantRequest(
    string Message,
    double? OriginLatitude = null,
    double? OriginLongitude = null,
    Guid? TripSessionId = null,
    string? DestinationId = null);

public sealed record AssistantJourney(
    Guid JourneyId,
    string RecommendationType,
    double FarePesos,
    double DurationSeconds,
    double WalkingMeters,
    IReadOnlyList<AssistantJourneyLeg> Legs,
    JeepneyTripPlan Plan);

public sealed record AssistantJourneyLeg(string Mode, string? RouteName);

public sealed record AssistantResponse(
    string Status,
    string Message,
    IReadOnlyList<AssistantJourney>? Journeys = null,
    IReadOnlyList<backend.Models.Destinations.DestinationSearchResult>? Destinations = null,
    object? Navigation = null,
    backend.Models.Destinations.DestinationSearchResult? Destination = null);
