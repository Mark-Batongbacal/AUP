namespace backend.Models.Routing;

/// <summary>
/// Per-request passenger preferences. These refine candidate preservation and
/// objective selection, while RoutingOptions remains the absolute server-side
/// safety and performance envelope.
/// </summary>
public sealed record JourneyPlanningPreferences(
    decimal? MaxFarePesos = null,
    double? MaxWalkingMeters = null,
    JourneyWalkingPreference WalkingPreference = JourneyWalkingPreference.Normal,
    JourneyOptimizationPreference? OptimizationPreference = null,
    IReadOnlySet<AccessMode>? AvoidTransportModes = null,
    OnboardTransitPlanningContext? OnboardTransit = null);

/// <summary>
/// Recovery context for a passenger who is already riding a route. Progress is
/// authoritative full-route progress, so loops and retraced coordinates remain
/// distinct occurrences.
/// </summary>
public sealed record OnboardTransitPlanningContext(
    string RouteId,
    double CurrentRouteProgressMeters,
    double ProgressToleranceMeters = 75)
{
    public bool IsMateriallyBehind(double boardProgressMeters) =>
        boardProgressMeters < CurrentRouteProgressMeters - ProgressToleranceMeters;

    public bool IsCurrentOccurrence(double boardProgressMeters) =>
        Math.Abs(boardProgressMeters - CurrentRouteProgressMeters) <= ProgressToleranceMeters;
}

public enum JourneyWalkingPreference
{
    Less,
    Normal,
    More
}

public enum JourneyOptimizationPreference
{
    Fastest,
    Cheapest,
    Efficient
}
