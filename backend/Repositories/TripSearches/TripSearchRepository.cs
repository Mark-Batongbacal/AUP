using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trip searches. Missing search lookups return null.
/// </summary>
public sealed class TripSearchRepository(TukiDbContext context) : ITripSearchRepository
{
    private readonly TukiDbContext _context = context;

    public async Task<TripSearch> AddAsync(TripSearch tripSearch, CancellationToken cancellationToken = default)
    {
        await _context.TripSearches.AddAsync(tripSearch, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tripSearch;
    }

    public Task<TripSearch?> GetByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default) =>
        _context.TripSearches
            .AsNoTracking()
            .FirstOrDefaultAsync(search => search.TripSearchId == tripSearchId, cancellationToken);

    public Task<List<TripSearch>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.TripSearches
            .AsNoTracking()
            .Where(search => search.UserId == userId)
            .OrderByDescending(search => search.RequestedAt)
            .ToListAsync(cancellationToken);
}
