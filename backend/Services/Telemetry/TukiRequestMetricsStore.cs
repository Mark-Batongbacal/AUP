using System.Collections.Concurrent;

namespace backend.Services.Telemetry;

public static class TukiRequestMetricsStore
{
    private static readonly ConcurrentQueue<TukiRequestSample> Requests = new();
    private static readonly TimeSpan MaximumRetention = TimeSpan.FromHours(24);

    public static void Record(string path, int statusCode, double elapsedMilliseconds)
    {
        var now = DateTimeOffset.UtcNow;
        Requests.Enqueue(new TukiRequestSample(
            now,
            path,
            statusCode,
            Math.Max(0, elapsedMilliseconds)));
        Trim(now - MaximumRetention);
    }

    public static TukiRequestMetricsSnapshot Snapshot(TimeSpan retention, int recentLimit = 12)
    {
        if (retention <= TimeSpan.Zero)
            retention = TimeSpan.FromHours(1);
        if (retention > MaximumRetention)
            retention = MaximumRetention;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - retention;
        Trim(cutoff);

        var samples = Requests
            .Where(sample => sample.OccurredAtUtc >= cutoff)
            .ToArray();

        var totalRequests = samples.LongLength;
        var serverErrors = samples.LongCount(sample => sample.StatusCode >= 500);
        var averageResponseTimeMs = totalRequests == 0
            ? 0
            : samples.Average(sample => sample.ElapsedMilliseconds);
        var errorRatePercent = totalRequests == 0
            ? 0
            : serverErrors * 100d / totalRequests;

        var timeline = samples
            .GroupBy(sample => StartOfHour(sample.OccurredAtUtc))
            .OrderBy(group => group.Key)
            .Select(group => new TukiRequestTimelinePoint(
                group.Key,
                group.LongCount(),
                group.Average(sample => sample.ElapsedMilliseconds),
                group.LongCount(sample => sample.StatusCode >= 500)))
            .ToArray();

        var recent = samples
            .OrderByDescending(sample => sample.OccurredAtUtc)
            .Take(Math.Clamp(recentLimit, 1, 50))
            .ToArray();

        return new TukiRequestMetricsSnapshot(
            Math.Max(1, (int)Math.Ceiling(retention.TotalHours)),
            totalRequests,
            averageResponseTimeMs,
            serverErrors,
            errorRatePercent,
            timeline,
            recent);
    }

    private static void Trim(DateTimeOffset cutoff)
    {
        while (Requests.TryPeek(out var oldest) && oldest.OccurredAtUtc < cutoff)
            Requests.TryDequeue(out _);
    }

    private static DateTimeOffset StartOfHour(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, TimeSpan.Zero);
}

public sealed record TukiRequestSample(
    DateTimeOffset OccurredAtUtc,
    string Path,
    int StatusCode,
    double ElapsedMilliseconds);

public sealed record TukiRequestTimelinePoint(
    DateTimeOffset HourUtc,
    long Requests,
    double AverageResponseTimeMs,
    long ServerErrors);

public sealed record TukiRequestMetricsSnapshot(
    int RetentionHours,
    long TotalRequests,
    double AverageResponseTimeMs,
    long ServerErrors,
    double ErrorRatePercent,
    IReadOnlyList<TukiRequestTimelinePoint> Timeline,
    IReadOnlyList<TukiRequestSample> RecentRequests);
