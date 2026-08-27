namespace backend.Models.SystemMonitoring;

public sealed record AdminSystemOverviewResponse(
    string Status,
    DateTimeOffset CheckedAtUtc,
    string Environment,
    string Version,
    long UptimeSeconds,
    AdminSystemResourceResponse Resources,
    string ValhallaEndpoint,
    IReadOnlyList<AdminServiceHealthResponse> Services,
    AdminRequestMetricsResponse Requests,
    IReadOnlyList<AdminRecentRequestResponse> RecentRequests);

public sealed record AdminSystemResourceResponse(
    double? CpuPercent,
    long WorkingSetBytes,
    long ManagedMemoryBytes,
    int ThreadCount,
    int ProcessorCount,
    int ProcessId,
    string MachineName,
    string ContainerName,
    bool IsContainer,
    long? ContainerMemoryCurrentBytes,
    long? ContainerMemoryLimitBytes,
    long? DiskUsedBytes,
    long? DiskTotalBytes,
    double? DiskUsagePercent,
    long? NetworkReceivedBytes,
    long? NetworkSentBytes);

public sealed record AdminServiceHealthResponse(
    string Key,
    string Name,
    string Status,
    double? ResponseTimeMs,
    string Detail);

public sealed record AdminRequestMetricsResponse(
    int RetentionHours,
    long TotalRequests,
    double AverageResponseTimeMs,
    long ServerErrors,
    double ErrorRatePercent,
    IReadOnlyList<AdminRequestTimelinePointResponse> Timeline);

public sealed record AdminRequestTimelinePointResponse(
    DateTimeOffset HourUtc,
    long Requests,
    double AverageResponseTimeMs,
    long ServerErrors);

public sealed record AdminRecentRequestResponse(
    DateTimeOffset OccurredAtUtc,
    string Path,
    int StatusCode,
    double ElapsedMilliseconds);
