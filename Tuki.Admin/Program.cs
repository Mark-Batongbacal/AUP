using Microsoft.AspNetCore.Authentication.Cookies;
using Tuki.Admin.Repositories.AdminAuth;
using Tuki.Admin.Repositories.Common;
using Tuki.Admin.Repositories.JeepneyRoutes;
using Tuki.Admin.Repositories.TricyclePoints;
using Tuki.Admin.Repositories.TricycleSubmissions;
using Tuki.Admin.Services.AdminAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = ".Tuki.Admin.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Tuki.Admin.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddHttpClient(BackendApiClientNames.TukiBackend, (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["BackendApi:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("BackendApi:BaseUrl is not configured.");
    }

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IAdminAuthRepository, AdminAuthRepository>();
builder.Services.AddScoped<IAdminTricycleSubmissionRepository, AdminTricycleSubmissionRepository>();
builder.Services.AddScoped<IAdminTricyclePointRepository, AdminTricyclePointRepository>();
builder.Services.AddScoped<IAdminJeepneyRouteRepository, AdminJeepneyRouteRepository>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
