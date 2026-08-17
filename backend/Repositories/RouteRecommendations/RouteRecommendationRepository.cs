using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for stored Route recommendations. This repository does not rank recommendations.
/// </summary>
public sealed class RouteRecommendationRepository(TukiDbContext context) : IRouteRecommendationRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<RouteRecommendation>> GetByTripSearchAsync(Guid tripSearchId, CancellationToken cancellationToken = default) =>
        _context.RouteRecommendations
            .AsNoTracking()
            .Where(Recommendation => Recommendation.TripSearchId == tripSearchId)
            .OrderBy(Recommendation => Recommendation.RecommendationType)
            .ThenBy(Recommendation => Recommendation.RankNumber)
            .ToListAsync(cancellationToken);

    public Task<RouteRecommendation?> GetByIdAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.RouteRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(Recommendation => Recommendation.RecommendationId == recommendationId, cancellationToken);

    /// <summary>
    /// Includes Recommendation legs with related routes, stops, and transport modes.
    /// </summary>
    public Task<RouteRecommendation?> GetWithLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.RouteRecommendations
            .AsNoTracking()
            .Include(Recommendation => Recommendation.RecommendationLegs)
                .ThenInclude(Leg => Leg.TransportMode)
            .Include(Recommendation => Recommendation.RecommendationLegs)
                .ThenInclude(Leg => Leg.Route)
            .Include(Recommendation => Recommendation.RecommendationLegs)
                .ThenInclude(Leg => Leg.FromStop)
            .Include(Recommendation => Recommendation.RecommendationLegs)
                .ThenInclude(Leg => Leg.ToStop)
            .FirstOrDefaultAsync(Recommendation => Recommendation.RecommendationId == recommendationId, cancellationToken);

    public Task<List<RecommendationLeg>> GetOrderedLegsAsync(Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.RecommendationLegs
            .AsNoTracking()
            .Include(Leg => Leg.TransportMode)
            .Include(Leg => Leg.Route)
            .Include(Leg => Leg.FromStop)
            .Include(Leg => Leg.ToStop)
            .Where(Leg => Leg.RecommendationId == recommendationId)
            .OrderBy(Leg => Leg.LegOrder)
            .ToListAsync(cancellationToken);

    public async Task<RouteRecommendation> AddAsync(RouteRecommendation Recommendation, CancellationToken cancellationToken = default)
    {
        await _context.RouteRecommendations.AddAsync(Recommendation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Recommendation;
    }
}
