using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteRecommendationRepository
{
    Task<List<RouteRecommendation>> GetByTripSearchAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<RouteRecommendation?> GetByIdAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<RouteRecommendation?> GetWithLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<List<RecommendationLeg>> GetOrderedLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<RouteRecommendation> AddAsync(RouteRecommendation Recommendation, CancellationToken cancellationToken = default);
}
