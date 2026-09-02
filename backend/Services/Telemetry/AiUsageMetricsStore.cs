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
    private static readonly TimeSpan SchemaInitializationTimeout = TimeSpan.FromSeconds(10);

    private readonly DateTimeOffset _sinceUtc = DateTimeOffset.UtcNow;
    private readonly decimal _inputUsdPerMillionTokens;
    private readonly decimal _outputUsdPerMillionTokens;
    private readonly decimal _usdToPhp;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiUsageMetricsStore> _logger;
    private readonly object _modelLock = new();
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    private long _totalCalls;
    private long _intentCalls;
    private long _navigationCalls;
    private long _inputTokens;
    private long _outputTokens;
    private string? _lastModel;
    private volatile bool _storageReady;

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

        // The previous implementation only inserted when dbo.AiUsageEvents already
        // existed, which made persistence silently stay empty on a fresh deployment.
        // Bootstrap the idempotent schema in the background as soon as the singleton
        // is first resolved, and PersistAsync still verifies it before every first write.
        _ = InitializeStorageAsync();
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

    private async Task InitializeStorageAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(SchemaInitializationTimeout);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TukiDbContext>();
            await EnsureStorageAsync(dbContext, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "AI usage schema initialization exceeded {TimeoutMs}ms",
                SchemaInitializationTimeout.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "AI usage schema initialization failed; live counters remain available");
        }
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

            await EnsureStorageAsync(dbContext, timeout.Token);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
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

    private async Task EnsureStorageAsync(
        TukiDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (_storageReady)
            return;

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_storageReady)
                return;

            await dbContext.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.AiUsageEvents', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AiUsageEvents
                    (
                        AiUsageEventId BIGINT IDENTITY(1,1) NOT NULL
                            CONSTRAINT PK_AiUsageEvents PRIMARY KEY,
                        OccurredAtUtc DATETIME2(7) NOT NULL
                            CONSTRAINT DF_AiUsageEvents_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
                        Source NVARCHAR(30) NOT NULL,
                        Model NVARCHAR(200) NOT NULL,
                        InputTokens BIGINT NOT NULL,
                        OutputTokens BIGINT NOT NULL,
                        InputUsdPerMillionTokens DECIMAL(18,6) NOT NULL,
                        OutputUsdPerMillionTokens DECIMAL(18,6) NOT NULL,
                        UsdToPhp DECIMAL(18,6) NOT NULL,
                        EstimatedCostUsd DECIMAL(19,10) NOT NULL,
                        EstimatedCostPhp DECIMAL(19,8) NOT NULL,
                        CONSTRAINT CK_AiUsageEvents_InputTokens CHECK (InputTokens >= 0),
                        CONSTRAINT CK_AiUsageEvents_OutputTokens CHECK (OutputTokens >= 0),
                        CONSTRAINT CK_AiUsageEvents_InputPrice CHECK (InputUsdPerMillionTokens > 0),
                        CONSTRAINT CK_AiUsageEvents_OutputPrice CHECK (OutputUsdPerMillionTokens > 0),
                        CONSTRAINT CK_AiUsageEvents_UsdToPhp CHECK (UsdToPhp > 0),
                        CONSTRAINT CK_AiUsageEvents_CostUsd CHECK (EstimatedCostUsd >= 0),
                        CONSTRAINT CK_AiUsageEvents_CostPhp CHECK (EstimatedCostPhp >= 0)
                    );
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.AiUsageEvents')
                      AND name = N'IX_AiUsageEvents_OccurredAtUtc'
                )
                BEGIN
                    CREATE INDEX IX_AiUsageEvents_OccurredAtUtc
                        ON dbo.AiUsageEvents (OccurredAtUtc)
                        INCLUDE (Source, Model, InputTokens, OutputTokens, EstimatedCostUsd, EstimatedCostPhp);
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.AiUsageEvents')
                      AND name = N'IX_AiUsageEvents_SourceOccurredAtUtc'
                )
                BEGIN
                    CREATE INDEX IX_AiUsageEvents_SourceOccurredAtUtc
                        ON dbo.AiUsageEvents (Source, OccurredAtUtc);
                END;
                """, cancellationToken);

            _storageReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static decimal ReadPositiveDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
}
