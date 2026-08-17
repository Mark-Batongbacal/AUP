using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for fare rules only. This repository does not calculate passenger fares.
/// </summary>
public sealed class FareRuleRepository(TukiDbContext context) : IFareRuleRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<FareRule>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        _context.FareRules
            .AsNoTracking()
            .Include(rule => rule.Route)
            .Include(rule => rule.TransportMode)
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.RuleName)
            .ToListAsync(cancellationToken);

    public Task<List<FareRule>> GetActiveByRouteAsync(long routeId, CancellationToken cancellationToken = default) =>
        _context.FareRules
            .AsNoTracking()
            .Include(rule => rule.Route)
            .Where(rule => rule.RouteId == routeId && rule.IsActive)
            .OrderByDescending(rule => rule.EffectiveFrom)
            .ToListAsync(cancellationToken);

    public Task<List<FareRule>> GetActiveByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default) =>
        _context.FareRules
            .AsNoTracking()
            .Include(rule => rule.Route)
            .Where(rule => rule.Route.TransportModeId == transportModeId && rule.IsActive)
            .OrderByDescending(rule => rule.EffectiveFrom)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns the active fare rule effective on the supplied date. Route-specific rules can be
    /// requested with routeId; pass null for mode-level rules.
    /// </summary>
    public Task<FareRule?> GetCurrentlyEffectiveAsync(
        int transportModeId,
        long? routeId = null,
        DateOnly? effectiveOn = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = effectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return _context.FareRules
            .AsNoTracking()
            .Include(rule => rule.Route)
            .Include(rule => rule.Route)
            .Where(rule =>
                rule.Route.TransportModeId == transportModeId &&
                rule.RouteId == routeId &&
                rule.IsActive &&
                rule.EffectiveFrom <= targetDate &&
                (rule.EffectiveTo == null || rule.EffectiveTo >= targetDate))
            .OrderByDescending(rule => rule.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<FareRule?> GetByIdAsync(long fareRuleId, CancellationToken cancellationToken = default) =>
        _context.FareRules
            .AsNoTracking()
            .Include(rule => rule.Route)
            .FirstOrDefaultAsync(rule => rule.FareRuleId == fareRuleId, cancellationToken);
}
