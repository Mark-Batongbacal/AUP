using Tuki.Admin.Models.TricyclePoints;

namespace Tuki.Admin.Repositories.TricyclePoints;

public interface IAdminTricyclePointRepository
{
    Task<AdminPointRepositoryResult<IReadOnlyList<AdminTricyclePoint>>> GetAllAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<AdminTricyclePoint>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<IReadOnlyList<TricyclePointDuplicateWarning>>> GetDuplicatesAsync(
        double latitude,
        double longitude,
        long? excludeId = null,
        double thresholdMeters = 75,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> CreateAsync(
        AdminTricyclePointRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> UpdateAsync(
        long id,
        AdminTricyclePointRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> ArchiveAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> RestoreAsync(
        long id,
        CancellationToken cancellationToken = default);
}
