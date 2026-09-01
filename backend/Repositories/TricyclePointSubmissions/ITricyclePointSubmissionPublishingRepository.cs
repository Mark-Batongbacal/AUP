using backend.Models.TricyclePointSubmissions;

namespace backend.Repositories;

public interface ITricyclePointSubmissionPublishingRepository
{
    Task<TricyclePointSubmissionPublishResult> PublishAsync(
        long submissionId,
        Guid adminUserId,
        TricyclePointPublicationDraft draft,
        CancellationToken cancellationToken = default);
}
