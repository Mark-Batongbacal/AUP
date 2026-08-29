using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Repositories.ServerPerformance;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ServerPerformanceController(
    IServerPerformanceRepository serverPerformanceRepository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await serverPerformanceRepository.GetSnapshotAsync(cancellationToken);
        ViewData["MonitoringError"] = result.ErrorMessage;
        return View(result.Snapshot);
    }

    [HttpGet]
    public async Task<IActionResult> Snapshot(CancellationToken cancellationToken)
    {
        var result = await serverPerformanceRepository.GetSnapshotAsync(cancellationToken);
        if (result.Succeeded && result.Snapshot is not null)
            return Json(result.Snapshot);

        return StatusCode(
            result.StatusCode is >= 400 and <= 599 ? result.StatusCode.Value : StatusCodes.Status502BadGateway,
            new { message = result.ErrorMessage ?? "Server monitoring is unavailable." });
    }
}
