using backend.Models.Database;

namespace backend.Repositories;

public interface IRecommendationLegRepository
{
    Task<List<RecommendationLeg>> GetOrderedByRecommendationAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<RecommendationLeg?> GetByIdAsync(Guid legId, CancellationToken cancellationToken = default);

    Task<RecommendationLeg> AddAsync(RecommendationLeg Leg, CancellationToken cancellationToken = default);
}
