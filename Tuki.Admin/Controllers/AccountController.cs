using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Services.AdminAuth;
using Tuki.Admin.ViewModels;

namespace Tuki.Admin.Controllers;

public sealed class AccountController(IAdminAuthService adminAuthService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await adminAuthService.AuthenticateAsync(
            model.UserName,
            model.Password,
            cancellationToken);

        if (!result.Succeeded || result.Login is null || string.IsNullOrWhiteSpace(result.UserName))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to sign in.");
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, result.UserName),
            new Claim(ClaimTypes.NameIdentifier, result.UserName),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        HttpContext.Session.Clear();
        HttpContext.Session.SetString("TukiAdminApiKey", result.Login.ApiKey);
        HttpContext.Session.SetString("TukiAdminApiKeyHeader", result.Login.HeaderName);
        HttpContext.Session.SetString("TukiAdminApiKeyExpiresAt", result.Login.ExpiresAt.ToString("O"));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = result.Login.ExpiresAt
            });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
