using backend.Models.Routing;

namespace backend.Services.Routing;

public interface IJeepneyRoutingService
{
    Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
    Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default);
}
