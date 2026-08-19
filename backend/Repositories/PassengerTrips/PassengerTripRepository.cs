using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trips. Missing trip lookups return null.
/// </summary>
public sealed class PassengerTripRepository(TukiDbContext context) : IPassengerTripRepository
{
    private readonly TukiDbContext _context = context;

    public Task<PassengerTrip?> GetByIdAsync(Guid passengerTripId, CancellationToken cancellationToken = default) =>
        _context.PassengerTrips
            .AsNoTracking()
            .Include(trip => trip.Recommendation)
            .FirstOrDefaultAsync(trip => trip.PassengerTripId == passengerTripId, cancellationToken);

    public Task<List<PassengerTrip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.PassengerTrips
            .AsNoTracking()
            .Include(trip => trip.Recommendation)
            .Where(trip => trip.UserId == userId)
            .OrderByDescending(trip => trip.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateStatusAndCurrentLegAsync(
        Guid passengerTripId,
        string Status,
        int? currentLegOrder = null,
        CancellationToken cancellationToken = default)
    {
        var trip = await _context.PassengerTrips.FirstOrDefaultAsync(trip => trip.PassengerTripId == passengerTripId, cancellationToken);
        if (trip is null)
        {
            return false;
        }

        trip.Status = Status;
        if (currentLegOrder.HasValue)
        {
            trip.CurrentLegOrder = currentLegOrder.Value;
        }

        trip.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PassengerTrip> AddAsync(PassengerTrip trip, CancellationToken cancellationToken = default)
    {
        await _context.PassengerTrips.AddAsync(trip, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return trip;
    }

    public Task<int> CountByUserAndRecommendationAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default) =>
        _context.PassengerTrips
            .AsNoTracking()
            .CountAsync(trip => trip.UserId == userId && trip.RecommendationId == recommendationId, cancellationToken);
}
