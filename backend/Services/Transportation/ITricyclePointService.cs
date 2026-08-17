using backend.Models.Database;

namespace backend.Services.Transportation;

public interface ITricyclePointService
{
    Task<List<TricyclePoint>> GetAllActivePointsAsync(CancellationToken cancellationToken = default);

    Task<TricyclePoint?> GetPointByIdAsync(
        long tricyclePointId,
        CancellationToken cancellationToken = default);

    Task<TricyclePoint?> GetPointByCodeAsync(
        string pointCode,
        CancellationToken cancellationToken = default);

    bool IsLocationInsideTricyclePointRadius(
        TricyclePoint? tricyclePoint,
        double latitude,
        double longitude);

    Task<List<TricyclePoint>> GetActivePointsCoveringLocationAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    Task<TricyclePointMutationResult> AddVerifiedTricyclePointAsync(
        string pointCode,
        string pointName,
        double centerLatitude,
        double centerLongitude,
        int radiusMeters,
        long? stopId = null,
        string? description = null,
        string? address = null,
        string? operatorName = null,
        decimal? baseFare = null,
        decimal? farePerKilometer = null,
        int? averageWaitingTimeSeconds = null,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        bool isActive = true,
        CancellationToken cancellationToken = default);

    Task<TricyclePointMutationResult> UpdateVerifiedTricyclePointAsync(
        long tricyclePointId,
        string pointCode,
        string pointName,
        double centerLatitude,
        double centerLongitude,
        int radiusMeters,
        long? stopId = null,
        string? description = null,
        string? address = null,
        string? operatorName = null,
        decimal? baseFare = null,
        decimal? farePerKilometer = null,
        int? averageWaitingTimeSeconds = null,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        bool isActive = true,
        CancellationToken cancellationToken = default);
}

public enum TricyclePointMutationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Duplicate,
}

public sealed record TricyclePointMutationResult(
    TricyclePointMutationStatus Status,
    IReadOnlyList<string> Errors,
    TricyclePoint? TricyclePoint)
{
    public static TricyclePointMutationResult Success(TricyclePoint tricyclePoint) =>
        new(TricyclePointMutationStatus.Success, [], tricyclePoint);

    public static TricyclePointMutationResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(TricyclePointMutationStatus.ValidationFailed, errors, null);

    public static TricyclePointMutationResult NotFound(long tricyclePointId) =>
        new(
            TricyclePointMutationStatus.NotFound,
            [$"Tricycle point {tricyclePointId} was not found."],
            null);

    public static TricyclePointMutationResult Duplicate(IReadOnlyList<string> errors) =>
        new(TricyclePointMutationStatus.Duplicate, errors, null);
}
