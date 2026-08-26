using backend.Models.Database;

namespace backend.Services.Navigation;

public static class NavigationTripRules
{
    public static double? RemainingMeters(TripSession session, RecommendationLeg? leg)
    {
        if (leg?.DistanceMeters is not { } distance) return null;
        return Math.Max(0, (double)distance - session.CurrentProgressMeters);
    }

    public static bool CanConfirmAlighting(
        TripSession session,
        RecommendationLeg? leg,
        NavigationOptions options)
    {
        return session.CurrentNavigationState == TripNavigationState.ApproachingAlightPoint &&
               RemainingMeters(session, leg) is { } remaining &&
               remaining <= options.ConfirmAlightDistanceMeters;
    }

    public static bool IsPaidTransport(RecommendationLeg leg)
    {
        return IsTransit(leg) && leg.EstimatedFare > 0;
    }

    public static bool IsTransit(RecommendationLeg leg) =>
        leg.TransportMode?.Code?.ToUpperInvariant() is "JEEPNEY" or "TRICYCLE" or "TRIKE";

    public static decimal EstimatedRemainingFare(
        TripSession session,
        IReadOnlyList<RecommendationLeg> legs)
    {
        return legs
            .Where(leg => leg.LegOrder >= session.CurrentLegIndex)
            .Sum(leg => leg.EstimatedFare);
    }
}
