using System.Diagnostics;
using System.Globalization;

namespace backend.Services.Telemetry;

public sealed class SystemResourceMetricsSampler
{
    private readonly object _sync = new();
    private TimeSpan? _lastProcessorTime;
    private DateTimeOffset? _lastSampledAtUtc;

    public SystemResourceMetricsSample Sample()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var now = DateTimeOffset.UtcNow;
        double? cpuPercent = null;

        lock (_sync)
        {
            if (_lastProcessorTime.HasValue && _lastSampledAtUtc.HasValue)
            {
                var cpuDelta = process.TotalProcessorTime - _lastProcessorTime.Value;
                var wallDelta = now - _lastSampledAtUtc.Value;
                if (wallDelta.TotalMilliseconds > 0)
                {
                    cpuPercent = cpuDelta.TotalMilliseconds /
                                 (wallDelta.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount)) * 100d;
                    cpuPercent = Math.Clamp(cpuPercent.Value, 0, 100);
                }
            }

            _lastProcessorTime = process.TotalProcessorTime;
            _lastSampledAtUtc = now;
        }

        var disk = ReadDisk();
        var cgroup = ReadContainerMemory();
        var network = ReadNetworkTotals();
        var isContainer = string.Equals(
                              Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                              "true",
                              StringComparison.OrdinalIgnoreCase) ||
                          File.Exists("/.dockerenv");

        return new SystemResourceMetricsSample(
            cpuPercent,
            process.WorkingSet64,
            GC.GetTotalMemory(forceFullCollection: false),
            process.Threads.Count,
            Environment.ProcessorCount,
            process.Id,
            Environment.MachineName,
            Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName,
            isContainer,
            cgroup.CurrentBytes,
            cgroup.LimitBytes,
            disk.UsedBytes,
            disk.TotalBytes,
            disk.UsagePercent,
            network.ReceivedBytes,
            network.SentBytes);
    }

    private static (long? UsedBytes, long? TotalBytes, double? UsagePercent) ReadDisk()
    {
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(root))
                return (null, null, null);

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return (null, null, null);

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return (used, drive.TotalSize, used * 100d / drive.TotalSize);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static (long? CurrentBytes, long? LimitBytes) ReadContainerMemory()
    {
        try
        {
            const string currentPath = "/sys/fs/cgroup/memory.current";
            const string maxPath = "/sys/fs/cgroup/memory.max";
            if (!File.Exists(currentPath) || !File.Exists(maxPath))
                return (null, null);

            var currentText = File.ReadAllText(currentPath).Trim();
            var maxText = File.ReadAllText(maxPath).Trim();
            var current = long.TryParse(currentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentValue)
                ? currentValue
                : (long?)null;
            var limit = string.Equals(maxText, "max", StringComparison.OrdinalIgnoreCase)
                ? null
                : long.TryParse(maxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxValue)
                    ? maxValue
                    : (long?)null;
            return (current, limit);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (long? ReceivedBytes, long? SentBytes) ReadNetworkTotals()
    {
        try
        {
            const string path = "/proc/net/dev";
            if (!File.Exists(path))
                return (null, null);

            long received = 0;
            long sent = 0;
            foreach (var line in File.ReadLines(path).Skip(2))
            {
                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                    continue;

                var interfaceName = parts[0].Trim();
                if (interfaceName.Equals("lo", StringComparison.OrdinalIgnoreCase))
                    continue;

                var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 9)
                    continue;

                if (long.TryParse(values[0], out var rx)) received += rx;
                if (long.TryParse(values[8], out var tx)) sent += tx;
            }

            return (received, sent);
        }
        catch
        {
            return (null, null);
        }
    }
}

public sealed record SystemResourceMetricsSample(
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
