using backend.Models.Database;

namespace backend.Repositories;

public interface IPassengerTripRepository
{
    Task<PassengerTrip?> GetByIdAsync(Guid passengerTripId, CancellationToken cancellationToken = default);

    Task<List<PassengerTrip>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAndCurrentLegAsync(
        Guid passengerTripId,
        string Status,
        int? currentLegOrder = null,
        CancellationToken cancellationToken = default);

    Task<PassengerTrip> AddAsync(PassengerTrip trip, CancellationToken cancellationToken = default);

    Task<int> CountByUserAndRecommendationAsync(Guid userId, Guid recommendationId, CancellationToken cancellationToken = default);
}
