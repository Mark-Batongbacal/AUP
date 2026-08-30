using System.Globalization;

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

    private readonly DateTimeOffset _sinceUtc = DateTimeOffset.UtcNow;
    private readonly decimal _inputUsdPerMillionTokens;
    private readonly decimal _outputUsdPerMillionTokens;
    private readonly decimal _usdToPhp;
    private readonly object _modelLock = new();

    private long _totalCalls;
    private long _intentCalls;
    private long _navigationCalls;
    private long _inputTokens;
    private long _outputTokens;
    private string? _lastModel;

    public AiUsageMetricsStore(IConfiguration configuration)
    {
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
        var safeInputTokens = Math.Max(0, inputTokens);
        var safeOutputTokens = Math.Max(0, outputTokens);

        Interlocked.Increment(ref _totalCalls);
        if (string.Equals(source, "intent", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _intentCalls);
        else if (string.Equals(source, "navigation", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _navigationCalls);

        Interlocked.Add(ref _inputTokens, safeInputTokens);
        Interlocked.Add(ref _outputTokens, safeOutputTokens);

        if (!string.IsNullOrWhiteSpace(model))
        {
            lock (_modelLock)
                _lastModel = model.Trim();
        }
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

    private static decimal ReadPositiveDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
}
