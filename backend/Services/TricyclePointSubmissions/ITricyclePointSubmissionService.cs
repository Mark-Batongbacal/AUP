using backend.Models.TricyclePointSubmissions;

namespace backend.Services;

public interface ITricyclePointSubmissionService
{
    Task<TricyclePointSubmissionMutationResult> CreateAsync(
        Guid userId,
        CreateTricyclePointSubmissionRequest request,
        CancellationToken cancellationToken = default);

    Task<TricyclePointSubmissionResponse?> GetByIdForUserAsync(
        Guid userId,
        long submissionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TricyclePointSubmissionResponse>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record TricyclePointSubmissionMutationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors,
    TricyclePointSubmissionResponse? Submission)
{
    public static TricyclePointSubmissionMutationResult Success(TricyclePointSubmissionResponse submission) =>
        new(true, [], submission);

    public static TricyclePointSubmissionMutationResult Invalid(params string[] errors) =>
        new(false, errors, null);
}
