using backend.Services.Telemetry;
using Microsoft.Extensions.Logging;

namespace backend.Tests.Services.Telemetry;

public sealed class RoutingPerformanceTelemetryTests
{
    [Fact]
    public void CompletedPlan_EmitsOneCorrelatedSummaryWithPassMetrics()
    {
        var logger = new CapturingLogger<TukiTelemetry>();
        var telemetry = new TukiTelemetry(logger);

        using (var plan = telemetry.BeginRoutingPlan("test"))
        {
            using var nestedPlan = telemetry.BeginRoutingPlan("nested");
            using (var pass = telemetry.BeginRoutingPass(2))
            {
                telemetry.IncrementRouting("candidates_generated", 42);
                telemetry.SetRoutingValue("selected_plan_count", 3);
                telemetry.ObserveRouting("confirmation_ms", 12.5);
                telemetry.ObserveRouting("confirmation_ms", 7.5);
                pass.Complete("success");
            }

            nestedPlan.Complete("success");
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("test", entry["Source"]);
        Assert.Equal("success", entry["Outcome"]);
        Assert.NotEqual(Guid.Empty, Assert.IsType<Guid>(entry["PlanId"]));

        Assert.Equal(42L, Assert.IsType<long>(entry["candidates_generated"]));
        Assert.Equal(2L, Assert.IsType<long>(entry["confirmation_ms_count"]));
        Assert.Equal(20, Assert.IsType<double>(entry["confirmation_ms_sum"]));
        Assert.Equal(12.5, Assert.IsType<double>(entry["confirmation_ms_max"]));

        var passes = Assert.IsAssignableFrom<
            IReadOnlyList<RoutingPassTelemetrySnapshot>>(entry["Passes"]);
        var passSnapshot = Assert.Single(passes);
        Assert.Equal(2, passSnapshot.MaxTransfers);
        Assert.Equal("success", passSnapshot.Outcome);
        Assert.Equal(42, passSnapshot.Counts["candidates_generated"]);
    }

    [Fact]
    public void IncompletePlan_UsesCancellationStateForOutcome()
    {
        var logger = new CapturingLogger<TukiTelemetry>();
        var telemetry = new TukiTelemetry(logger);
        using var cancellation = new CancellationTokenSource();

        using (telemetry.BeginRoutingPlan("failed"))
        {
        }

        cancellation.Cancel();
        using (telemetry.BeginRoutingPlan("canceled", cancellation.Token))
        {
        }

        Assert.Collection(
            logger.Entries,
            entry => Assert.Equal("failed", entry["Outcome"]),
            entry => Assert.Equal("canceled", entry["Outcome"]));
    }

    [Fact]
    public void IncompleteOuterOperation_OverridesAnEarlierInnerOutcome()
    {
        var logger = new CapturingLogger<TukiTelemetry>();
        var telemetry = new TukiTelemetry(logger);

        using (telemetry.BeginRoutingPlan("request"))
        {
            using (var fallbackPass = telemetry.BeginRoutingPlan("preferred"))
                fallbackPass.Complete("no_route");

            using var outerOperation = telemetry.BeginRoutingPlan("fallback");
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("failed", entry["Outcome"]);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values ||
                !formatter(state, exception).StartsWith(
                    "TukiRoutingPlan ",
                    StringComparison.Ordinal))
            {
                return;
            }

            Entries.Add(values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal));
        }
    }
}
