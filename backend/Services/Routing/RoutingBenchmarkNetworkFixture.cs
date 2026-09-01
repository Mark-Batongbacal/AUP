using System.Security.Cryptography;
using System.Text.Json;
using backend.Models.Routing;
using Microsoft.Extensions.Options;

namespace backend.Services.Routing;

public sealed class RoutingBenchmarkNetworkOptions
{
    public const string SectionName = "RoutingBenchmarkNetwork";

    public string? SnapshotPath { get; init; }
    public string? ExpectedSha256 { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SnapshotPath);

    public bool IsValid(out string? error)
    {
        if (!IsConfigured)
        {
            error = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(ExpectedSha256) ||
            ExpectedSha256.Length != 64 ||
            ExpectedSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            error = "RoutingBenchmarkNetwork:ExpectedSha256 must be a 64-character SHA-256 hash when SnapshotPath is configured.";
            return false;
        }

        error = null;
        return true;
    }
}

internal sealed record RoutingBenchmarkNetworkFixture(
    string FixtureId,
    string Sha256,
    IReadOnlyList<StaticJeepneyRoute> Routes,
    IReadOnlyList<TrikePoint> TrikePoints);

/// <summary>
/// Loads an explicitly configured, checksum-pinned routing network for local
/// performance comparisons. It is opt-in and never replaces database-backed
/// routing unless the benchmark process supplies both path and checksum.
/// </summary>
public sealed class RoutingBenchmarkNetworkFixtureProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RoutingBenchmarkNetworkOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<RoutingBenchmarkNetworkFixtureProvider> _logger;
    private readonly object _sync = new();
    private Task<RoutingBenchmarkNetworkFixture?>? _loadTask;

    public RoutingBenchmarkNetworkFixtureProvider(
        IOptions<RoutingBenchmarkNetworkOptions> options,
        IHostEnvironment environment,
        ILogger<RoutingBenchmarkNetworkFixtureProvider> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    internal Task<RoutingBenchmarkNetworkFixture?> GetFixtureAsync()
    {
        lock (_sync)
            return _loadTask ??= LoadAsync();
    }

    private async Task<RoutingBenchmarkNetworkFixture?> LoadAsync()
    {
        if (!_options.IsConfigured)
            return null;

        var configuredPath = _options.SnapshotPath!;
        var path = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath));
        var bytes = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(
                sha256,
                _options.ExpectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Routing benchmark network checksum mismatch for '{path}'. " +
                $"Expected {_options.ExpectedSha256}, found {sha256}.");
        }

        var document = JsonSerializer.Deserialize<BenchmarkNetworkDocument>(
                bytes,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"Routing benchmark network '{path}' is empty or malformed.");
        if (document.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(document.FixtureId) ||
            document.Routes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Routing benchmark network '{path}' has an unsupported or incomplete schema.");
        }

        var routes = document.Routes.Select(route => new StaticJeepneyRoute
        {
            RouteId = route.RouteId,
            RouteName = route.RouteName,
            Coordinates = route.Coordinates.Select(point => point.ToArray()).ToList()
        }).ToList();
        var trikePoints = document.TrikePoints.Select(point => new TrikePoint(
            point.Id,
            point.Name,
            point.Latitude,
            point.Longitude)).ToList();

        _logger.LogInformation(
            "Loaded routing benchmark fixture FixtureId={FixtureId} Sha256={Sha256} Routes={RouteCount} TodaPoints={TodaPointCount}",
            document.FixtureId,
            sha256,
            routes.Count,
            trikePoints.Count);

        return new RoutingBenchmarkNetworkFixture(
            document.FixtureId,
            sha256,
            routes,
            trikePoints);
    }

    private sealed record BenchmarkNetworkDocument(
        int SchemaVersion,
        string FixtureId,
        List<BenchmarkRoute> Routes,
        List<BenchmarkTrikePoint> TrikePoints);

    private sealed record BenchmarkRoute(
        string RouteId,
        string RouteName,
        List<double[]> Coordinates);

    private sealed record BenchmarkTrikePoint(
        string Id,
        string Name,
        double Latitude,
        double Longitude);
}
