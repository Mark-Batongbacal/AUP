using backend.Models.Routing;

namespace backend.Services.Routing;

/// <summary>
/// Final safety checks that depend on authoritative, road-network-confirmed
/// access distances rather than straight-line candidate estimates.
/// </summary>
public static class RoutingPlanSafety
{
    public static bool HasValidTransitAccess(
        JeepneyTripPlan plan,
        double maxWalkAccessDistanceMeters)
    {
        if (maxWalkAccessDistanceMeters < 0)
            return false;

        // Direct walk-only and direct tricycle trips have their own distance
        // limits. This rule is specifically for access to/from a jeepney trip.
        if (!plan.Legs.Any(leg => leg.Mode == AccessMode.Jeepney))
            return true;

        return AccessWithinLimit(plan.OriginAccess, maxWalkAccessDistanceMeters) &&
            AccessWithinLimit(plan.DestinationAccess, maxWalkAccessDistanceMeters);
    }

    private static bool AccessWithinLimit(
        JeepneyAccessSegment access,
        double maxWalkAccessDistanceMeters) =>
        access.Mode != AccessMode.Walk ||
        access.WalkDistanceMeters <= maxWalkAccessDistanceMeters;
}
