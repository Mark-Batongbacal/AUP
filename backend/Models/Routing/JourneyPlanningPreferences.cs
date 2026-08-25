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
    IReadOnlySet<AccessMode>? AvoidTransportModes = null);

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
