using backend.Models.Database;

namespace backend.Repositories;

public interface IPassengerTripRepository
{
    Task<passenger_trip?> GetByIdAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<passenger_trip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAndCurrentLegAsync(
        Guid passengerTripId,
        string status,
        int? currentLegOrder = null,
        CancellationToken cancellationToken = default);

    Task<passenger_trip> AddAsync(passenger_trip trip, CancellationToken cancellationToken = default);
}
