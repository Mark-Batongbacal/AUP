namespace Tuki.Admin.Models.ServerPerformance;

public sealed class ServerPerformanceSnapshot
{
    public string Status { get; init; } = "Unknown";
    public DateTimeOffset CheckedAtUtc { get; init; }
    public string Environment { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long UptimeSeconds { get; init; }
    public ServerResourceSnapshot Resources { get; init; } = new();
    public string ValhallaEndpoint { get; init; } = string.Empty;
    public IReadOnlyList<ServerServiceHealth> Services { get; init; } = [];
    public long? TotalTrips { get; init; }
    public ServerAiUsageMetrics AiUsage { get; init; } = new();
    public ServerRequestMetrics Requests { get; init; } = new();
    public IReadOnlyList<ServerRecentRequest> RecentRequests { get; init; } = [];
}

public sealed class ServerResourceSnapshot
{
    public double? CpuPercent { get; init; }
    public long WorkingSetBytes { get; init; }
    public long ManagedMemoryBytes { get; init; }
    public int ThreadCount { get; init; }
    public int ProcessorCount { get; init; }
    public int ProcessId { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public bool IsContainer { get; init; }
    public long? ContainerMemoryCurrentBytes { get; init; }
    public long? ContainerMemoryLimitBytes { get; init; }
    public long? DiskUsedBytes { get; init; }
    public long? DiskTotalBytes { get; init; }
    public double? DiskUsagePercent { get; init; }
    public long? NetworkReceivedBytes { get; init; }
    public long? NetworkSentBytes { get; init; }
}

public sealed class ServerServiceHealth
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Unknown";
    public double? ResponseTimeMs { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class ServerAiUsageMetrics
{
    public DateTimeOffset SinceUtc { get; init; }
    public long TotalCalls { get; init; }
    public long IntentCalls { get; init; }
    public long NavigationCalls { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens { get; init; }
    public string? LastModel { get; init; }
    public decimal InputUsdPerMillionTokens { get; init; }
    public decimal OutputUsdPerMillionTokens { get; init; }
    public decimal UsdToPhp { get; init; }
    public decimal EstimatedCostUsd { get; init; }
    public decimal EstimatedCostPhp { get; init; }
}

public sealed class ServerRequestMetrics
{
    public int RetentionHours { get; init; }
    public long TotalRequests { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public long ServerErrors { get; init; }
    public double ErrorRatePercent { get; init; }
    public IReadOnlyList<ServerRequestTimelinePoint> Timeline { get; init; } = [];
}

public sealed class ServerRequestTimelinePoint
{
    public DateTimeOffset HourUtc { get; init; }
    public long Requests { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public long ServerErrors { get; init; }
}

public sealed class ServerRecentRequest
{
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string Path { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public double ElapsedMilliseconds { get; init; }
}
