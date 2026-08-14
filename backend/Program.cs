using backend.Authentication;
using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using backend.Helpers;
using System.Diagnostics;

LoadDevelopmentEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

// Render assigns the listening port through the PORT environment variable.
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod();
}));
builder.Services.Configure<LoginOptions>(builder.Configuration.GetSection(LoginOptions.SectionName));
var supabaseConnectionString = builder.Configuration.GetConnectionString("Supabase")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Supabase configuration. Set ConnectionStrings__Supabase.");
var supabaseConnectionStringBuilder = new NpgsqlConnectionStringBuilder(supabaseConnectionString)
{
    // Supabase installed PostGIS in this custom schema. It lets EF resolve geography types.
    SearchPath = "public,gis"
};
builder.Services.AddDbContext<SupabaseDbContext>(options =>
    options.UseNpgsql(supabaseConnectionStringBuilder.ConnectionString, npgsqlOptions => npgsqlOptions.UseNetTopologySuite()));
builder.Services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IDriverAvailabilitySessionRepository, DriverAvailabilitySessionRepository>();
builder.Services.AddScoped<IDriverLocationRepository, DriverLocationRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IDriverVehicleRepository, DriverVehicleRepository>();
builder.Services.AddScoped<IFareRuleRepository, FareRuleRepository>();
builder.Services.AddScoped<IPassengerRideRequestRepository, PassengerRideRequestRepository>();
builder.Services.AddScoped<IPassengerTripRepository, PassengerTripRepository>();
builder.Services.AddScoped<IRecommendationLegRepository, RecommendationLegRepository>();
builder.Services.AddScoped<IRideMatchRepository, RideMatchRepository>();
builder.Services.AddScoped<IRouteRecommendationRepository, RouteRecommendationRepository>();
builder.Services.AddScoped<IRouteSegmentRepository, RouteSegmentRepository>();
builder.Services.AddScoped<IRouteStopRepository, RouteStopRepository>();
builder.Services.AddScoped<ITransportModeRepository, TransportModeRepository>();
builder.Services.AddScoped<ITransportRouteRepository, TransportRouteRepository>();
builder.Services.AddScoped<ITransportStopRepository, TransportStopRepository>();
builder.Services.AddScoped<ITripAlertRepository, TripAlertRepository>();
builder.Services.AddScoped<ITripSearchRepository, TripSearchRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddSingleton<IApiKeyService, InMemoryApiKeyService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<ITransportRouteService, TransportRouteService>();
builder.Services.AddScoped<NemotronAIHelper>();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Render terminates HTTPS at its proxy and forwards requests to this container over HTTP.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.Use(async (context, next) =>
{
    context.Items["RequestStartTimestamp"] = Stopwatch.GetTimestamp();
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/test", () => "Backend is alive");
app.Run();

static void LoadDevelopmentEnvironmentFile()
{
    if (!string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environments.Development,
            StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var environmentFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(environmentFile))
    {
        return;
    }

    foreach (var line in File.ReadLines(environmentFile))
    {
        var trimmedLine = line.Trim();
        if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmedLine[..separatorIndex].Trim();
        var value = trimmedLine[(separatorIndex + 1)..].Trim();
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
