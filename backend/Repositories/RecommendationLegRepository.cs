using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for individual recommendation legs. Leg sequences are ordered by leg_order.
/// </summary>
public sealed class RecommendationLegRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<recommendation_leg>> GetOrderedByRecommendationAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.recommendation_legs
            .AsNoTracking()
            .Include(leg => leg.transport_mode)
            .Include(leg => leg.route)
            .Include(leg => leg.from_stop)
            .Include(leg => leg.to_stop)
            .Where(leg => leg.recommendation_id == recommendationId)
            .OrderBy(leg => leg.leg_order)
            .ToListAsync(cancellationToken);

    public Task<recommendation_leg?> GetByIdAsync(Guid legId, CancellationToken cancellationToken = default) =>
        _context.recommendation_legs
            .AsNoTracking()
            .Include(leg => leg.transport_mode)
            .Include(leg => leg.route)
            .Include(leg => leg.from_stop)
            .Include(leg => leg.to_stop)
            .FirstOrDefaultAsync(leg => leg.leg_id == legId, cancellationToken);

    public async Task<recommendation_leg> AddAsync(recommendation_leg leg, CancellationToken cancellationToken = default)
    {
        await _context.recommendation_legs.AddAsync(leg, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return leg;
    }
}
