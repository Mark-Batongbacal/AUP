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

    Task<(List<TricyclePointSubmission> Items, int TotalCount)> GetForAdminAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TricyclePointSubmission?> GetTrackedByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
