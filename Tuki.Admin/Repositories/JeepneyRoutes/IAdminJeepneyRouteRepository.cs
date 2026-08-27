using Tuki.Admin.Models.JeepneyRoutes;

namespace Tuki.Admin.Repositories.JeepneyRoutes;

public interface IAdminJeepneyRouteRepository
{
    Task<AdminJeepneyRepositoryResult<IReadOnlyList<AdminJeepneyRoute>>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<IReadOnlyList<AdminJeepneyRoute>>> GetArchivedAsync(
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> GetByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRouteGeometry>> GetGeometryAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoutePublishReadiness>> GetPublishReadinessAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> CreateDraftAsync(
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> UpdateDraftAsync(
        long routeId,
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRouteGeometry>> ReplaceDraftGeometryAsync(
        long routeId,
        AdminJeepneyRouteGeometryRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyValhallaPreview>> PreviewValhallaAsync(
        long routeId,
        AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRouteGeometry>> SaveValhallaGeometryAsync(
        long routeId,
        AdminJeepneyValhallaRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> PublishAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<bool>> ArchiveAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRepositoryResult<bool>> RestoreAsync(
        long routeId,
        CancellationToken cancellationToken = default);
}
