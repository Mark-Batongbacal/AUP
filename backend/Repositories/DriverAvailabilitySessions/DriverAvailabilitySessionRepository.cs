using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for Driver availability sessions. Available sessions are active rows whose Status is
/// AVAILABLE.
/// </summary>
public sealed class DriverAvailabilitySessionRepository(TukiDbContext context) : IDriverAvailabilitySessionRepository
{
    private readonly TukiDbContext _context = context;
    private const string AvailableStatus = "AVAILABLE";
    private const string EndedStatus = "ENDED";

    public Task<DriverAvailabilitySession?> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.DriverAvailabilitySessions
            .AsNoTracking()
            .Include(Session => Session.Vehicle)
            .FirstOrDefaultAsync(
                Session => Session.DriverId == driverId && Session.Status == AvailableStatus && Session.EndedAt == null,
                cancellationToken);

    public Task<List<DriverAvailabilitySession>> GetAvailableSessionsAsync(CancellationToken cancellationToken = default) =>
        _context.DriverAvailabilitySessions
            .AsNoTracking()
            .Include(Session => Session.Driver)
            .Include(Session => Session.Vehicle)
            .Where(Session => Session.Status == AvailableStatus && Session.EndedAt == null)
            .OrderBy(Session => Session.StartedAt)
            .ToListAsync(cancellationToken);

    public Task<DriverAvailabilitySession?> GetByIdAsync(long sessionId, CancellationToken cancellationToken = default) =>
        _context.DriverAvailabilitySessions
            .AsNoTracking()
            .Include(Session => Session.Driver)
            .Include(Session => Session.Vehicle)
            .FirstOrDefaultAsync(Session => Session.SessionId == sessionId, cancellationToken);

    public async Task<DriverAvailabilitySession> AddAsync(DriverAvailabilitySession Session, CancellationToken cancellationToken = default)
    {
        await _context.DriverAvailabilitySessions.AddAsync(Session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Session;
    }

    public async Task<bool> UpdateStatusAsync(long sessionId, string Status, CancellationToken cancellationToken = default)
    {
        var Session = await _context.DriverAvailabilitySessions.FirstOrDefaultAsync(Session => Session.SessionId == sessionId, cancellationToken);
        if (Session is null)
        {
            return false;
        }

        Session.Status = Status;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> EndSessionAsync(long sessionId, DateTime? endedAt = null, CancellationToken cancellationToken = default)
    {
        var Session = await _context.DriverAvailabilitySessions.FirstOrDefaultAsync(Session => Session.SessionId == sessionId, cancellationToken);
        if (Session is null)
        {
            return false;
        }

        Session.Status = EndedStatus;
        Session.EndedAt = endedAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
