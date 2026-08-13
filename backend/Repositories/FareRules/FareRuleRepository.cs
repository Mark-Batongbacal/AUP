using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for fare rules only. This repository does not calculate passenger fares.
/// </summary>
public sealed class FareRuleRepository(SupabaseDbContext context) : IFareRuleRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<fare_rule>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        _context.fare_rules
            .AsNoTracking()
            .Include(rule => rule.route)
            .Include(rule => rule.transport_mode)
            .Where(rule => rule.is_active)
            .OrderBy(rule => rule.rule_name)
            .ToListAsync(cancellationToken);

    public Task<List<fare_rule>> GetActiveByRouteAsync(Guid routeId, CancellationToken cancellationToken = default) =>
        _context.fare_rules
            .AsNoTracking()
            .Include(rule => rule.transport_mode)
            .Where(rule => rule.route_id == routeId && rule.is_active)
            .OrderByDescending(rule => rule.effective_from)
            .ToListAsync(cancellationToken);

    public Task<List<fare_rule>> GetActiveByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default) =>
        _context.fare_rules
            .AsNoTracking()
            .Include(rule => rule.route)
            .Where(rule => rule.transport_mode_id == transportModeId && rule.is_active)
            .OrderByDescending(rule => rule.effective_from)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns the active fare rule effective on the supplied date. Route-specific rules can be
    /// requested with routeId; pass null for mode-level rules.
    /// </summary>
    public Task<fare_rule?> GetCurrentlyEffectiveAsync(
        short transportModeId,
        Guid? routeId = null,
        DateOnly? effectiveOn = null,
        CancellationToken cancellationToken = default)
    {
        var targetDate = effectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return _context.fare_rules
            .AsNoTracking()
            .Include(rule => rule.route)
            .Include(rule => rule.transport_mode)
            .Where(rule =>
                rule.transport_mode_id == transportModeId &&
                rule.route_id == routeId &&
                rule.is_active &&
                rule.effective_from <= targetDate &&
                (rule.effective_to == null || rule.effective_to >= targetDate))
            .OrderByDescending(rule => rule.effective_from)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<fare_rule?> GetByIdAsync(Guid fareRuleId, CancellationToken cancellationToken = default) =>
        _context.fare_rules
            .AsNoTracking()
            .Include(rule => rule.route)
            .Include(rule => rule.transport_mode)
            .FirstOrDefaultAsync(rule => rule.fare_rule_id == fareRuleId, cancellationToken);
}
