using backend.Authentication;
using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Facebook;
using backend.Services.Authentication.Google;
using backend.Services.Authentication.Login;
using backend.Services.Email;
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

if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
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

builder.Services.AddSingleton<IValhallaConcurrencyGate, ValhallaConcurrencyGate>();
builder.Services.AddHttpClient<IValhallaService, ValhallaService>(client =>
{
    client.BaseAddress = valhallaUri;
});
builder.Services.AddHttpClient("ValhallaHealth", client =>
{
    client.BaseAddress = new Uri(valhallaUri.ToString().TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(4);
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
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.Configure<FacebookOptions>(builder.Configuration.GetSection(FacebookOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddDbContext<TukiDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IDriverAvailabilitySessionRepository, DriverAvailabilitySessionRepository>();
builder.Services.AddScoped<IDriverLocationRepository, DriverLocationRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IDriverVehicleRepository, DriverVehicleRepository>();
builder.Services.AddScoped<IFareRuleRepository, FareRuleRepository>();
builder.Services.AddScoped<IFavoriteTripRepository, FavoriteTripRepository>();
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
builder.Services.AddScoped<ITricyclePointSubmissionRepository, TricyclePointSubmissionRepository>();
builder.Services.AddScoped<ITricyclePointSubmissionPublishingRepository, TricyclePointSubmissionPublishingRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IApiKeyService, SqlServerApiKeyService>();
builder.Services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
builder.Services.AddSingleton<IFacebookAccessTokenValidator>(serviceProvider =>
{
    var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

#if DEBUG
    return new FacebookAccessTokenValidator(
        httpClient,
        serviceProvider.GetRequiredService<ILogger<FacebookAccessTokenValidator>>(),
        builder.Environment.IsDevelopment());
#else
    return new FacebookAccessTokenValidator(httpClient);
#endif
});
builder.Services.AddSingleton<IFacebookOidcTokenValidator>(serviceProvider =>
{
    var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    var facebookOptions = serviceProvider.GetRequiredService<IOptions<FacebookOptions>>().Value;

    return new FacebookOidcTokenValidator(
        httpClient,
        facebookOptions.OidcIssuer,
        facebookOptions.OidcJwksUri);
});
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IRideMatchingService, RideMatchingService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IFavoriteTripService, FavoriteTripService>();
builder.Services.AddScoped<IEmailSender, AzureEmailSender>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<ILocalAuthenticationService, LocalAuthenticationService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ITransferConnectionService, TransferConnectionService>();
builder.Services.AddScoped<ITricyclePointService, TricyclePointService>();
builder.Services.AddScoped<IAdminTricyclePointManagementService, AdminTricyclePointManagementService>();
builder.Services.AddScoped<IAdminJeepneyRouteManagementService, AdminJeepneyRouteManagementService>();
builder.Services.AddScoped<ITricyclePointSubmissionService, TricyclePointSubmissionService>();
builder.Services.AddScoped<IAdminTricyclePointSubmissionService, AdminTricyclePointSubmissionService>();
builder.Services.AddScoped<ITricyclePointSubmissionPublishingService, TricyclePointSubmissionPublishingService>();
builder.Services.AddSingleton<ITricycleProofStorage, FileSystemTricycleProofStorage>();
builder.Services.AddScoped<ITransportRouteService, TransportRouteService>();
builder.Services.AddScoped<IRouteGeneratorService, RouteGeneratorService>();
builder.Services.AddScoped<IAssistantIntentExtractor, NemotronIntentExtractor>();
builder.Services.AddScoped<IAssistantPlaceResolver, GoogleAssistantPlaceResolver>();
builder.Services.AddScoped<ITukiAssistantService, TukiAssistantService>();
builder.Services.AddScoped<IJourneyPlanPersistenceService, JourneyPlanPersistenceService>();
builder.Services.AddSingleton<ITukiTelemetry, TukiTelemetry>();
builder.Services.AddSingleton<SystemResourceMetricsSampler>();
builder.Services.AddSingleton<RoutingNetworkSnapshotProvider>();
builder.Services.AddSingleton<IRoutingNetworkChangeNotifier>(services =>
    services.GetRequiredService<RoutingNetworkSnapshotProvider>());
builder.Services.AddScoped<IRoutingService, TransferFallbackRoutingService>();
builder.Services.AddScoped<IJourneyPlanningFacadeService, JourneyPlanningFacadeService>();
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
builder.Services.AddScoped<INavigationSpeechService, NemotronNavigationSpeechService>();
builder.Services.AddScoped<INavigationFacadeService, NavigationFacadeService>();
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
builder.Services.AddScoped<IReverseGeocodingService>(services =>
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.Use(async (context, next) =>
{
    var telemetry = context.RequestServices.GetRequiredService<ITukiTelemetry>();
    using var routingPlan = context.Request.Method == HttpMethods.Post &&
        context.Request.Path.Equals("/api/journeys/plan")
            ? telemetry.BeginRoutingPlan(
                "POST /api/journeys/plan",
                context.RequestAborted)
            : null;
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        telemetry.RecordRequest(
            context.Request.Path,
            context.Response.StatusCode,
            elapsedMilliseconds);
        TukiRequestMetricsStore.Record(
            context.Request.Path,
            context.Response.StatusCode,
            elapsedMilliseconds);
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void LoadDevelopmentEnvironmentFile()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "backend", ".env"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"))
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return;
    }

    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
