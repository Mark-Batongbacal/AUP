using backend.Models.Database;

namespace backend.Repositories;

public interface IPassengerRideRequestRepository
{
    Task<PassengerRideRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<PassengerRideRequest>> GetByPassengerAsync(Guid passengerUserId, CancellationToken cancellationToken = default);

    Task<List<PassengerRideRequest>> GetActiveSearchingAsync(CancellationToken cancellationToken = default);

    Task<PassengerRideRequest> AddAsync(PassengerRideRequest Request, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(Guid requestId, string Status, CancellationToken cancellationToken = default);
}
