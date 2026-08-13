using backend.Models.Database;

namespace backend.Repositories;

public interface IFareRuleRepository
{
    Task<List<FareRule>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<List<FareRule>> GetActiveByRouteAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<List<FareRule>> GetActiveByTransportModeAsync(short transportModeId, CancellationToken cancellationToken = default);

    Task<FareRule?> GetCurrentlyEffectiveAsync(
        short transportModeId,
        Guid? routeId = null,
        DateOnly? effectiveOn = null,
        CancellationToken cancellationToken = default);

    Task<FareRule?> GetByIdAsync(Guid fareRuleId, CancellationToken cancellationToken = default);
}
