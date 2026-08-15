using backend.Models.Routing;

namespace backend.Services.Route;

public interface IJeepneyRoutingService
{
    Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}