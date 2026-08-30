using System.Diagnostics;
using System.Reflection;
using backend.Authentication;
using backend.Models.Database;
using backend.Models.SystemMonitoring;
using backend.Services.Authentication.ApiKey;
using backend.Services.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/system")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = "Admin")]
public sealed class AdminSystemController(
    TukiDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    SystemResourceMetricsSampler resourceMetricsSampler,
    IAiUsageMetricsStore aiUsageMetricsStore) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<AdminSystemOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSystemOverviewResponse>> GetOverview(
        CancellationToken cancellationToken = default)
    {
        var databaseHealthTask = CheckDatabaseAsync(cancellationToken);
        var valhallaHealthTask = CheckValhallaAsync(cancellationToken);
        await Task.WhenAll(databaseHealthTask, valhallaHealthTask);

        var services = new[]
        {
            new AdminServiceHealthResponse(
                "backend",
                "Backend API",
                "Healthy",
                0,
                "The TUKI API is responding to the authenticated monitoring request."),
            await databaseHealthTask,
            await valhallaHealthTask
        };

        var totalTrips = await GetTotalTripsAsync(cancellationToken);
        var aiUsage = aiUsageMetricsStore.Snapshot();
        var requestSnapshot = TukiRequestMetricsStore.Snapshot(TimeSpan.FromHours(24));
        var resourceSnapshot = resourceMetricsSampler.Sample();
        using var process = Process.GetCurrentProcess();
        var processStartedAt = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        var uptime = DateTimeOffset.UtcNow - processStartedAt;
        var overallStatus = services.All(service => service.Status == "Healthy")
            ? "Healthy"
            : services.Any(service => service.Status == "Unhealthy")
                ? "Unhealthy"
                : "Degraded";

        var response = new AdminSystemOverviewResponse(
            overallStatus,
            DateTimeOffset.UtcNow,
            environment.EnvironmentName,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            Math.Max(0, (long)uptime.TotalSeconds),
            new AdminSystemResourceResponse(
                resourceSnapshot.CpuPercent,
                resourceSnapshot.WorkingSetBytes,
                resourceSnapshot.ManagedMemoryBytes,
                resourceSnapshot.ThreadCount,
                resourceSnapshot.ProcessorCount,
                resourceSnapshot.ProcessId,
                resourceSnapshot.MachineName,
                resourceSnapshot.ContainerName,
                resourceSnapshot.IsContainer,
                resourceSnapshot.ContainerMemoryCurrentBytes,
                resourceSnapshot.ContainerMemoryLimitBytes,
                resourceSnapshot.DiskUsedBytes,
                resourceSnapshot.DiskTotalBytes,
                resourceSnapshot.DiskUsagePercent,
                resourceSnapshot.NetworkReceivedBytes,
                resourceSnapshot.NetworkSentBytes),
            configuration["Valhalla:BaseUrl"] ?? "Not configured",
            services,
            totalTrips,
            new AdminAiUsageResponse(
                aiUsage.SinceUtc,
                aiUsage.TotalCalls,
                aiUsage.IntentCalls,
                aiUsage.NavigationCalls,
                aiUsage.InputTokens,
                aiUsage.OutputTokens,
                aiUsage.TotalTokens,
                aiUsage.LastModel,
                aiUsage.InputUsdPerMillionTokens,
                aiUsage.OutputUsdPerMillionTokens,
                aiUsage.UsdToPhp,
                aiUsage.EstimatedCostUsd,
                aiUsage.EstimatedCostPhp),
            new AdminRequestMetricsResponse(
                requestSnapshot.RetentionHours,
                requestSnapshot.TotalRequests,
                requestSnapshot.AverageResponseTimeMs,
                requestSnapshot.ServerErrors,
                requestSnapshot.ErrorRatePercent,
                requestSnapshot.Timeline
                    .Select(point => new AdminRequestTimelinePointResponse(
                        point.HourUtc,
                        point.Requests,
                        point.AverageResponseTimeMs,
                        point.ServerErrors))
                    .ToArray()),
            requestSnapshot.RecentRequests
                .Select(sample => new AdminRecentRequestResponse(
                    sample.OccurredAtUtc,
                    sample.Path,
                    sample.StatusCode,
                    sample.ElapsedMilliseconds))
                .ToArray());

        return Ok(response);
    }

    private async Task<long?> GetTotalTripsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.PassengerTrips.LongCountAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<AdminServiceHealthResponse> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();
            return canConnect
                ? new AdminServiceHealthResponse(
                    "database",
                    "SQL Server",
                    "Healthy",
                    stopwatch.Elapsed.TotalMilliseconds,
                    "Database connection succeeded.")
                : new AdminServiceHealthResponse(
                    "database",
                    "SQL Server",
                    "Unhealthy",
                    stopwatch.Elapsed.TotalMilliseconds,
                    "Database connection check returned false.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new AdminServiceHealthResponse(
                "database",
                "SQL Server",
                "Unhealthy",
                stopwatch.Elapsed.TotalMilliseconds,
                "Database connection check failed.");
        }
    }

    private async Task<AdminServiceHealthResponse> CheckValhallaAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("ValhallaHealth");
            using var response = await client.GetAsync("status", cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new AdminServiceHealthResponse(
                    "valhalla",
                    "Valhalla",
                    "Healthy",
                    stopwatch.Elapsed.TotalMilliseconds,
                    "Routing engine health endpoint responded successfully.");
            }

            return new AdminServiceHealthResponse(
                "valhalla",
                "Valhalla",
                "Degraded",
                stopwatch.Elapsed.TotalMilliseconds,
                $"Routing engine responded with HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            return new AdminServiceHealthResponse(
                "valhalla",
                "Valhalla",
                "Unhealthy",
                stopwatch.Elapsed.TotalMilliseconds,
                "Routing engine could not be reached.");
        }
    }
}
