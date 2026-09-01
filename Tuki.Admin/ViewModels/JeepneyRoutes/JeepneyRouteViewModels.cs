using Tuki.Admin.Models.JeepneyRoutes;

namespace Tuki.Admin.ViewModels.JeepneyRoutes;

public sealed class JeepneyRouteListViewModel
{
    public IReadOnlyList<AdminJeepneyRoute> Routes { get; init; } = [];
    public bool IncludeActive { get; init; } = true;
    public bool IncludeDrafts { get; init; } = true;
    public bool IncludeArchived { get; init; }
    public string Status => IncludeArchived ? "archived" : IncludeActive && !IncludeDrafts ? "published" : !IncludeActive && IncludeDrafts ? "draft" : "all";
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
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
    public bool CanEdit => Route is null || (!Route.IsActive && !Route.IsArchived);
}

public sealed class JeepneyRoutePlotViewModel
{
    public AdminJeepneyRoute Route { get; init; } = null!;
    public AdminJeepneyRouteGeometry Geometry { get; init; } = null!;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public bool CanEdit => !Route.IsActive && !Route.IsArchived;
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
    public bool CanEdit => !Route.IsActive && !Route.IsArchived;
}

public sealed class JeepneyRouteValhallaPostModel
{
    public string WaypointsText { get; set; } = string.Empty;
}
