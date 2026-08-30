using System.Globalization;
using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Telemetry;

public interface IAiUsageMetricsStore
{
    void Record(string source, string model, long inputTokens, long outputTokens);
    AiUsageMetricsSnapshot Snapshot();
}

public sealed record AiUsageMetricsSnapshot(
    DateTimeOffset SinceUtc,
    long TotalCalls,
    long IntentCalls,
    long NavigationCalls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    string? LastModel,
    decimal InputUsdPerMillionTokens,
    decimal OutputUsdPerMillionTokens,
    decimal UsdToPhp,
    decimal EstimatedCostUsd,
    decimal EstimatedCostPhp);

public sealed class AiUsageMetricsStore : IAiUsageMetricsStore
{
    private const decimal DefaultInputUsdPerMillionTokens = 0.30m;
    private const decimal DefaultOutputUsdPerMillionTokens = 2.50m;
    private const decimal DefaultUsdToPhp = 62.27m;
    private static readonly TimeSpan PersistenceTimeout = TimeSpan.FromSeconds(2);

    private readonly DateTimeOffset _sinceUtc = DateTimeOffset.UtcNow;
    private readonly decimal _inputUsdPerMillionTokens;
    private readonly decimal _outputUsdPerMillionTokens;
    private readonly decimal _usdToPhp;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiUsageMetricsStore> _logger;
    private readonly object _modelLock = new();

    private long _totalCalls;
    private long _intentCalls;
    private long _navigationCalls;
    private long _inputTokens;
    private long _outputTokens;
    private string? _lastModel;

    public AiUsageMetricsStore(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<AiUsageMetricsStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _inputUsdPerMillionTokens = ReadPositiveDecimal(
            configuration["AiUsage:InputUsdPerMillionTokens"],
            DefaultInputUsdPerMillionTokens);
        _outputUsdPerMillionTokens = ReadPositiveDecimal(
            configuration["AiUsage:OutputUsdPerMillionTokens"],
            DefaultOutputUsdPerMillionTokens);
        _usdToPhp = ReadPositiveDecimal(
            configuration["AiUsage:UsdToPhp"],
            DefaultUsdToPhp);
    }

    public void Record(string source, string model, long inputTokens, long outputTokens)
    {
        var safeSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
        var safeModel = string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim();
        var safeInputTokens = Math.Max(0, inputTokens);
        var safeOutputTokens = Math.Max(0, outputTokens);

        Interlocked.Increment(ref _totalCalls);
        if (string.Equals(safeSource, "intent", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _intentCalls);
        else if (string.Equals(safeSource, "navigation", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _navigationCalls);

        Interlocked.Add(ref _inputTokens, safeInputTokens);
        Interlocked.Add(ref _outputTokens, safeOutputTokens);

        lock (_modelLock)
            _lastModel = safeModel;

        var estimatedCostUsd =
            (safeInputTokens / 1_000_000m * _inputUsdPerMillionTokens) +
            (safeOutputTokens / 1_000_000m * _outputUsdPerMillionTokens);
        var estimatedCostPhp = estimatedCostUsd * _usdToPhp;

        // Keep the request hot path non-blocking. Persistence uses its own scoped
        // DbContext so it cannot accidentally save unrelated request-tracked state.
        _ = PersistAsync(
            safeSource,
            safeModel,
            safeInputTokens,
            safeOutputTokens,
            estimatedCostUsd,
            estimatedCostPhp);
    }

    public AiUsageMetricsSnapshot Snapshot()
    {
        var inputTokens = Interlocked.Read(ref _inputTokens);
        var outputTokens = Interlocked.Read(ref _outputTokens);
        string? lastModel;
        lock (_modelLock)
            lastModel = _lastModel;

        var estimatedCostUsd =
            (inputTokens / 1_000_000m * _inputUsdPerMillionTokens) +
            (outputTokens / 1_000_000m * _outputUsdPerMillionTokens);
        var estimatedCostPhp = estimatedCostUsd * _usdToPhp;

        return new AiUsageMetricsSnapshot(
            _sinceUtc,
            Interlocked.Read(ref _totalCalls),
            Interlocked.Read(ref _intentCalls),
            Interlocked.Read(ref _navigationCalls),
            inputTokens,
            outputTokens,
            inputTokens + outputTokens,
            lastModel,
            _inputUsdPerMillionTokens,
            _outputUsdPerMillionTokens,
            _usdToPhp,
            estimatedCostUsd,
            estimatedCostPhp);
    }

    private async Task PersistAsync(
        string source,
        string model,
        long inputTokens,
        long outputTokens,
        decimal estimatedCostUsd,
        decimal estimatedCostPhp)
    {
        try
        {
            using var timeout = new CancellationTokenSource(PersistenceTimeout);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TukiDbContext>();

            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                IF OBJECT_ID(N'dbo.AiUsageEvents', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO dbo.AiUsageEvents
                    (
                        Source,
                        Model,
                        InputTokens,
                        OutputTokens,
                        InputUsdPerMillionTokens,
                        OutputUsdPerMillionTokens,
                        UsdToPhp,
                        EstimatedCostUsd,
                        EstimatedCostPhp
                    )
                    VALUES
                    (
                        {source},
                        {model},
                        {inputTokens},
                        {outputTokens},
                        {_inputUsdPerMillionTokens},
                        {_outputUsdPerMillionTokens},
                        {_usdToPhp},
                        {estimatedCostUsd},
                        {estimatedCostPhp}
                    );
                END
                """, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI usage persistence exceeded {TimeoutMs}ms", PersistenceTimeout.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI usage persistence failed; live counters remain available");
        }
    }

    private static decimal ReadPositiveDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
}
