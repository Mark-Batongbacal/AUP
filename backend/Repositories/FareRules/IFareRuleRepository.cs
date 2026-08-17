using backend.Models.Database;

namespace backend.Repositories;

public interface IFareRuleRepository
{
    Task<List<FareRule>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<List<FareRule>> GetActiveByRouteAsync(long routeId, CancellationToken cancellationToken = default);

    Task<List<FareRule>> GetActiveByTransportModeAsync(int transportModeId, CancellationToken cancellationToken = default);

    Task<FareRule?> GetCurrentlyEffectiveAsync(
        int transportModeId,
        long? routeId = null,
        DateOnly? effectiveOn = null,
        CancellationToken cancellationToken = default);

    Task<FareRule?> GetByIdAsync(long fareRuleId, CancellationToken cancellationToken = default);
}
