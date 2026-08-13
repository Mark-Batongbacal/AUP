using backend.Models.Database;

namespace backend.Repositories;

public interface IFareRuleRepository
{
    Task<List<fare_rule>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<List<fare_rule>> GetActiveByRouteAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<fare_rule>> GetActiveByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<fare_rule?> GetCurrentlyEffectiveAsync(
        short transportModeId,
        Guid? routeId = null,
        DateOnly? effectiveOn = null,
        CancellationToken cancellationToken = default);

    Task<fare_rule?> GetByIdAsync(Guid fareRuleId, CancellationToken cancellationToken = default);
}
