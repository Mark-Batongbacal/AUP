using backend.Models.Database;

namespace backend.Repositories;

public interface IPassengerRideRequestRepository
{
    Task<passenger_ride_request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<List<passenger_ride_request>> GetByPassengerAsync(Guid passengerUserId, CancellationToken cancellationToken = default);

    Task<List<passenger_ride_request>> GetActiveSearchingAsync(CancellationToken cancellationToken = default);

    Task<passenger_ride_request> AddAsync(passenger_ride_request request, CancellationToken cancellationToken = default);

    Task<bool> UpdateStatusAsync(Guid requestId, string status, CancellationToken cancellationToken = default);
}
