using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for stored route recommendations. This repository does not rank recommendations.
/// </summary>
public sealed class RouteRecommendationRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<route_recommendation>> GetByTripSearchAsync(Guid tripSearchId, CancellationToken cancellationToken = default) =>
        _context.route_recommendations
            .AsNoTracking()
            .Where(recommendation => recommendation.trip_search_id == tripSearchId)
            .OrderBy(recommendation => recommendation.recommendation_type)
            .ThenBy(recommendation => recommendation.rank_number)
            .ToListAsync(cancellationToken);

    public Task<route_recommendation?> GetByIdAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.route_recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(recommendation => recommendation.recommendation_id == recommendationId, cancellationToken);

    /// <summary>
    /// Includes recommendation legs with related routes, stops, and transport modes.
    /// </summary>
    public Task<route_recommendation?> GetWithLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.route_recommendations
            .AsNoTracking()
            .Include(recommendation => recommendation.recommendation_legs)
                .ThenInclude(leg => leg.transport_mode)
            .Include(recommendation => recommendation.recommendation_legs)
                .ThenInclude(leg => leg.route)
            .Include(recommendation => recommendation.recommendation_legs)
                .ThenInclude(leg => leg.from_stop)
            .Include(recommendation => recommendation.recommendation_legs)
                .ThenInclude(leg => leg.to_stop)
            .FirstOrDefaultAsync(recommendation => recommendation.recommendation_id == recommendationId, cancellationToken);

    public Task<List<recommendation_leg>> GetOrderedLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.recommendation_legs
            .AsNoTracking()
            .Include(leg => leg.transport_mode)
            .Include(leg => leg.route)
            .Include(leg => leg.from_stop)
            .Include(leg => leg.to_stop)
            .Where(leg => leg.recommendation_id == recommendationId)
            .OrderBy(leg => leg.leg_order)
            .ToListAsync(cancellationToken);

    public async Task<route_recommendation> AddAsync(route_recommendation recommendation, CancellationToken cancellationToken = default)
    {
        await _context.route_recommendations.AddAsync(recommendation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return recommendation;
    }
}
