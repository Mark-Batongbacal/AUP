using backend.Models.Database;

namespace backend.Repositories;

public interface ITripLandmarkCandidateRepository
{
    Task ReplaceAsync(Guid sessionId, IReadOnlyList<TripLandmarkCandidate> candidates, CancellationToken cancellationToken = default);
    Task<List<TripLandmarkCandidate>> GetCrossedAsync(Guid sessionId, int legIndex, double previousProgress, double currentProgress, CancellationToken cancellationToken = default);
    Task<List<TripLandmarkCandidate>> GetForLegAsync(Guid sessionId, int legIndex, CancellationToken cancellationToken = default);
    Task MarkTriggeredAsync(Guid candidateId, DateTime triggeredAt, CancellationToken cancellationToken = default);
}
