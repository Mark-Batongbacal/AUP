using Tuki.Admin.Models.TricycleSubmissions;

namespace Tuki.Admin.Repositories.TricycleSubmissions;

public interface IAdminTricycleSubmissionRepository
{
    Task<AdminRepositoryResult<AdminTricycleSubmissionPage>> GetPageAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<AdminTricycleSubmission>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<AdminTricycleSubmission>> UpdateReviewAsync(
        long id,
        AdminTricycleReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<AdminTricyclePublication>> ApproveAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<AdminTricycleSubmission>> RejectAsync(
        long id,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<AdminTricycleSubmission>> NeedsChangesAsync(
        long id,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AdminRepositoryResult<ProofImageContent>> GetProofAsync(
        string proofImageUrl,
        CancellationToken cancellationToken = default);
}
