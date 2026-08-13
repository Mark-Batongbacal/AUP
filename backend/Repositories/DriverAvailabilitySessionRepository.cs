using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for driver availability sessions. Available sessions are active rows whose status is
/// AVAILABLE.
/// </summary>
public sealed class DriverAvailabilitySessionRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;
    private const string AvailableStatus = "AVAILABLE";
    private const string EndedStatus = "ENDED";

    public Task<driver_availability_session?> GetActiveByDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        _context.driver_availability_sessions
            .AsNoTracking()
            .Include(session => session.vehicle)
            .Include(session => session.destination_stop)
            .FirstOrDefaultAsync(
                session => session.driver_id == driverId && session.status == AvailableStatus && session.ended_at == null,
                cancellationToken);

    public Task<List<driver_availability_session>> GetAvailableSessionsAsync(CancellationToken cancellationToken = default) =>
        _context.driver_availability_sessions
            .AsNoTracking()
            .Include(session => session.driver)
            .Include(session => session.vehicle)
            .Include(session => session.destination_stop)
            .Where(session => session.status == AvailableStatus && session.ended_at == null)
            .OrderBy(session => session.started_at)
            .ToListAsync(cancellationToken);

    public Task<driver_availability_session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _context.driver_availability_sessions
            .AsNoTracking()
            .Include(session => session.driver)
            .Include(session => session.vehicle)
            .Include(session => session.destination_stop)
            .FirstOrDefaultAsync(session => session.session_id == sessionId, cancellationToken);

    public async Task<driver_availability_session> AddAsync(driver_availability_session session, CancellationToken cancellationToken = default)
    {
        await _context.driver_availability_sessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> UpdateStatusAsync(Guid sessionId, string status, CancellationToken cancellationToken = default)
    {
        var session = await _context.driver_availability_sessions.FirstOrDefaultAsync(session => session.session_id == sessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.status = status;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> EndSessionAsync(Guid sessionId, DateTime? endedAt = null, CancellationToken cancellationToken = default)
    {
        var session = await _context.driver_availability_sessions.FirstOrDefaultAsync(session => session.session_id == sessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.status = EndedStatus;
        session.ended_at = endedAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
