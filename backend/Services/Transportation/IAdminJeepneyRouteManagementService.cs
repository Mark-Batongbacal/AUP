using backend.Models.JeepneyRouteManagement;

namespace backend.Services.Transportation;

public interface IAdminJeepneyRouteManagementService
{
    Task<IReadOnlyList<AdminJeepneyRouteResponse>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteResponse?> GetByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteGeometryResponse?> GetGeometryAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRoutePublishReadinessResponse?> GetPublishReadinessAsync(
        long routeId,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteMutationResult> CreateDraftAsync(
        AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteMutationResult> UpdateDraftAsync(
        long routeId,
        AdminJeepneyRouteMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteGeometryMutationResult> ReplaceDraftGeometryAsync(
        long routeId,
        AdminJeepneyRouteGeometryRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminJeepneyRouteMutationResult> PublishDraftAsync(
        long routeId,
        CancellationToken cancellationToken = default);
}
