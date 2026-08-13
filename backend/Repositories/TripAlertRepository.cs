using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger trip alerts and trigger state.
/// </summary>
public sealed class TripAlertRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<trip_alert>> GetByPassengerTripAsync(Guid passengerTripId, CancellationToken cancellationToken = default) =>
        _context.trip_alerts
            .AsNoTracking()
            .Include(alert => alert.leg)
            .Include(alert => alert.target_stop)
            .Where(alert => alert.passenger_trip_id == passengerTripId)
            .OrderBy(alert => alert.created_at)
            .ToListAsync(cancellationToken);

    public Task<List<trip_alert>> GetUntriggeredAsync(CancellationToken cancellationToken = default) =>
        _context.trip_alerts
            .AsNoTracking()
            .Include(alert => alert.passenger_trip)
            .Include(alert => alert.target_stop)
            .Where(alert => !alert.is_triggered)
            .OrderBy(alert => alert.created_at)
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateTriggerStateAsync(
        Guid alertId,
        bool isTriggered,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default)
    {
        var alert = await _context.trip_alerts.FirstOrDefaultAsync(alert => alert.alert_id == alertId, cancellationToken);
        if (alert is null)
        {
            return false;
        }

        alert.is_triggered = isTriggered;
        alert.triggered_at = isTriggered ? triggeredAt ?? DateTime.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<trip_alert> AddAsync(trip_alert alert, CancellationToken cancellationToken = default)
    {
        await _context.trip_alerts.AddAsync(alert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return alert;
    }
}
