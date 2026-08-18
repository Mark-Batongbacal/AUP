using backend.Models.Routing;
using backend.Services.Assistant;

namespace backend.Services.Routing;

public sealed record JourneyPlanRequest(
    double OriginLatitude,
    double OriginLongitude,
    string DestinationName,
    double DestinationLatitude,
    double DestinationLongitude,
    decimal? Budget = null,
    string? Preference = null);

public sealed record MobileJourneyRecommendation(
    Guid RecommendationId,
    JeepneyTripPlan Plan);

public interface IJourneyPlanningFacadeService
{
    Task<IReadOnlyList<MobileJourneyRecommendation>> PlanAsync(
        Guid userId, JourneyPlanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class JourneyPlanningFacadeService(
    IRoutingService routing,
    IJourneyPlanPersistenceService persistence) : IJourneyPlanningFacadeService
{
    public async Task<IReadOnlyList<MobileJourneyRecommendation>> PlanAsync(
        Guid userId, JourneyPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(request.DestinationName))
            throw new RoutingValidationException("INVALID_REQUEST",
                "A user and destination name are required.");
        var plans = await routing.PlanTripsAsync(
            request.OriginLatitude, request.OriginLongitude,
            request.DestinationLatitude, request.DestinationLongitude,
            cancellationToken);
        var eligible = plans.Where(plan => request.Budget is null ||
            (decimal)plan.TotalFarePesos <= request.Budget.Value).ToList();
        if (!string.IsNullOrWhiteSpace(request.Preference))
            eligible = eligible.OrderByDescending(plan => plan.RecommendationType.Split(',')
                .Contains(request.Preference, StringComparer.OrdinalIgnoreCase)).ToList();
        if (eligible.Count == 0) return [];
        var stored = await persistence.PersistAsync(userId,
            request.OriginLatitude, request.OriginLongitude,
            request.DestinationName.Trim(), request.DestinationLatitude,
            request.DestinationLongitude, request.Budget, request.Preference,
            eligible, cancellationToken);
        return stored.Select(item => new MobileJourneyRecommendation(
            item.Recommendation.RecommendationId, item.Plan)).ToList();
    }
}
