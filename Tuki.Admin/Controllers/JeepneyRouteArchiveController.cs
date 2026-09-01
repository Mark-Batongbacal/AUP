using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Repositories.JeepneyRoutes;
using Tuki.Admin.ViewModels.JeepneyRoutes;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class JeepneyRouteArchiveController(IAdminJeepneyRouteRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var result = await repository.GetArchivedAsync(cancellationToken);
        return View(new JeepneyRouteListViewModel
        {
            Routes = result.Value ?? [],
            IncludeArchived = true,
            ErrorMessage = result.Succeeded ? TempData["JeepneyRouteError"] as string : result.ErrorMessage,
            SuccessMessage = TempData["JeepneyRouteSuccess"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.ArchiveAsync(id, cancellationToken);
        TempData[result.Succeeded ? "JeepneyRouteSuccess" : "JeepneyRouteError"] =
            result.Succeeded
                ? "Jeepney route archived. Its previous published/draft state is preserved for restore."
                : result.ErrorMessage ?? "Unable to archive the jeepney route.";

        return RedirectToAction("Index", "JeepneyRoutes");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.RestoreAsync(id, cancellationToken);
        TempData[result.Succeeded ? "JeepneyRouteSuccess" : "JeepneyRouteError"] =
            result.Succeeded
                ? "Jeepney route restored to its previous published/draft state."
                : result.ErrorMessage ?? "Unable to restore the jeepney route.";

        return RedirectToAction(nameof(Index));
    }
}
