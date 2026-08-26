using Tuki.Admin.Models.JeepneyRoutes;

namespace Tuki.Admin.ViewModels.JeepneyRoutes;

public sealed class JeepneyRouteListViewModel
{
    public IReadOnlyList<AdminJeepneyRoute> Routes { get; init; } = [];
    public bool IncludeActive { get; init; } = true;
    public bool IncludeDrafts { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
}

public sealed class JeepneyRouteEditViewModel
{
    public long? RouteId { get; init; }
    public AdminJeepneyRouteRequest Request { get; init; } = new();
    public AdminJeepneyRoute? Route { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public bool IsCreate => RouteId is null;
    public bool CanEdit => Route is null || !Route.IsActive;
}
