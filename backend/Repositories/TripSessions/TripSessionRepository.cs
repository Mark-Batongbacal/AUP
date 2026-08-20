using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class TripSessionRepository(TukiDbContext context) : ITripSessionRepository
{
    public async Task<TripSession> AddAsync(TripSession session, CancellationToken cancellationToken = default)
    {
        await context.TripSessions.AddAsync(session, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public Task<TripSession?> GetOwnedAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        context.TripSessions.AsNoTracking().FirstOrDefaultAsync(
            session => session.TripSessionId == sessionId && session.UserId == userId,
            cancellationToken);

    public Task<TripSession?> GetActiveOwnedAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.TripSessions.AsNoTracking()
            .Where(session => session.UserId == userId &&
                session.CurrentNavigationState != TripNavigationState.Arrived &&
                session.CurrentNavigationState != TripNavigationState.Cancelled)
            .OrderByDescending(session => session.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<TripSession>> GetOwnedHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.TripSessions.AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.StartedAt ?? session.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<TripSession>> GetOwnedRecentHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.TripSessions.AsNoTracking()
            .Where(session => session.UserId == userId &&
                ((session.CurrentNavigationState == TripNavigationState.Arrived &&
                    session.CompletedAt != null) ||
                 (session.CurrentNavigationState == TripNavigationState.Cancelled &&
                    session.CancelledAt != null)))
            .OrderByDescending(session =>
                session.CompletedAt ?? session.CancelledAt ?? session.StartedAt ?? session.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<int> CountCompletedByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        context.TripSessions
            .AsNoTracking()
            .CountAsync(
                session => session.UserId == userId &&
                    session.CurrentNavigationState == TripNavigationState.Arrived &&
                    session.CompletedAt != null,
                cancellationToken);

    public async Task<TripSession> UpdateAsync(
        TripSession session, CancellationToken cancellationToken = default)
    {
        var tracked = context.TripSessions.Local.FirstOrDefault(
            item => item.TripSessionId == session.TripSessionId);
        if (tracked is null)
        {
            context.TripSessions.Update(session);
            tracked = session;
        }
        else if (!ReferenceEquals(tracked, session))
        {
            context.Entry(tracked).CurrentValues.SetValues(session);
        }
        await context.SaveChangesAsync(cancellationToken);
        return tracked;
    }
}
