using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for individual Recommendation legs. Leg sequences are ordered by LegOrder.
/// </summary>
public sealed class RecommendationLegRepository(TukiDbContext context) : IRecommendationLegRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<RecommendationLeg>> GetOrderedByRecommendationAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.RecommendationLegs
            .AsNoTracking()
            .Include(Leg => Leg.TransportMode)
            .Include(Leg => Leg.Route)
            .Include(Leg => Leg.FromStop)
            .Include(Leg => Leg.ToStop)
            .Where(Leg => Leg.RecommendationId == recommendationId)
            .OrderBy(Leg => Leg.LegOrder)
            .ToListAsync(cancellationToken);

    public Task<RecommendationLeg?> GetByIdAsync(Guid legId, CancellationToken cancellationToken = default) =>
        _context.RecommendationLegs
            .AsNoTracking()
            .Include(Leg => Leg.TransportMode)
            .Include(Leg => Leg.Route)
            .Include(Leg => Leg.FromStop)
            .Include(Leg => Leg.ToStop)
            .FirstOrDefaultAsync(Leg => Leg.LegId == legId, cancellationToken);

    public async Task<RecommendationLeg> AddAsync(RecommendationLeg Leg, CancellationToken cancellationToken = default)
    {
        await _context.RecommendationLegs.AddAsync(Leg, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Leg;
    }
}
