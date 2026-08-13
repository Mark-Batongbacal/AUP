using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for stored ride matches. This repository does not perform matching algorithms.
/// </summary>
public sealed class RideMatchRepository(SupabaseDbContext context) : IRideMatchRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<RideMatch>> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.RideMatches
            .AsNoTracking()
            .Include(match => match.Driver)
            .Include(match => match.Session)
            .Include(match => match.Vehicle)
            .Where(match => match.RequestId == requestId)
            .OrderByDescending(match => match.MatchScore)
            .ThenBy(match => match.OfferedAt)
            .ToListAsync(cancellationToken);

    public Task<List<RideMatch>> GetByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.RideMatches
            .AsNoTracking()
            .Include(match => match.Request)
            .Include(match => match.Session)
            .Include(match => match.Vehicle)
            .Where(match => match.DriverId == driverId)
            .OrderByDescending(match => match.OfferedAt)
            .ToListAsync(cancellationToken);

    public Task<RideMatch?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _context.RideMatches
            .AsNoTracking()
            .Include(match => match.Driver)
            .Include(match => match.Request)
            .Include(match => match.Session)
            .Include(match => match.Vehicle)
            .FirstOrDefaultAsync(match => match.MatchId == matchId, cancellationToken);

    public async Task<RideMatch> AddAsync(RideMatch match, CancellationToken cancellationToken = default)
    {
        await _context.RideMatches.AddAsync(match, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return match;
    }

    public async Task<RideMatch> UpdateAsync(RideMatch match, CancellationToken cancellationToken = default)
    {
        _context.RideMatches.Update(match);
        await _context.SaveChangesAsync(cancellationToken);
        return match;
    }
}
