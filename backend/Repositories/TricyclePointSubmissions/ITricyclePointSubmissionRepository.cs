using backend.Models.Database;

namespace backend.Repositories;

public interface ITricyclePointSubmissionRepository
{
    Task<TricyclePointSubmission> AddAsync(
        TricyclePointSubmission submission,
        CancellationToken cancellationToken = default);

    Task<TricyclePointSubmission?> GetByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default);

    Task<List<TricyclePointSubmission>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
