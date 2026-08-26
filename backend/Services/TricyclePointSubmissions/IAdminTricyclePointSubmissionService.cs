using backend.Models.TricyclePointSubmissions;

namespace backend.Services;

public interface IAdminTricyclePointSubmissionService
{
    Task<AdminTricyclePointSubmissionPageResponse> GetPageAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointSubmissionResponse?> GetByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointSubmissionMutationResult> UpdateReviewAsync(
        Guid adminUserId,
        long submissionId,
        UpdateAdminTricyclePointSubmissionReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointSubmissionMutationResult> RejectAsync(
        Guid adminUserId,
        long submissionId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointSubmissionMutationResult> MarkNeedsChangesAsync(
        Guid adminUserId,
        long submissionId,
        string reason,
        CancellationToken cancellationToken = default);
}
