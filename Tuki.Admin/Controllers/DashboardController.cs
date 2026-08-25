using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.ViewModels;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(new DashboardViewModel
        {
            AdminUserName = User.Identity?.Name ?? "Admin"
        });
    }
}
