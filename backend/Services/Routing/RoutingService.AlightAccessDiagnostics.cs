using System.Diagnostics;
using backend.Services.Telemetry;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private AlightAccessComputationDiagnostics?
        _alightAccessComputationDiagnostics;

    private enum AlightAccessCallerCategory
    {
        DirectDiscovery,
        DestinationAccessConstruction,
        Other
    }

    private sealed class AlightAccessComputationDiagnostics
    {
        private readonly long[] _requestedByCaller = new long[3];
        private readonly long[] _computedByCaller = new long[3];
        private long _requested;
        private long _computed;
        private long _reuseHits;
        private long _maximumRepeats;
        private long _totalComputationTicks;
        private long _firstComputationTicks;

        public void BeginComputation(AlightAccessCallerCategory caller)
        {
            _requested++;
            _requestedByCaller[(int)caller]++;
            _computed++;
            _computedByCaller[(int)caller]++;
        }

        public void RecordReuse(AlightAccessCallerCategory caller)
        {
            _requested++;
            _requestedByCaller[(int)caller]++;
            _reuseHits++;
            _maximumRepeats = Math.Max(_maximumRepeats, 1);
        }

        public void CompleteComputation(long elapsedTicks)
        {
            _totalComputationTicks += elapsedTicks;
            _firstComputationTicks += elapsedTicks;
        }

        public void Flush(ITukiTelemetry telemetry)
        {
            telemetry.IncrementRouting(
                "alight_access_computation_requests",
                _requested);
            telemetry.IncrementRouting(
                "alight_access_computations_executed",
                _computed);
            telemetry.IncrementRouting(
                "alight_access_unique_semantic_inputs",
                _computed);
            telemetry.IncrementRouting(
                "alight_access_repeated_semantic_inputs",
                _requested - _computed);
            telemetry.IncrementRouting(
                "alight_access_reuse_hits",
                _reuseHits);
            telemetry.SetRoutingValue(
                "alight_access_maximum_repeats_per_input",
                _maximumRepeats);
            RecordCallerCounts(
                telemetry,
                AlightAccessCallerCategory.DirectDiscovery,
                "direct_discovery");
            RecordCallerCounts(
                telemetry,
                AlightAccessCallerCategory.DestinationAccessConstruction,
                "destination_access");
            RecordCallerCounts(
                telemetry,
                AlightAccessCallerCategory.Other,
                "other");
            telemetry.ObserveRouting(
                "alight_access_computation_total_ms",
                ToMilliseconds(_totalComputationTicks));
            telemetry.ObserveRouting(
                "alight_access_first_computation_ms",
                ToMilliseconds(_firstComputationTicks));
            telemetry.ObserveRouting(
                "alight_access_repeated_computation_ms",
                0);
        }

        private void RecordCallerCounts(
            ITukiTelemetry telemetry,
            AlightAccessCallerCategory caller,
            string metricName)
        {
            telemetry.IncrementRouting(
                $"alight_access_{metricName}_requests",
                _requestedByCaller[(int)caller]);
            telemetry.IncrementRouting(
                $"alight_access_{metricName}_computations",
                _computedByCaller[(int)caller]);
        }

        private static double ToMilliseconds(long ticks) =>
            ticks * 1_000d / Stopwatch.Frequency;
    }
}
