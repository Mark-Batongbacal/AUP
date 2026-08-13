using backend.Models.Database;

namespace backend.Repositories;

public interface IRouteRecommendationRepository
{
    Task<List<route_recommendation>> GetByTripSearchAsync(Guid tripSearchId, CancellationToken cancellationToken = default);

    Task<route_recommendation?> GetByIdAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<route_recommendation?> GetWithLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<List<recommendation_leg>> GetOrderedLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<route_recommendation> AddAsync(route_recommendation recommendation, CancellationToken cancellationToken = default);
}
