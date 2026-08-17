using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trip alerts and trigger state.
/// </summary>
public sealed class TripAlertRepository(TukiDbContext context) : ITripAlertRepository
{
    private readonly TukiDbContext _context = context;

    public Task<List<TripAlert>> GetByPassengerTripAsync(Guid passengerTripId, CancellationToken cancellationToken = default) =>
        _context.TripAlerts
            .AsNoTracking()
            .Include(alert => alert.Leg)
            .Include(alert => alert.TargetStop)
            .Where(alert => alert.PassengerTripId == passengerTripId)
            .OrderBy(alert => alert.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<TripAlert>> GetUntriggeredAsync(CancellationToken cancellationToken = default) =>
        _context.TripAlerts
            .AsNoTracking()
            .Include(alert => alert.PassengerTrip)
            .Include(alert => alert.TargetStop)
            .Where(alert => !alert.IsTriggered)
            .OrderBy(alert => alert.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateTriggerStateAsync(
        Guid alertId,
        bool isTriggered,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.TripAlerts.FirstOrDefaultAsync(alert => alert.AlertId == alertId, cancellationToken);
        if (alert is null)
        {
            return false;
        }

        alert.IsTriggered = isTriggered;
        alert.TriggeredAt = isTriggered ? triggeredAt ?? DateTime.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TripAlert> AddAsync(TripAlert alert, CancellationToken cancellationToken = default)
    {
        await _context.TripAlerts.AddAsync(alert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return alert;
    }
}
