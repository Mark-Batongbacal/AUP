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
    public void CompletedPlan_EmitsCompactCoordinateFreeJourneyPerformanceSummary()
    {
        var logger = new CapturingLogger<TukiTelemetry>();
        var telemetry = new TukiTelemetry(logger);

        using (var plan = telemetry.BeginRoutingPlan("test"))
        {
            telemetry.SetRoutingValue("route_count", 21);
            telemetry.SetRoutingValue(
                "routes_considered_after_spatial_filter",
                7);
            telemetry.SetRoutingValue("selected_plan_count", 3);
            telemetry.IncrementRouting("board_access_alternatives", 40);
            telemetry.IncrementRouting("destination_access_alternatives", 32);
            telemetry.IncrementRouting(
                "board_alight_combinations_evaluated",
                120);
            telemetry.IncrementRouting(
                "transfer_interchange_candidates_evaluated",
                80);
            telemetry.IncrementRouting("transit_candidates_confirmed", 5);
            telemetry.IncrementRouting("valhalla_matrix_http_calls", 4);
            telemetry.IncrementRouting("valhalla_matrix_cache_hits", 3);
            telemetry.IncrementRouting("request_local_matrix_cache_hits", 2);
            telemetry.IncrementRouting("valhalla_route_http_calls", 6);
            telemetry.IncrementRouting("valhalla_route_cache_hits", 1);
            plan.Complete("success");
        }

        var entry = Assert.Single(logger.PerformanceEntries);
        Assert.Equal(21, Assert.IsType<double>(entry["RoutesTotal"]));
        Assert.Equal(7, Assert.IsType<double>(entry["RoutesConsidered"]));
        Assert.Equal(120L, entry["CombinationsEvaluated"]);
        Assert.Equal(5L, entry["MatrixCacheHits"]);
        Assert.Equal(3, Assert.IsType<double>(entry["OptionsProduced"]));
        Assert.DoesNotContain(entry.Keys, key =>
            key.Contains("Latitude", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Longitude", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task RoutingStage_AttributesValhallaMetricsAndRestoresNestedStage()
    {
        var logger = new CapturingLogger<TukiTelemetry>();
        var telemetry = new TukiTelemetry(logger);

        using (var plan = telemetry.BeginRoutingPlan("test"))
        {
            using (var pass = telemetry.BeginRoutingPass(2))
            {
                using (telemetry.BeginRoutingStage("access_discovery"))
                {
                    await Task.Run(() =>
                    {
                        telemetry.IncrementRouting(
                            "valhalla_matrix_http_calls");
                        telemetry.ObserveRouting(
                            "valhalla_gate_wait_ms",
                            10);
                        telemetry.IncrementRouting("valhalla_cache_misses");
                    });

                    using (telemetry.BeginRoutingStage("confirmation"))
                    {
                        telemetry.IncrementRouting(
                            "valhalla_matrix_http_calls");
                        telemetry.ObserveRouting(
                            "valhalla_gate_wait_ms",
                            20);
                        telemetry.IncrementRouting("valhalla_cache_hits");
                        telemetry.IncrementRouting("valhalla_calls_avoided");
                    }

                    telemetry.IncrementRouting(
                        "valhalla_matrix_http_calls");
                    telemetry.ObserveRouting(
                        "valhalla_gate_wait_ms",
                        30);
                    telemetry.RecordRoutingAccessDiscoveryRoute(
                        "ROUTE-A",
                        80,
                        12,
                        8,
                        3,
                        6,
                        12_160,
                        42,
                        16,
                        120,
                        96,
                        8);
                }

                pass.Complete("success");
            }

            plan.Complete("success");
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(
            3L,
            Assert.IsType<long>(entry["valhalla_matrix_http_calls"]));
        Assert.Equal(
            2L,
            Assert.IsType<long>(entry[
                "access_discovery_valhalla_matrix_http_calls"]));
        Assert.Equal(
            1L,
            Assert.IsType<long>(entry[
                "confirmation_valhalla_matrix_http_calls"]));
        Assert.Equal(
            60,
            Assert.IsType<double>(entry["valhalla_gate_wait_ms_sum"]));
        Assert.Equal(
            40,
            Assert.IsType<double>(entry[
                "access_discovery_valhalla_gate_wait_ms_sum"]));
        Assert.Equal(
            20,
            Assert.IsType<double>(entry[
                "confirmation_valhalla_gate_wait_ms_sum"]));
        Assert.Equal(
            1L,
            Assert.IsType<long>(entry[
                "access_discovery_valhalla_cache_misses"]));
        Assert.Equal(
            1L,
            Assert.IsType<long>(entry[
                "confirmation_valhalla_cache_hits"]));
        Assert.Equal(
            1L,
            Assert.IsType<long>(entry[
                "confirmation_valhalla_calls_avoided"]));

        var passes = Assert.IsAssignableFrom<
            IReadOnlyList<RoutingPassTelemetrySnapshot>>(entry["Passes"]);
        var passSnapshot = Assert.Single(passes);
        Assert.Equal(
            2,
            passSnapshot.Counts[
                "access_discovery_valhalla_matrix_http_calls"]);
        Assert.Equal(
            20,
            passSnapshot.Observations[
                "confirmation_valhalla_gate_wait_ms"].Sum);
        var route = Assert.Single(passSnapshot.AccessDiscoveryRoutes);
        Assert.Equal("ROUTE-A", route.RouteId);
        Assert.Equal(12_160, route.TodaCandidatesConsidered);
        Assert.Equal(120, route.BoardAccessAlternatives);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Entries { get; } = [];
        public List<IReadOnlyDictionary<string, object?>> PerformanceEntries
            { get; } = [];

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
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            {
                return;
            }

            var entry = values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal);
            var message = formatter(state, exception);
            if (message.StartsWith("TukiRoutingPlan ", StringComparison.Ordinal))
                Entries.Add(entry);
            else if (message.StartsWith(
                         "JourneyPerformance ",
                         StringComparison.Ordinal))
                PerformanceEntries.Add(entry);
        }
    }
}
