using backend.Models.Database;

namespace backend.Repositories;

public interface ITricyclePointRepository
{
    Task<List<TricyclePoint>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<TricyclePoint?> GetByIdAsync(long tricyclePointId, CancellationToken cancellationToken = default);

    Task<TricyclePoint?> GetByPointCodeAsync(string pointCode, CancellationToken cancellationToken = default);

    Task<TricyclePoint?> GetByStopIdAsync(long stopId, CancellationToken cancellationToken = default);

    Task<TricyclePoint> AddAsync(TricyclePoint tricyclePoint, CancellationToken cancellationToken = default);

    Task<TricyclePoint> UpdateAsync(TricyclePoint tricyclePoint, CancellationToken cancellationToken = default);
}
