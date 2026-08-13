using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trip searches. Missing search lookups return null.
/// </summary>
public sealed class TripSearchRepository(SupabaseDbContext context) : ITripSearchRepository
{
    private readonly SupabaseDbContext _context = context;

    public async Task<trip_search> AddAsync(trip_search tripSearch, CancellationToken cancellationToken = default)
    {
        await _context.trip_searches.AddAsync(tripSearch, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tripSearch;
    }

    public Task<trip_search?> GetByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default) =>
        _context.trip_searches
            .AsNoTracking()
            .FirstOrDefaultAsync(search => search.trip_search_id == tripSearchId, cancellationToken);

    public Task<List<trip_search>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.trip_searches
            .AsNoTracking()
            .Where(search => search.user_id == userId)
            .OrderByDescending(search => search.requested_at)
            .ToListAsync(cancellationToken);
}
