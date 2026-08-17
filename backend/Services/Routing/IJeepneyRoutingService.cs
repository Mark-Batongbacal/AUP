using backend.Models.Routing;

namespace backend.Services.Routing;

public interface IJeepneyRoutingService
{
    Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
