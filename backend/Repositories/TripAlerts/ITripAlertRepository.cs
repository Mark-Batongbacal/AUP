using backend.Models.Database;

namespace backend.Repositories;

public interface ITripAlertRepository
{
    Task<List<TripAlert>> GetByPassengerTripAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<TripAlert>> GetUntriggeredAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateTriggerStateAsync(
        Guid alertId,
        bool isTriggered,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default);

    Task<TripAlert> AddAsync(TripAlert alert, CancellationToken cancellationToken = default);
}
