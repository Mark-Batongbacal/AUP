using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for passenger ride requests. Active ride requests are rows with SEARCHING Status.
/// </summary>
public sealed class PassengerRideRequestRepository(TukiDbContext context) : IPassengerRideRequestRepository
{
    private readonly TukiDbContext _context = context;
    private const string SearchingStatus = "SEARCHING";

    public Task<PassengerRideRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.PassengerRideRequests
            .AsNoTracking()
            .Include(Request => Request.TransportMode)
            .FirstOrDefaultAsync(Request => Request.RequestId == requestId, cancellationToken);

    public Task<List<PassengerRideRequest>> GetByPassengerAsync(Guid passengerUserId, CancellationToken cancellationToken = default) =>
        _context.PassengerRideRequests
            .AsNoTracking()
            .Include(Request => Request.TransportMode)
            .Where(Request => Request.PassengerUserId == passengerUserId)
            .OrderByDescending(Request => Request.RequestedAt)
            .ToListAsync(cancellationToken);

    public Task<List<PassengerRideRequest>> GetActiveSearchingAsync(CancellationToken cancellationToken = default) =>
        _context.PassengerRideRequests
            .AsNoTracking()
            .Include(Request => Request.TransportMode)
            .Where(Request => Request.Status == SearchingStatus && (Request.ExpiresAt == null || Request.ExpiresAt > DateTime.UtcNow))
            .OrderBy(Request => Request.RequestedAt)
            .ToListAsync(cancellationToken);

    public async Task<PassengerRideRequest> AddAsync(PassengerRideRequest Request, CancellationToken cancellationToken = default)
    {
        await _context.PassengerRideRequests.AddAsync(Request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Request;
    }

    public async Task<bool> UpdateStatusAsync(Guid requestId, string Status, CancellationToken cancellationToken = default)
    {
        var Request = await _context.PassengerRideRequests.FirstOrDefaultAsync(Request => Request.RequestId == requestId, cancellationToken);
        if (Request is null)
        {
            return false;
        }

        Request.Status = Status;
        Request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
