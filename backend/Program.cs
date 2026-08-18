using backend.Authentication;
using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using backend.Services.Transportation;
using Microsoft.EntityFrameworkCore;
using backend.Helpers;
using System.Diagnostics;
using backend.Services.Routing;
using backend.Services.Destinations;
using backend.Services.TripSessions;
using backend.Services.Navigation;
using backend.Services.Assistant;
using backend.Services.Telemetry;
using Microsoft.Extensions.Options;

LoadDevelopmentEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

// Some container platforms assign the listening port through PORT. The
// Docker image otherwise uses ASPNETCORE_HTTP_PORTS=8080.
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

var valhallaBaseUrl = builder.Configuration["Valhalla:BaseUrl"];
if (!Uri.TryCreate(valhallaBaseUrl, UriKind.Absolute, out var valhallaUri) ||
    (valhallaUri.Scheme != Uri.UriSchemeHttp && valhallaUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Valhalla:BaseUrl must be an absolute HTTP or HTTPS URL. " +
        "Set Valhalla__BaseUrl in the environment.");
}

builder.Services.AddHttpClient<IValhallaService, ValhallaService>(client =>
{
    client.BaseAddress = valhallaUri;
});

builder.Services.AddOptions<RoutingOptions>()
    .Bind(builder.Configuration.GetSection(RoutingOptions.SectionName))
    .Validate(options => options.IsValid(out _), "Routing configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<NavigationOptions>()
    .Bind(builder.Configuration.GetSection(NavigationOptions.SectionName))
    .Validate(options => options.IsValid(), "Navigation configuration is invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<PeliasOptions>()
    .Bind(builder.Configuration.GetSection(PeliasOptions.SectionName))
    .Validate(options => options.IsValid(), "Pelias configuration is invalid.")
    .ValidateOnStart();

var connectionString =
    builder.Configuration.GetConnectionString("TukiDbConnection")
    ?? throw new InvalidOperationException(
        "The TukiDbConnection connection string is missing.");


builder.Services.Configure<LoginOptions>(builder.Configuration.GetSection(LoginOptions.SectionName));

builder.Services.AddDbContext<TukiDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IRouteSegmentRepository, RouteSegmentRepository>();
builder.Services.AddScoped<IRouteStopRepository, RouteStopRepository>();
builder.Services.AddScoped<ITransportModeRepository, TransportModeRepository>();
builder.Services.AddScoped<ITransportRouteRepository, TransportRouteRepository>();
builder.Services.AddScoped<ITransportStopRepository, TransportStopRepository>();
builder.Services.AddScoped<ITripAlertRepository, TripAlertRepository>();
builder.Services.AddScoped<ITripSearchRepository, TripSearchRepository>();
builder.Services.AddScoped<ITripSessionRepository, TripSessionRepository>();
builder.Services.AddScoped<INavigationInstructionRepository, NavigationInstructionRepository>();
builder.Services.AddScoped<ITripLandmarkCandidateRepository, TripLandmarkCandidateRepository>();
builder.Services.AddScoped<ITransferConnectionRepository, TransferConnectionRepository>();
builder.Services.AddScoped<ITricyclePointRepository, TricyclePointRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddSingleton<IApiKeyService, InMemoryApiKeyService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IRideMatchingService, RideMatchingService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ITransferConnectionService, TransferConnectionService>();
builder.Services.AddScoped<ITricyclePointService, TricyclePointService>();
builder.Services.AddScoped<ITransportRouteService, TransportRouteService>();
builder.Services.AddScoped<IRouteGeneratorService, RouteGeneratorService>();
builder.Services.AddScoped<IAssistantIntentExtractor, NemotronIntentExtractor>();
builder.Services.AddScoped<ITukiAssistantService, TukiAssistantService>();
builder.Services.AddScoped<IJourneyPlanPersistenceService, JourneyPlanPersistenceService>();
builder.Services.AddSingleton<ITukiTelemetry, TukiTelemetry>();
builder.Services.AddScoped<IRoutingService, RoutingService>();
builder.Services.AddSingleton<ITripSessionStateMachine, TripSessionStateMachine>();
builder.Services.AddScoped<ITripSessionService, TripSessionService>();
builder.Services.AddScoped<INavigationInstructionService, NavigationInstructionService>();
builder.Services.AddSingleton<IGpsQualityValidator, GpsQualityValidator>();
builder.Services.AddSingleton<IMapMatchingService, MapMatchingService>();
builder.Services.AddSingleton<IOffRouteDetector, OffRouteDetector>();
builder.Services.AddScoped<ILocationTrackingService, LocationTrackingService>();
builder.Services.AddScoped<ILandmarkService, LandmarkService>();
builder.Services.AddScoped<ILandmarkCorridorPrefetchService, LandmarkCorridorPrefetchService>();
builder.Services.AddScoped<IReroutingService, ReroutingService>();
builder.Services.AddSingleton<IServiceArea, BoundingBoxServiceArea>();
builder.Services.AddSingleton<ITripAreaValidator, TripAreaValidator>();
builder.Services.AddScoped<IDestinationSearchProvider, LocalDestinationSearchProvider>();
builder.Services.AddHttpClient<PeliasPlaceProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<PeliasOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<IDestinationSearchProvider>(services =>
    services.GetRequiredService<PeliasPlaceProvider>());
builder.Services.AddScoped<IPlaceLandmarkDiscoveryService>(services =>
    services.GetRequiredService<PeliasPlaceProvider>());
builder.Services.AddScoped<IDestinationSearchService, DestinationSearchService>();
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
