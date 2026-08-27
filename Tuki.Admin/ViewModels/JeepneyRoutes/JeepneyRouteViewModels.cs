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
    public AdminJeepneyRoutePublishReadiness? PublishReadiness { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public bool IsCreate => RouteId is null;
    public bool CanEdit => Route is null || !Route.IsActive;
}

public sealed class JeepneyRoutePlotViewModel
{
    public AdminJeepneyRoute Route { get; init; } = null!;
    public AdminJeepneyRouteGeometry Geometry { get; init; } = null!;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public bool CanEdit => !Route.IsActive;
}

public sealed class JeepneyRoutePlotPostModel
{
    public string PointsJson { get; set; } = "[]";
}

public sealed class JeepneyRouteValhallaViewModel
{
    public AdminJeepneyRoute Route { get; init; } = null!;
    public AdminJeepneyRouteGeometry Geometry { get; init; } = null!;
    public string WaypointsText { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public bool CanEdit => !Route.IsActive;
}

public sealed class JeepneyRouteValhallaPostModel
{
    public string WaypointsText { get; set; } = string.Empty;
}
