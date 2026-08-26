using Tuki.Admin.Models.JeepneyRoutes;

namespace Tuki.Admin.Repositories.JeepneyRoutes;

public interface IAdminJeepneyRouteRepository
{
    Task<AdminJeepneyRepositoryResult<IReadOnlyList<AdminJeepneyRoute>>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> GetByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> CreateDraftAsync(
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> UpdateDraftAsync(
        long routeId,
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default);
}
