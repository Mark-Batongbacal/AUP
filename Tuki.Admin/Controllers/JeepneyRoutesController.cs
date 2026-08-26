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

        TempData["JeepneyRouteSuccess"] = "Jeepney route draft created. Add route geometry in the route plotter before publishing.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.RouteId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status404NotFound || result.Value is null)
            return NotFound();

        var route = result.Value;
        return View(new JeepneyRouteEditViewModel
        {
            RouteId = route.RouteId,
            Route = route,
            Request = MapRequest(route),
            ErrorMessage = TempData["JeepneyRouteError"] as string,
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
            return View(model.WithId(id));

        var result = await repository.UpdateDraftAsync(id, model.Request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to update the jeepney route draft.");
            var current = await repository.GetByIdAsync(id, cancellationToken);
            return View(model.WithId(id, current.Value));
        }

        TempData["JeepneyRouteSuccess"] = "Jeepney route draft metadata updated.";
        return RedirectToAction(nameof(Edit), new { id });
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
        AdminJeepneyRoute? route = null) => new()
    {
        RouteId = id,
        Route = route ?? model.Route,
        Request = model.Request,
        ErrorMessage = model.ErrorMessage,
        SuccessMessage = model.SuccessMessage
    };
}
