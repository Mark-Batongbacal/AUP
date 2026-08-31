// DIAGTEMP -- re-validates the routing fixes against the real Valhalla
// instance configured in backend/.env. Responses are cached on disk so the
// public server is queried once per distinct matrix call across all configs.
using System.Collections.Concurrent;
using System.Text.Json;
using backend.Models.Routing;
using backend.Models.Valhalla;
using backend.Services.Routing;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace backend.Tests.Services.Routing;

public sealed class RealValhallaRevalidationDIAGTEMP(ITestOutputHelper output)
{
    [Theory]
    [InlineData("T1 live-confirmed prefix", 15.12, 120.565, 15.12, 120.595)]
    [InlineData("T2 http sample B", 15.109698583445889, 120.58240903543013, 15.139582098206548, 120.60108373338038)]
    [InlineData("T3 http sample A", 15.12254950605129, 120.5997480979361, 15.139582098206548, 120.60108373338038)]
    public async Task Plan(string label, double oLat, double oLon, double dLat, double dLon)
    {
        var service = ProductionNetworkFixture.CreateService(
            valhalla: DiskCachedValhalla.Instance);

        var started = DateTime.UtcNow;
        var plans = await service.PlanTripsAsync(oLat, oLon, dLat, dLon);
        var elapsed = (DateTime.UtcNow - started).TotalSeconds;

        output.WriteLine($"### {label}: plans={plans.Count} in {elapsed:F0}s " +
            $"(valhalla hits={DiskCachedValhalla.Instance.Hits} misses={DiskCachedValhalla.Instance.Misses})");

        foreach (var plan in plans)
        {
            var jeepney = plan.Legs.Where(l => l.Mode == AccessMode.Jeepney).ToList();
            output.WriteLine($"[{plan.RecommendationType}] " +
                string.Join(" > ", plan.Legs.Select(l => l.Mode switch
                {
                    AccessMode.Jeepney => $"JEEP {l.RouteId} {l.DistanceMeters:F0}m",
                    AccessMode.Trike => $"TRIKE {l.DistanceMeters:F0}m",
                    _ => $"WALK {l.DistanceMeters:F0}m"
                })) +
                $"  P{plan.TotalFarePesos:F0} {plan.TotalTimeSeconds / 60:F0}min" +
                $" jeep={jeepney.Sum(l => l.DistanceMeters):F0}m" +
                $" feeder={plan.Legs.Where(l => l.Mode != AccessMode.Jeepney).Sum(l => l.DistanceMeters):F0}m");
        }

        DiskCachedValhalla.Instance.Flush();
    }
}

/// <summary>
/// Real Valhalla, with every matrix answer cached on disk. Three different
/// builds of the planner therefore see identical network data, which is what
/// makes the before/after comparison meaningful, and the public server is
/// asked once per distinct query rather than once per config.
/// </summary>
internal sealed class DiskCachedValhalla : IValhallaService
{
    public static readonly DiskCachedValhalla Instance = new();

    private readonly record struct Entry(int From, int To, double? Distance, double? Time);

    private readonly ConcurrentDictionary<string, List<Entry>> _cache;
    private readonly IValhallaService _inner;
    private readonly string _path;
    private readonly object _flushLock = new();

    public int Hits;
    public int Misses;

    private DiskCachedValhalla()
    {
        var scratch = Environment.GetEnvironmentVariable("VALHALLA_CACHE")
            ?? Path.Combine(Path.GetTempPath(), "tuki-valhalla-cache.json");
        _path = scratch;

        _cache = File.Exists(_path)
            ? new ConcurrentDictionary<string, List<Entry>>(
                JsonSerializer.Deserialize<Dictionary<string, List<Entry>>>(
                    File.ReadAllText(_path)) ?? [])
            : new ConcurrentDictionary<string, List<Entry>>();

        var env = LoadEnv();
        var http = new HttpClient
        {
            BaseAddress = new Uri(env["Valhalla__BaseUrl"]),
            Timeout = TimeSpan.FromSeconds(90)
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Valhalla:MaxConcurrentRequests"] =
                    env.GetValueOrDefault("Valhalla__MaxConcurrentRequests", "5")
            })
            .Build();
        _inner = new ValhallaService(http, configuration);
    }

    public Task<ValhallaRouteResponse> GetRouteAsync(
        double startLatitude, double startLongitude,
        double endLatitude, double endLongitude,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default) =>
        _inner.GetRouteAsync(startLatitude, startLongitude,
            endLatitude, endLongitude, costing, cancellationToken);

    public async Task<IReadOnlyList<ValhallaMatrixResult>> GetMatrixAsync(
        ValhallaLocation source,
        IReadOnlyList<ValhallaLocation> targets,
        string costing = "pedestrian",
        CancellationToken cancellationToken = default)
    {
        var key = costing + "|" + Key(source) + "|" +
            string.Join(";", targets.Select(Key));

        if (_cache.TryGetValue(key, out var cached))
        {
            Interlocked.Increment(ref Hits);
            return Materialize(cached);
        }

        Interlocked.Increment(ref Misses);
        var results = await _inner.GetMatrixAsync(source, targets, costing, cancellationToken);
        _cache[key] = results
            .Select(r => new Entry(r.FromIndex, r.ToIndex, r.Distance, r.Time))
            .ToList();
        return results;
    }

    public void Flush()
    {
        lock (_flushLock)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(
                _cache.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
    }

    private static IReadOnlyList<ValhallaMatrixResult> Materialize(List<Entry> entries) =>
        entries.Select(e => new ValhallaMatrixResult
        {
            FromIndex = e.From,
            ToIndex = e.To,
            Distance = e.Distance,
            Time = e.Time
        }).ToList();

    private static string Key(ValhallaLocation location) =>
        $"{location.Lat:F7},{location.Lon:F7}";

    private static Dictionary<string, string> LoadEnv()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../backend/.env"));
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
                continue;
            var split = trimmed.Split('=', 2);
            values[split[0].Trim()] = split[1].Trim().Trim('"').Trim('\'');
        }
        return values;
    }
}
