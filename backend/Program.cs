using backend.Authentication;
using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using backend.Services.Transportation;
using Microsoft.EntityFrameworkCore;
using backend.Helpers;
using System.Diagnostics;
using backend.Services.Routing;
using Microsoft.Extensions.Options;

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

var connectionString =
    builder.Configuration.GetConnectionString("TukiDbConnection")
    ?? throw new InvalidOperationException(
        "The TukiDbConnection connection string is missing.");


builder.Services.Configure<LoginOptions>(builder.Configuration.GetSection(LoginOptions.SectionName));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.Configure<FacebookOptions>(builder.Configuration.GetSection(FacebookOptions.SectionName));

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
builder.Services.AddScoped<ITransferConnectionRepository, TransferConnectionRepository>();
builder.Services.AddScoped<ITricyclePointRepository, TricyclePointRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddSingleton<IApiKeyService, InMemoryApiKeyService>();
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
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ITransferConnectionService, TransferConnectionService>();
builder.Services.AddScoped<ITricyclePointService, TricyclePointService>();
builder.Services.AddScoped<ITransportRouteService, TransportRouteService>();
builder.Services.AddScoped<IRouteGeneratorService, RouteGeneratorService>();
builder.Services.AddScoped<NemotronAIHelper>();
builder.Services.AddScoped<IRoutingService, RoutingService>();
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
