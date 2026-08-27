using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Models.JeepneyRoutes;
using Tuki.Admin.Repositories.JeepneyRoutes;
using Tuki.Admin.ViewModels.JeepneyRoutes;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class JeepneyRoutesController(IAdminJeepneyRouteRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default)
    {
        var result = await repository.GetAllAsync(includeActive, includeDrafts, cancellationToken);
        return View(new JeepneyRouteListViewModel
        {
            Routes = result.Value ?? [],
            IncludeActive = includeActive,
            IncludeDrafts = includeDrafts,
            ErrorMessage = result.Succeeded ? TempData["JeepneyRouteError"] as string : result.ErrorMessage,
            SuccessMessage = TempData["JeepneyRouteSuccess"] as string
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new JeepneyRouteEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        JeepneyRouteEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return View("Edit", model);

        var result = await repository.CreateDraftAsync(model.Request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to create the jeepney route draft.");
            return View("Edit", model);
        }

        TempData["JeepneyRouteSuccess"] = "Jeepney route draft created. Add route geometry before publishing.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.RouteId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status404NotFound || result.Value is null)
            return NotFound();

        var readinessResult = await repository.GetPublishReadinessAsync(id, cancellationToken);
        var route = result.Value;
        return View(new JeepneyRouteEditViewModel
        {
            RouteId = route.RouteId,
            Route = route,
            PublishReadiness = readinessResult.Value,
            Request = MapRequest(route),
            ErrorMessage = TempData["JeepneyRouteError"] as string ?? (!readinessResult.Succeeded ? readinessResult.ErrorMessage : null),
            SuccessMessage = TempData["JeepneyRouteSuccess"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        long id,
        JeepneyRouteEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var current = await repository.GetByIdAsync(id, cancellationToken);
            var readiness = await repository.GetPublishReadinessAsync(id, cancellationToken);
            return View(model.WithId(id, current.Value, readiness.Value));
        }

        var result = await repository.UpdateDraftAsync(id, model.Request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to update the jeepney route draft.");
            var current = await repository.GetByIdAsync(id, cancellationToken);
            var readiness = await repository.GetPublishReadinessAsync(id, cancellationToken);
            return View(model.WithId(id, current.Value, readiness.Value));
        }

        TempData["JeepneyRouteSuccess"] = "Jeepney route draft metadata updated.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Plot(long id, CancellationToken cancellationToken = default)
    {
        var routeResult = await repository.GetByIdAsync(id, cancellationToken);
        var geometryResult = await repository.GetGeometryAsync(id, cancellationToken);
        if (routeResult.Value is null || geometryResult.Value is null)
        {
            if (routeResult.StatusCode == StatusCodes.Status404NotFound ||
                geometryResult.StatusCode == StatusCodes.Status404NotFound)
                return NotFound();

            TempData["JeepneyRouteError"] = routeResult.ErrorMessage ?? geometryResult.ErrorMessage ?? "Unable to load route geometry.";
            return RedirectToAction(nameof(Index));
        }

        return View(new JeepneyRoutePlotViewModel
        {
            Route = routeResult.Value,
            Geometry = geometryResult.Value,
            ErrorMessage = TempData["JeepneyRouteError"] as string,
            SuccessMessage = TempData["JeepneyRouteSuccess"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Plot(
        long id,
        JeepneyRoutePlotPostModel model,
        CancellationToken cancellationToken = default)
    {
        List<AdminJeepneyRouteGeometryPointRequest>? points;
        try
        {
            points = JsonSerializer.Deserialize<List<AdminJeepneyRouteGeometryPointRequest>>(
                model.PointsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            points = null;
        }

        if (points is null || points.Count < 2)
        {
            TempData["JeepneyRouteError"] = "Plot at least two valid route points before saving.";
            return RedirectToAction(nameof(Plot), new { id });
        }

        var result = await repository.ReplaceDraftGeometryAsync(
            id,
            new AdminJeepneyRouteGeometryRequest { Points = points },
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            TempData["JeepneyRouteError"] = result.ErrorMessage ?? "Unable to save route geometry.";
            return RedirectToAction(nameof(Plot), new { id });
        }

        TempData["JeepneyRouteSuccess"] = $"Route geometry saved with {result.Value.Points.Count} ordered points. The route remains an inactive draft.";
        return RedirectToAction(nameof(Plot), new { id });
    }

    [HttpGet]
    public Task<IActionResult> Valhalla(long id, CancellationToken cancellationToken = default) =>
        RenderValhallaAsync(
            id,
            string.Empty,
            TempData["JeepneyRouteError"] as string,
            TempData["JeepneyRouteSuccess"] as string,
            cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValhallaPreview(
        long id,
        JeepneyRouteValhallaPostModel model,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseWaypoints(model.WaypointsText, out var waypoints, out var error))
            return BadRequest(new { error });

        var result = await repository.PreviewValhallaAsync(
            id,
            new AdminJeepneyValhallaRequest { Waypoints = waypoints },
            cancellationToken);

        return result.Succeeded && result.Value is not null
            ? Json(result.Value)
            : StatusCode(result.StatusCode, new { error = result.ErrorMessage ?? "Unable to generate a Valhalla preview." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveValhalla(
        long id,
        JeepneyRouteValhallaPostModel model,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseWaypoints(model.WaypointsText, out var waypoints, out var error))
            return await RenderValhallaAsync(id, model.WaypointsText, error, null, cancellationToken);

        var result = await repository.SaveValhallaGeometryAsync(
            id,
            new AdminJeepneyValhallaRequest { Waypoints = waypoints },
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
            return await RenderValhallaAsync(
                id,
                model.WaypointsText,
                result.ErrorMessage ?? "Unable to save the Valhalla-generated geometry.",
                null,
                cancellationToken);

        TempData["JeepneyRouteSuccess"] =
            $"Valhalla route accepted and saved with {waypoints.Count} waypoints and {result.Value.Points.Count} generated route points.";
        return RedirectToAction(nameof(Plot), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.PublishAsync(id, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["JeepneyRouteError"] = result.ErrorMessage ?? "Unable to publish the jeepney route.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["JeepneyRouteSuccess"] = "Jeepney route published successfully. It is now available to active passenger routing.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<IActionResult> RenderValhallaAsync(
        long id,
        string waypointsText,
        string? errorMessage,
        string? successMessage,
        CancellationToken cancellationToken)
    {
        var routeResult = await repository.GetByIdAsync(id, cancellationToken);
        var geometryResult = await repository.GetGeometryAsync(id, cancellationToken);
        if (routeResult.Value is null || geometryResult.Value is null)
        {
            if (routeResult.StatusCode == StatusCodes.Status404NotFound ||
                geometryResult.StatusCode == StatusCodes.Status404NotFound)
                return NotFound();

            TempData["JeepneyRouteError"] = routeResult.ErrorMessage ?? geometryResult.ErrorMessage ?? "Unable to load the route for Valhalla preview.";
            return RedirectToAction(nameof(Index));
        }

        return View("Valhalla", new JeepneyRouteValhallaViewModel
        {
            Route = routeResult.Value,
            Geometry = geometryResult.Value,
            WaypointsText = waypointsText,
            ErrorMessage = errorMessage,
            SuccessMessage = successMessage
        });
    }

    private static bool TryParseWaypoints(
        string? text,
        out List<AdminJeepneyRouteGeometryPointRequest> waypoints,
        out string? error)
    {
        waypoints = [];
        error = null;
        var lines = (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length < 2)
        {
            error = "Paste at least two ordered latitude, longitude waypoint pairs.";
            return false;
        }

        if (lines.Length > 100)
        {
            error = "Use at most 100 ordered waypoints. Paste selected route anchors, not every generated route point.";
            return false;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var pieces = lines[index]
                .Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pieces.Length != 2 ||
                !double.TryParse(pieces[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(pieces[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                error = $"Waypoint line {index + 1} must contain exactly: latitude, longitude.";
                waypoints = [];
                return false;
            }

            if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
                !double.IsFinite(longitude) || longitude is < -180 or > 180)
            {
                error = $"Waypoint line {index + 1} contains an invalid latitude or longitude.";
                waypoints = [];
                return false;
            }

            waypoints.Add(new AdminJeepneyRouteGeometryPointRequest
            {
                Latitude = latitude,
                Longitude = longitude
            });
        }

        return true;
    }

    private static AdminJeepneyRouteRequest MapRequest(AdminJeepneyRoute route) => new()
    {
        RouteCode = route.RouteCode,
        RouteName = route.RouteName,
        OriginName = route.OriginName,
        DestinationName = route.DestinationName,
        DirectionName = route.DirectionName,
        OperatorName = route.OperatorName,
        Description = route.Description,
        BaseFare = route.BaseFare
    };
}

internal static class JeepneyRouteEditViewModelExtensions
{
    public static JeepneyRouteEditViewModel WithId(
        this JeepneyRouteEditViewModel model,
        long id,
        AdminJeepneyRoute? route = null,
        AdminJeepneyRoutePublishReadiness? publishReadiness = null) => new()
    {
        RouteId = id,
        Route = route ?? model.Route,
        PublishReadiness = publishReadiness ?? model.PublishReadiness,
        Request = model.Request,
        ErrorMessage = model.ErrorMessage,
        SuccessMessage = model.SuccessMessage
    };
}
