using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Models.TricyclePoints;
using Tuki.Admin.Repositories.TricyclePoints;
using Tuki.Admin.ViewModels.TricyclePoints;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class TricyclePointsController(IAdminTricyclePointRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool includeArchived = true, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetAllAsync(includeArchived, cancellationToken);
        return View(new TricyclePointListViewModel
        {
            Points = result.Value ?? [],
            IncludeArchived = includeArchived,
            ErrorMessage = result.Succeeded ? null : result.ErrorMessage
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new TricyclePointEditViewModel
    {
        Request = new AdminTricyclePointRequest { RadiusMeters = 500, IsActive = true }
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TricyclePointEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || model.Request.Latitude is null || model.Request.Longitude is null)
            return View("Edit", model);

        var result = await repository.CreateAsync(model.Request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to create the tricycle point.");
            return View("Edit", model);
        }

        TempData["PointSuccess"] = "Official tricycle point created.";
        return RedirectToAction(nameof(Edit), new { id = result.Value.Point.TricyclePointId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status404NotFound || result.Value is null)
            return NotFound();

        var point = result.Value;
        var warnings = await repository.GetDuplicatesAsync(
            point.Latitude,
            point.Longitude,
            point.TricyclePointId,
            cancellationToken: cancellationToken);

        return View(new TricyclePointEditViewModel
        {
            TricyclePointId = id,
            Request = MapRequest(point),
            DuplicateWarnings = warnings.Value ?? [],
            SuccessMessage = TempData["PointSuccess"] as string,
            ErrorMessage = TempData["PointError"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        long id,
        TricyclePointEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || model.Request.Latitude is null || model.Request.Longitude is null)
            return View(model.WithId(id));

        var result = await repository.UpdateAsync(id, model.Request, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to update the tricycle point.");
            return View(model.WithId(id));
        }

        TempData["PointSuccess"] = "Official tricycle point updated.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.ArchiveAsync(id, cancellationToken);
        TempData[result.Succeeded ? "PointSuccess" : "PointError"] =
            result.Succeeded ? "Tricycle point archived." : result.ErrorMessage ?? "Unable to archive tricycle point.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.RestoreAsync(id, cancellationToken);
        TempData[result.Succeeded ? "PointSuccess" : "PointError"] =
            result.Succeeded ? "Tricycle point restored." : result.ErrorMessage ?? "Unable to restore tricycle point.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Duplicates(
        double latitude,
        double longitude,
        long? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await repository.GetDuplicatesAsync(latitude, longitude, excludeId, cancellationToken: cancellationToken);
        return result.Succeeded ? Json(result.Value ?? []) : BadRequest(new { error = result.ErrorMessage });
    }

    private static AdminTricyclePointRequest MapRequest(AdminTricyclePoint point) => new()
    {
        PointCode = point.PointCode,
        PointName = point.PointName,
        Latitude = point.Latitude,
        Longitude = point.Longitude,
        RadiusMeters = point.RadiusMeters,
        StopId = point.StopId,
        Description = point.Description,
        Address = point.Address,
        OperatorName = point.OperatorName,
        BaseFare = point.BaseFare,
        FarePerKilometer = point.FarePerKilometer,
        AverageWaitingTimeSeconds = point.AverageWaitingTimeSeconds,
        ServiceStartTime = point.ServiceStartTime,
        ServiceEndTime = point.ServiceEndTime,
        IsActive = point.IsActive
    };
}

internal static class TricyclePointEditViewModelExtensions
{
    public static TricyclePointEditViewModel WithId(this TricyclePointEditViewModel model, long id) => new()
    {
        TricyclePointId = id,
        Request = model.Request,
        DuplicateWarnings = model.DuplicateWarnings,
        ErrorMessage = model.ErrorMessage,
        SuccessMessage = model.SuccessMessage
    };
}
