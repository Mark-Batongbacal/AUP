using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for stored ride matches. This repository does not perform matching algorithms.
/// </summary>
public sealed class RideMatchRepository(SupabaseDbContext context) : IRideMatchRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<ride_match>> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.ride_matches
            .AsNoTracking()
            .Include(match => match.driver)
            .Include(match => match.session)
            .Include(match => match.vehicle)
            .Where(match => match.request_id == requestId)
            .OrderByDescending(match => match.match_score)
            .ThenBy(match => match.offered_at)
            .ToListAsync(cancellationToken);

    public Task<List<ride_match>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.ride_matches
            .AsNoTracking()
            .Include(match => match.request)
            .Include(match => match.session)
            .Include(match => match.vehicle)
            .Where(match => match.driver_id == driverId)
            .OrderByDescending(match => match.offered_at)
            .ToListAsync(cancellationToken);

    public Task<ride_match?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _context.ride_matches
            .AsNoTracking()
            .Include(match => match.driver)
            .Include(match => match.request)
            .Include(match => match.session)
            .Include(match => match.vehicle)
            .FirstOrDefaultAsync(match => match.match_id == matchId, cancellationToken);

    public async Task<ride_match> AddAsync(ride_match match, CancellationToken cancellationToken = default)
    {
        await _context.ride_matches.AddAsync(match, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return match;
    }

    public async Task<ride_match> UpdateAsync(ride_match match, CancellationToken cancellationToken = default)
    {
        _context.ride_matches.Update(match);
        await _context.SaveChangesAsync(cancellationToken);
        return match;
    }
}
