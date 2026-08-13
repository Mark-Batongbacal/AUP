using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trips. Missing trip lookups return null.
/// </summary>
public sealed class PassengerTripRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<passenger_trip?> GetByIdAsync(Guid passengerTripId, CancellationToken cancellationToken = default) =>
        _context.passenger_trips
            .AsNoTracking()
            .Include(trip => trip.recommendation)
            .FirstOrDefaultAsync(trip => trip.passenger_trip_id == passengerTripId, cancellationToken);

    public Task<List<passenger_trip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.passenger_trips
            .AsNoTracking()
            .Include(trip => trip.recommendation)
            .Where(trip => trip.user_id == userId)
            .OrderByDescending(trip => trip.created_at)
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateStatusAndCurrentLegAsync(
        Guid passengerTripId,
        string status,
        int? currentLegOrder = null,
        CancellationToken cancellationToken = default)
    {
        var trip = await _context.passenger_trips.FirstOrDefaultAsync(trip => trip.passenger_trip_id == passengerTripId, cancellationToken);
        if (trip is null)
        {
            return false;
        }

        trip.status = status;
        if (currentLegOrder.HasValue)
        {
            trip.current_leg_order = currentLegOrder.Value;
        }

        trip.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<passenger_trip> AddAsync(passenger_trip trip, CancellationToken cancellationToken = default)
    {
        await _context.passenger_trips.AddAsync(trip, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return trip;
    }
}
