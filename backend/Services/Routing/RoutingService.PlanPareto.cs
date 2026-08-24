using backend.Models.Routing;

namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Applies the confirmed Pareto rule once more after direct and
    /// geometry-proven access-state completions join the transit pool.
    /// Earlier candidate pruning cannot compare plans produced by those
    /// separate generation paths.
    /// </summary>
    private List<JeepneyTripPlan> PruneDominatedPlans(
        List<JeepneyTripPlan> plans)
    {
        if (plans.Count <= 1)
            return plans;

        const double epsilon = 0.001;
        var metrics = plans.ToDictionary(plan => plan, BuildPlanParetoMetrics);
        var kept = new List<JeepneyTripPlan>();

        foreach (var plan in plans)
        {
            var current = metrics[plan];
            var dominator = plans.FirstOrDefault(other =>
            {
                if (ReferenceEquals(other, plan))
                    return false;

                var alternative = metrics[other];
                var noWorse =
                    alternative.TimeSeconds <= current.TimeSeconds + epsilon &&
                    alternative.FarePesos <= current.FarePesos + epsilon &&
                    alternative.GeneralizedCostPesos <=
                        current.GeneralizedCostPesos + epsilon &&
                    alternative.AccessBurdenSeconds <=
                        current.AccessBurdenSeconds + epsilon &&
                    alternative.WalkingMeters <= current.WalkingMeters + epsilon &&
                    alternative.TransferCount <= current.TransferCount;
                var strictlyBetter =
                    alternative.TimeSeconds < current.TimeSeconds - epsilon ||
                    alternative.FarePesos < current.FarePesos - epsilon ||
                    alternative.GeneralizedCostPesos <
                        current.GeneralizedCostPesos - epsilon ||
                    alternative.AccessBurdenSeconds <
                        current.AccessBurdenSeconds - epsilon ||
                    alternative.WalkingMeters < current.WalkingMeters - epsilon ||
                    alternative.TransferCount < current.TransferCount;
                return noWorse && strictlyBetter;
            });

            if (dominator is null)
            {
                kept.Add(plan);
                continue;
            }

            _logger.LogDebug(
                "Routing plan rejected after generation-path merge: Pareto " +
                "dominated. routes={Routes} referenceRoutes={ReferenceRoutes}",
                PlanRouteSequence(plan),
                PlanRouteSequence(dominator));
        }

        return kept;
    }

    private static ConfirmedPlanParetoMetrics BuildPlanParetoMetrics(
        JeepneyTripPlan plan) =>
        new(
            plan.TotalTimeSeconds,
            plan.TotalFarePesos,
            plan.GeneralizedCostPesos,
            plan.Legs
                .Where(leg => leg.Mode != AccessMode.Jeepney)
                .Sum(leg => leg.DurationSeconds),
            plan.Legs
                .Where(leg => leg.Mode == AccessMode.Walk)
                .Sum(leg => leg.DistanceMeters),
            plan.TransferCount);

    private static string PlanRouteSequence(JeepneyTripPlan plan) =>
        string.Join('>', plan.Legs
            .Where(leg => leg.Mode == AccessMode.Jeepney)
            .Select(leg => leg.RouteId));

    private sealed record ConfirmedPlanParetoMetrics(
        double TimeSeconds,
        double FarePesos,
        double GeneralizedCostPesos,
        double AccessBurdenSeconds,
        double WalkingMeters,
        int TransferCount);
}
