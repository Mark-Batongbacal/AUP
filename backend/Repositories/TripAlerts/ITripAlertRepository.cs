using backend.Models.Database;

namespace backend.Repositories;

public interface ITripAlertRepository
{
    Task<List<trip_alert>> GetByPassengerTripAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<trip_alert>> GetUntriggeredAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateTriggerStateAsync(
        Guid alertId,
        bool isTriggered,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default);

    Task<trip_alert> AddAsync(trip_alert alert, CancellationToken cancellationToken = default);
}
