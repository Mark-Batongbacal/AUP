using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class TripLandmarkCandidateRepository(TukiDbContext context)
    : ITripLandmarkCandidateRepository
{
    public async Task ReplaceAsync(Guid sessionId, IReadOnlyList<TripLandmarkCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var existing = await context.TripLandmarkCandidates
            .Where(item => item.TripSessionId == sessionId).ToListAsync(cancellationToken);
        context.TripLandmarkCandidates.RemoveRange(existing);
        await context.TripLandmarkCandidates.AddRangeAsync(candidates, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<TripLandmarkCandidate>> GetCrossedAsync(
        Guid sessionId, int legIndex, double previousProgress, double currentProgress,
        CancellationToken cancellationToken = default) =>
        context.TripLandmarkCandidates.AsNoTracking()
            .Where(item => item.TripSessionId == sessionId && item.LegIndex == legIndex &&
                item.Role == LandmarkRole.ProgressReference &&
                item.TriggeredAt == null &&
                item.DistanceFromRouteStartMeters >= previousProgress - item.TriggerAfterMeters &&
                item.DistanceFromRouteStartMeters <= currentProgress + item.TriggerBeforeMeters)
            .OrderBy(item => item.DistanceFromRouteStartMeters).ToListAsync(cancellationToken);

    public Task<List<TripLandmarkCandidate>> GetForLegAsync(
        Guid sessionId, int legIndex, CancellationToken cancellationToken = default) =>
        context.TripLandmarkCandidates.AsNoTracking()
            .Where(item => item.TripSessionId == sessionId && item.LegIndex == legIndex)
            .OrderBy(item => item.Role)
            .ThenBy(item => item.DistanceFromTargetMeters)
            .ToListAsync(cancellationToken);

    public async Task MarkTriggeredAsync(Guid candidateId, DateTime triggeredAt, CancellationToken cancellationToken = default)
    {
        await context.TripLandmarkCandidates.Where(item => item.TripLandmarkCandidateId == candidateId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.TriggeredAt, triggeredAt), cancellationToken);
    }
}
