using backend.Models.TricyclePointManagement;

namespace backend.Services.Transportation;

public interface IAdminTricyclePointManagementService
{
    Task<IReadOnlyList<AdminTricyclePointResponse>> GetAllAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointResponse?> GetByIdAsync(
        long tricyclePointId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TricyclePointDuplicateWarning>> GetDuplicateWarningsAsync(
        double latitude,
        double longitude,
        long? excludeTricyclePointId = null,
        double thresholdMeters = 75,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointMutationResult> CreateAsync(
        AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointMutationResult> UpdateAsync(
        long tricyclePointId,
        AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTricyclePointMutationResult> SetActiveAsync(
        long tricyclePointId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
