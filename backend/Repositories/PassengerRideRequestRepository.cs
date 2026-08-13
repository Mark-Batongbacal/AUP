using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger ride requests. Active ride requests are rows with SEARCHING status.
/// </summary>
public sealed class PassengerRideRequestRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;
    private const string SearchingStatus = "SEARCHING";

    public Task<passenger_ride_request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.passenger_ride_requests
            .AsNoTracking()
            .Include(request => request.transport_mode)
            .FirstOrDefaultAsync(request => request.request_id == requestId, cancellationToken);

    public Task<List<passenger_ride_request>> GetByPassengerAsync(Guid passengerUserId, CancellationToken cancellationToken = default) =>
        _context.passenger_ride_requests
            .AsNoTracking()
            .Include(request => request.transport_mode)
            .Where(request => request.passenger_user_id == passengerUserId)
            .OrderByDescending(request => request.requested_at)
            .ToListAsync(cancellationToken);

    public Task<List<passenger_ride_request>> GetActiveSearchingAsync(CancellationToken cancellationToken = default) =>
        _context.passenger_ride_requests
            .AsNoTracking()
            .Include(request => request.transport_mode)
            .Where(request => request.status == SearchingStatus && (request.expires_at == null || request.expires_at > DateTime.UtcNow))
            .OrderBy(request => request.requested_at)
            .ToListAsync(cancellationToken);

    public async Task<passenger_ride_request> AddAsync(passenger_ride_request request, CancellationToken cancellationToken = default)
    {
        await _context.passenger_ride_requests.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<bool> UpdateStatusAsync(Guid requestId, string status, CancellationToken cancellationToken = default)
    {
        var request = await _context.passenger_ride_requests.FirstOrDefaultAsync(request => request.request_id == requestId, cancellationToken);
        if (request is null)
        {
            return false;
        }

        request.status = status;
        request.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
