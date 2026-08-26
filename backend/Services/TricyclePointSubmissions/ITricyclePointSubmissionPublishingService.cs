using backend.Models.TricyclePointSubmissions;

namespace backend.Services;

public interface ITricyclePointSubmissionPublishingService
{
    Task<TricyclePointSubmissionPublishResult> PublishAsync(
        Guid adminUserId,
        long submissionId,
        CancellationToken cancellationToken = default);
}
