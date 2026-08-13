using backend.Models.Database;

namespace backend.Repositories;

public interface IRecommendationLegRepository
{
    Task<List<recommendation_leg>> GetOrderedByRecommendationAsync(Guid recommendationId, CancellationToken cancellationToken = default);

    Task<recommendation_leg?> GetByIdAsync(Guid legId, CancellationToken cancellationToken = default);

    Task<recommendation_leg> AddAsync(recommendation_leg leg, CancellationToken cancellationToken = default);
}
