using System.Data;
using System.Data.Common;
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
    IAiUsageMetricsStore aiUsageMetricsStore,
    ILogger<AdminSystemController> logger) : ControllerBase
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8);

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

        var totalTrips = await GetTripCountAsync(null, cancellationToken);
        var aiUsage = aiUsageMetricsStore.Snapshot();
        var aiEconomics = await GetAiEconomicsAsync(aiUsage, cancellationToken);
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
            aiEconomics,
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

    private async Task<AdminAiEconomicsResponse> GetAiEconomicsAsync(
        AiUsageMetricsSnapshot processUsage,
        CancellationToken cancellationToken)
    {
        var nowManila = DateTimeOffset.UtcNow.ToOffset(ManilaOffset);
        var todayStartUtc = new DateTimeOffset(nowManila.Date, ManilaOffset).UtcDateTime;
        var last7DaysStartUtc = todayStartUtc.AddDays(-6);

        try
        {
            var connection = dbContext.Database.GetDbConnection();
            var closeAfter = connection.State != ConnectionState.Open;
            if (closeAfter)
                await connection.OpenAsync(cancellationToken);

            try
            {
                var windows = new Dictionary<string, PersistentAiWindow>(StringComparer.OrdinalIgnoreCase);
                DateTimeOffset? trackingStartedAtUtc = null;
                string? lastModel = processUsage.LastModel;

                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = """
                        IF OBJECT_ID(N'dbo.AiUsageEvents', N'U') IS NULL
                        BEGIN
                            SELECT CAST(0 AS bit) AS HasStorage;
                        END
                        ELSE
                        BEGIN
                            SELECT CAST(1 AS bit) AS HasStorage;

                            SELECT
                                w.WindowKey,
                                COUNT_BIG(e.AiUsageEventId) AS TotalCalls,
                                COALESCE(SUM(CASE WHEN e.Source = N'intent' THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS IntentCalls,
                                COALESCE(SUM(CASE WHEN e.Source = N'navigation' THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END), 0) AS NavigationCalls,
                                COALESCE(SUM(CAST(e.InputTokens AS bigint)), 0) AS InputTokens,
                                COALESCE(SUM(CAST(e.OutputTokens AS bigint)), 0) AS OutputTokens,
                                COALESCE(SUM(e.EstimatedCostUsd), CAST(0 AS decimal(19,10))) AS EstimatedCostUsd,
                                COALESCE(SUM(e.EstimatedCostPhp), CAST(0 AS decimal(19,8))) AS EstimatedCostPhp
                            FROM
                            (
                                VALUES
                                    (N'today', @TodayStartUtc),
                                    (N'last7days', @Last7DaysStartUtc),
                                    (N'lifetime', CAST(NULL AS datetime2(7)))
                            ) AS w(WindowKey, StartUtc)
                            LEFT JOIN dbo.AiUsageEvents e
                                ON w.StartUtc IS NULL OR e.OccurredAtUtc >= w.StartUtc
                            GROUP BY w.WindowKey;

                            SELECT
                                MIN(OccurredAtUtc) AS TrackingStartedAtUtc,
                                (
                                    SELECT TOP (1) Model
                                    FROM dbo.AiUsageEvents
                                    ORDER BY OccurredAtUtc DESC, AiUsageEventId DESC
                                ) AS LastModel
                            FROM dbo.AiUsageEvents;
                        END;
                        """;
                    AddParameter(command, "@TodayStartUtc", todayStartUtc);
                    AddParameter(command, "@Last7DaysStartUtc", last7DaysStartUtc);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(reader.GetOrdinal("HasStorage")))
                        return PersistentAiUnavailable(processUsage.LastModel);

                    if (!await reader.NextResultAsync(cancellationToken))
                        return PersistentAiUnavailable(processUsage.LastModel);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var inputTokens = reader.GetInt64(reader.GetOrdinal("InputTokens"));
                        var outputTokens = reader.GetInt64(reader.GetOrdinal("OutputTokens"));
                        windows[reader.GetString(reader.GetOrdinal("WindowKey"))] = new PersistentAiWindow(
                            reader.GetInt64(reader.GetOrdinal("TotalCalls")),
                            reader.GetInt64(reader.GetOrdinal("IntentCalls")),
                            reader.GetInt64(reader.GetOrdinal("NavigationCalls")),
                            inputTokens,
                            outputTokens,
                            reader.GetDecimal(reader.GetOrdinal("EstimatedCostUsd")),
                            reader.GetDecimal(reader.GetOrdinal("EstimatedCostPhp")));
                    }

                    if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                    {
                        var trackingOrdinal = reader.GetOrdinal("TrackingStartedAtUtc");
                        if (!reader.IsDBNull(trackingOrdinal))
                        {
                            var value = DateTime.SpecifyKind(reader.GetDateTime(trackingOrdinal), DateTimeKind.Utc);
                            trackingStartedAtUtc = new DateTimeOffset(value);
                        }

                        var modelOrdinal = reader.GetOrdinal("LastModel");
                        if (!reader.IsDBNull(modelOrdinal))
                            lastModel = reader.GetString(modelOrdinal);
                    }
                }

                // The data reader is closed before EF runs the trip-count queries,
                // so this works even when MultipleActiveResultSets is disabled.
                var todayTrips = await GetTripCountAsync(todayStartUtc, cancellationToken) ?? 0;
                var last7DaysTrips = await GetTripCountAsync(last7DaysStartUtc, cancellationToken) ?? 0;
                var trackedLifetimeTrips = trackingStartedAtUtc.HasValue
                    ? await GetTripCountAsync(trackingStartedAtUtc.Value.UtcDateTime, cancellationToken) ?? 0
                    : 0;

                return new AdminAiEconomicsResponse(
                    true,
                    "Asia/Manila (UTC+8)",
                    trackingStartedAtUtc,
                    lastModel,
                    ToWindow(windows.GetValueOrDefault("today"), todayTrips),
                    ToWindow(windows.GetValueOrDefault("last7days"), last7DaysTrips),
                    ToWindow(windows.GetValueOrDefault("lifetime"), trackedLifetimeTrips));
            }
            finally
            {
                if (closeAfter && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Persistent AI economics snapshot is unavailable");
            return PersistentAiUnavailable(processUsage.LastModel);
        }
    }

    private async Task<long?> GetTripCountAsync(
        DateTime? startUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = dbContext.TripSessions.AsNoTracking();
            if (startUtc.HasValue)
            {
                var threshold = DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc);
                query = query.Where(trip => trip.CreatedAt >= threshold);
            }

            return await query.LongCountAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not count trip sessions for Admin monitoring");
            return null;
        }
    }

    private static AdminAiUsageWindowResponse ToWindow(PersistentAiWindow? window, long trips)
    {
        window ??= PersistentAiWindow.Empty;
        return new AdminAiUsageWindowResponse(
            trips,
            window.TotalCalls,
            window.IntentCalls,
            window.NavigationCalls,
            window.InputTokens,
            window.OutputTokens,
            window.InputTokens + window.OutputTokens,
            window.EstimatedCostUsd,
            window.EstimatedCostPhp,
            trips > 0 ? window.EstimatedCostPhp / trips : null);
    }

    private static AdminAiEconomicsResponse PersistentAiUnavailable(string? lastModel) =>
        new(
            false,
            "Asia/Manila (UTC+8)",
            null,
            lastModel,
            ToWindow(null, 0),
            ToWindow(null, 0),
            ToWindow(null, 0));

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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

    private sealed record PersistentAiWindow(
        long TotalCalls,
        long IntentCalls,
        long NavigationCalls,
        long InputTokens,
        long OutputTokens,
        decimal EstimatedCostUsd,
        decimal EstimatedCostPhp)
    {
        public static PersistentAiWindow Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
    }
}
