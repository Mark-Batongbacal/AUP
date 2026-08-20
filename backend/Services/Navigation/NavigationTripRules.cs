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
        var mode = leg.TransportMode?.Code?.ToUpperInvariant();
        return (mode is "JEEPNEY" or "TRICYCLE" or "TRIKE") && leg.EstimatedFare > 0;
    }

    public static decimal EstimatedRemainingFare(
        TripSession session,
        IReadOnlyList<RecommendationLeg> legs)
    {
        return legs
            .Where(leg => leg.LegOrder >= session.CurrentLegIndex)
            .Sum(leg => leg.EstimatedFare);
    }
}
