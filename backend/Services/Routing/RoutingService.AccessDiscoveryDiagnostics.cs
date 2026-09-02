using System.Diagnostics;
using backend.Services.Telemetry;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private AccessDiscoveryDiagnostics? _accessDiscoveryDiagnostics;

    private sealed class AccessDiscoveryDiagnostics
    {
        public long WalkCandidateGenerationTicks { get; private set; }
        public long TricycleCandidateGenerationTicks { get; private set; }
        public long TodaCandidateDiscoveryTicks { get; private set; }
        public long TodaFilteringRankingTicks { get; private set; }
        public long AccessAlternativeRankingTicks { get; private set; }
        public long WalkAlternativesGenerated { get; private set; }
        public long TricycleAlternativesGenerated { get; private set; }
        public long TodaDiscoveryInvocations { get; private set; }
        public long TodaCandidatesConsidered { get; private set; }
        public long TodaCandidatesSurvivingFilters { get; private set; }
        public long TodaCandidatesSelected { get; private set; }

        public void RecordWalkCandidates(long started, long count)
        {
            WalkCandidateGenerationTicks += Stopwatch.GetTimestamp() - started;
            WalkAlternativesGenerated += count;
        }

        public void RecordTricycleCandidates(long started, long count)
        {
            TricycleCandidateGenerationTicks += Stopwatch.GetTimestamp() - started;
            TricycleAlternativesGenerated += count;
        }

        public void RecordTodaDiscovery(
            long discoveryTicks,
            long rankingTicks,
            long considered,
            long surviving,
            long selected)
        {
            TodaCandidateDiscoveryTicks += discoveryTicks;
            TodaFilteringRankingTicks += rankingTicks;
            TodaDiscoveryInvocations++;
            TodaCandidatesConsidered += considered;
            TodaCandidatesSurvivingFilters += surviving;
            TodaCandidatesSelected += selected;
        }

        public void RecordAlternativeRanking(long started) =>
            AccessAlternativeRankingTicks += Stopwatch.GetTimestamp() - started;

        public AccessDiscoveryDiagnosticCounts Counts() => new(
            TodaCandidatesConsidered,
            TodaCandidatesSurvivingFilters,
            TodaCandidatesSelected);

        public void Flush(ITukiTelemetry telemetry)
        {
            telemetry.ObserveRouting(
                "walk_access_candidate_generation_ms",
                ToMilliseconds(WalkCandidateGenerationTicks));
            telemetry.ObserveRouting(
                "tricycle_access_candidate_generation_ms",
                ToMilliseconds(TricycleCandidateGenerationTicks));
            telemetry.ObserveRouting(
                "toda_candidate_discovery_ms",
                ToMilliseconds(TodaCandidateDiscoveryTicks));
            telemetry.ObserveRouting(
                "toda_filtering_ranking_ms",
                ToMilliseconds(TodaFilteringRankingTicks));
            telemetry.ObserveRouting(
                "access_alternative_ranking_ms",
                ToMilliseconds(AccessAlternativeRankingTicks));
            telemetry.IncrementRouting(
                "walk_access_alternatives_generated",
                WalkAlternativesGenerated);
            telemetry.IncrementRouting(
                "tricycle_access_alternatives_generated",
                TricycleAlternativesGenerated);
            telemetry.IncrementRouting(
                "toda_discovery_invocations",
                TodaDiscoveryInvocations);
            telemetry.IncrementRouting(
                "toda_candidates_considered",
                TodaCandidatesConsidered);
            telemetry.IncrementRouting(
                "toda_candidates_surviving_filters",
                TodaCandidatesSurvivingFilters);
            telemetry.IncrementRouting(
                "toda_candidates_selected",
                TodaCandidatesSelected);
        }

        private static double ToMilliseconds(long stopwatchTicks) =>
            stopwatchTicks * 1_000d / Stopwatch.Frequency;
    }

    private readonly record struct AccessDiscoveryDiagnosticCounts(
        long TodaCandidatesConsidered,
        long TodaCandidatesSurvivingFilters,
        long TodaCandidatesSelected)
    {
        public static AccessDiscoveryDiagnosticCounts operator -(
            AccessDiscoveryDiagnosticCounts right,
            AccessDiscoveryDiagnosticCounts left) => new(
            right.TodaCandidatesConsidered - left.TodaCandidatesConsidered,
            right.TodaCandidatesSurvivingFilters - left.TodaCandidatesSurvivingFilters,
            right.TodaCandidatesSelected - left.TodaCandidatesSelected);
    }
}
