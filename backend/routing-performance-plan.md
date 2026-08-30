# Tuki routing performance optimization plan

## Goal and non-goals

Improve route-planning latency, throughput, and failure behavior without changing
which journeys Tuki considers valid or how it ranks and presents them. Work should
be incremental and evidence-led. This is not a routing rewrite, and no phase may
trade correctness or route diversity for a favorable benchmark.

The comparison baseline is:

| Load | Plan average | Plan median | Plan p95 | Plan success | HTTP failed | Flow success |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 10 VUs | 10.4 s | 9.67 s | 20.12 s | 97.45% | 2.98% | 87.26% |
| 15 VUs | — | 16.38 s | 27.15 s | — | — | — |
| 20 VUs | — | approaches 30 s | — | failures spike | — | — |

Gemini and navigation speech remain disabled for capacity measurements. The
workload is therefore intended to isolate deterministic routing, database, and
Valhalla behavior.

## Implementation status (2026-08-29)

- Phase A is complete in commit `494115d`: correlated routing telemetry and the
  reproducible k6 benchmark profile are in place.
- Phase B is implemented on `route-optimization`: a singleton, versioned
  `RoutingNetworkSnapshotProvider` single-flights construction and atomically
  publishes the active route/TODA geometry, samples, anchors, and interchange
  graph. Scoped `RoutingService` instances retain request-local state and share
  the same snapshot, including preferred/fallback passes. Successful in-process
  active route/TODA mutations invalidate the snapshot; failed rebuilds never
  replace the last known-good version.
- Phase C is implemented on `route-optimization`: all typed `ValhallaService`
  instances acquire the same singleton `IValhallaConcurrencyGate`. The limit is
  configurable through `Valhalla:MaxConcurrentRequests` and retains the existing
  default of five until capacity testing supports a different value.
- The Phase B invalidation signal is intentionally process-local. Before running
  multiple backend instances, add the durable revision/outbox notification
  described below so every process observes admin mutations.
- Phase D1 is retained in commit `7d46cc4`. Controlled same-data A/B testing
  showed that it lowers selector allocation by about 68%, lowers CPU, and
  improves the 10-VU plan p95 from 26.12 s to 19.59 s. The previously proposed
  D2 selector rewrite is canceled pending evidence of a different bottleneck.

### Frozen access-discovery diagnostic fixture

The access-discovery investigation adds an opt-in, SHA-256-pinned routing input
under `stress/fixtures`. Normal processes continue loading active route/TODA
records from the database. `stress/run-routing-benchmark.sh` starts a clean
backend process against the frozen 21-route/8,630-point/152-TODA snapshot and
runs exact-coordinate, plan-only k6 traffic. A deterministic first-118-TODA
variant is included only for controlled scaling; it is not labeled as the
unknown historical 118-TODA composition.

Fine-grained telemetry now records per-plan and per-route board discovery,
direct-connection discovery, prefix computation, destination access, TODA scan
and ranking, access alternative construction/ranking, access/direct completion
discovery, access Valhalla calls/gate wait/execution, and all requested candidate
counts.

The measured topology performs 1,534 `FindNearbyTrikePoints` invocations per
ordinary heavy plan. A route with `S` samples performs `2S + 4` full TODA scans:

- projected board and search-anchor board each rediscover the same origin TODAs;
- exact full-route board performs another origin scan;
- destination access scans once per sample inside direct-connection discovery;
- destination access scans once per sample again for transfer search;
- exact full-route alight performs one additional scan.

At 118 TODAs this evaluates 181,012 TODA/anchor pairs per plan; at 152 it
evaluates 233,168 (+28.8%). Median selected nearby TODAs rose only from 4,506 to
4,684 (+4.0%) because `MaxNearbyTrikeCandidates` remains four. Median 1-VU local
TODA scan time rose from 17.55 ms to 22.63 ms and destination-access computation
from 25.88 ms to 30.78 ms; complete access discovery remained effectively flat
(250.07 ms versus 246.30 ms).

Under the saturated plan-only 10-VU workload, access-discovery wall time varied
with the process-wide Valhalla gate rather than local TODA work. In the mixed
run, 118 to 152 TODAs changed median local TODA discovery from 67.90 ms to
84.69 ms, but access Valhalla gate wait from 0.05 ms to 868.26 ms and complete
access discovery from 881.52 ms to 1,992.39 ms. Access-discovery matrix-call
count was identical (median 10; mean 8.16) in both fixtures. A subsequent fixed
trip repeat reversed the access wall/gate direction while preserving the exact
181,130 versus 233,320 pair counts, demonstrating significant shared-Valhalla
run variance at saturation.

Diagnosis: TODA growth creates real linear CPU/allocation work through repeated
full-list scans and duplicated destination generation, but it does not directly
create more access-discovery Valhalla calls and is too small to explain a
multi-second historical increase. The disproportionate tail is access calls
waiting behind concurrent confirmation traffic at the global gate, with some
indirect pressure from the changed tricycle candidate mix. Do not change TODA,
walking, candidate, or confirmation limits based on these results.

Validation after B/C: all 461 backend tests pass. The isolated 1-VU run recorded
4.78 s average, 5.29 s median, 8.63 s p95, and 100% plan/flow success. The
isolated 10-VU run recorded 6.78 s average, 8.11 s median, 20.07 s p95, 100%
plan/flow success, and 0% failed HTTP requests. Relative to the supplied 10-VU
baseline, average improved 34.8%, median improved 16.1%, and p95 was effectively
flat (20.12 s to 20.07 s).

### Post-B/C tail diagnosis

The diagnostic telemetry change adds candidate-generation substage timings and
attributes Valhalla calls, cache activity, gate wait, and execution to either
access discovery or confirmation. It does not change candidate limits, routing
requests, ranking, filtering, or selection behavior. All 463 backend tests pass.

An unchanged B/C control run at the effective runtime Valhalla concurrency of 20
produced 59 successful 10-VU plan calls: 6.21 s average, 7.77 s median, and
17.74 s interpolated p95. In its empirical p95 cohort, median routing time was
18.52 s, candidate generation was 15.08 s (82.4%), and confirmation was 3.36 s
(17.6%). The slowest ten had the same shape: 83.5% candidate generation and
16.3% confirmation.

The instrumented run passed the overhead control: 1-VU average changed from
4.60 s to 4.68 s (+1.7%) and median from 5.02 s to 5.10 s (+1.6%). Its 68
successful 10-VU plan calls recorded 7.33 s average, 7.88 s median, and 18.63 s
interpolated p95. The empirical p95 cohort broke down as follows:

| Stage | Median wall time | Share/interpretation |
| --- | ---: | --- |
| Candidate generation | 13.05 s | 72.3% of routing time |
| Confirmation | 4.82 s | 27.6% of routing time |
| Diversity selection | 6.90 s | largest candidate substage |
| Access discovery | 1.79 s | includes 13 median Valhalla calls |
| Transfer candidate generation | 1.64 s | CPU candidate enumeration |
| Candidate key generation | 1.63 s | main dedupe key construction |
| Candidate dedupe | 0.55 s | grouping/representative selection excluding keys |
| Access expansion | 0.26 s | alternative enumeration/materialization |
| Hard-constraint filtering | 0.10 s | forward-progress and configured constraints |

Per-request substage sums matched `candidate_generation_ms` with a 0.1 ms
median residual. Access-discovery gate wait was secondary (75 ms median average
per call and 460 ms median maximum). The p95 cohort's confirmation stage made a
median 708 Valhalla calls, with 1.74 s median average gate wait per call and
3.28 s median maximum gate wait. That is substantial global queuing, but the
concurrent confirmation wall stage remained below 30% of p95 routing time. The
slowest ten instrumented requests spent 78.4% in candidate generation and 21.6%
in confirmation. The measured primary bottleneck is therefore candidate
generation, led by diversity selection; Valhalla confirmation/gate wait is a
material secondary bottleneck, not absent.

Two benchmark caveats are recorded rather than attributed to the instrumentation:
the active TODA dataset changed from 108 to 118 points between control and
instrumented captures, and 13 instrumented-run navigation starts returned 409
after planning succeeded. Plan success remained 100%; stage classification uses
within-request wall shares and the unchanged-control cohort rather than treating
the downstream flow failures as routing failures.

## Current architecture: verified findings

### Routing service lifetime and network initialization

- `backend/Program.cs` registers `IRoutingService` as scoped and maps it to
  `TransferFallbackRoutingService`.
- `backend/Services/Routing/TransferFallbackRoutingService.cs` constructs a
  preferred `RoutingService` and, when configured transfer depth permits it, a
  second fallback `RoutingService` itself. These instances are not shared across
  HTTP request scopes.
- `RoutingService.EnsureInitializedAsync` in
  `backend/Services/Routing/RoutingService.cs` queries
  `ITransportRouteRepository.GetAllActiveWithOrderedPointsAsync` and
  `ITricyclePointRepository.GetAllActiveAsync`, validates the data, then builds:
  - `_routes`
  - `_trikePoints`
  - `_routeGeometries`
  - `_routeSamples`
  - `_routeSearchAnchors`
  - `_interchangesByRoute`
- `_isInitialized` and `_initializationLock` belong to each `RoutingService`
  instance. Consequently, the preferred network is loaded and rebuilt once per
  scoped `TransferFallbackRoutingService`, normally once per HTTP request. If the
  fallback pass is used, its separate `RoutingService` initializes and rebuilds
  the same static network again in that request.

Making `RoutingService` a singleton is not safe: it participates in request work,
contains request-local matrix state, and is constructed with scoped repository
dependencies. Only its immutable network inputs and derived indexes should be
shared.

### Valhalla service lifetime and concurrency

- `backend/Program.cs` registers `IValhallaService, ValhallaService` using
  `AddHttpClient`. A typed HTTP client service is transient. In the current graph,
  a scoped `TransferFallbackRoutingService` resolves one typed client instance
  for that scope and passes it to both inner routing services.
- `backend/Services/Routing/ValhallaService.cs` creates its own `SemaphoreSlim`
  in its constructor. The configured limit is therefore per typed service
  instance/request scope, not process-wide.
- With the current default of five, 10 concurrent request scopes may collectively
  admit roughly 50 Valhalla calls. This is not global backpressure and can explain
  the nonlinear latency/failure increase when Valhalla saturates. Phase A metrics
  must confirm the concurrency knee before selecting a process-wide limit.

### Candidate confirmation fan-out

- `RoutingService.SelectCandidatesToConfirmWithDiversity` in
  `backend/Services/Routing/RoutingService.NavigationOptimization.cs` preserves
  physical boarding regions, objective orderings, access-mode pairs, route
  sequences, TODAs, and route occurrences within the configured 300-candidate
  ceiling.
- `RoutingService.ConfirmDestinationCompletionEdgesAsync` in
  `backend/Services/Routing/RoutingService.DestinationCompletion.cs` starts three
  confirmation families together:
  - up to 300 selected transit candidates;
  - direct walk/tricycle completion edges;
  - up to 300 access-path completion edges selected in
    `RoutingService.AccessPathTermination.cs`.
- The transit family creates a task for every selected candidate. Each call into
  `ConfirmJourneyCandidatesAsync` in
  `RoutingService.JourneyConfirmation.cs` can concurrently confirm origin access,
  destination access, and every transfer walk.
- Tricycle access confirmation can issue a pedestrian matrix and a ride matrix
  concurrently. Access-path termination can additionally request route geometry
  for the TODA-to-board path and a direct TODA-to-destination path.
- Therefore one planning request can schedule hundreds of high-level confirmation
  operations and more Valhalla operations than candidate count. The existing
  semaphore limits execution only within that one typed client instance; it does
  not prevent a large allocation/task fan-out or cross-request saturation.

The 300 ceiling is a correctness safeguard for difficult route networks, not a
tuning knob to reduce without evidence.

### Matrix caching

- `RoutingService.GetMatrixAsync` in `RoutingService.cs` uses `_matrixRequests` to
  reuse an exact matrix task by costing, source coordinate, and ordered targets.
- `_matrixRequests` belongs to one `RoutingService`; it is request/service-instance
  local. It does not help another HTTP request or the separately constructed
  fallback `RoutingService`.
- `ConcurrentDictionary.GetOrAdd` may run its value factory more than once during
  a race. A losing factory can already have started a Valhalla request, so the
  logical request-local cache hit count and actual Valhalla HTTP call count must
  be measured separately.

## Proposed target architecture

### Shared immutable routing network snapshot

Introduce an immutable `RoutingNetworkSnapshot` containing all data derived only
from active route/TODA records:

- version/revision and build timestamp;
- validated routes and tricycle points;
- full route geometries;
- sampled route points;
- search anchors;
- occurrence-aware interchange graph and route-name/index lookups.

Introduce an `IRoutingNetworkSnapshotProvider` with these properties:

1. The provider is singleton and exposes the current immutable snapshot.
2. Snapshot construction is single-flight. A scoped builder resolves the existing
   repositories from a newly created service scope; the singleton never captures
   a scoped repository or `DbContext`.
3. The first request may trigger a lazy initial build, or startup can warm it.
   Concurrent callers await the same build.
4. A successful rebuild is published with one atomic reference swap
   (`Interlocked.Exchange`/`Volatile.Read`). Requests already using the previous
   snapshot finish against that complete version. No request sees partially
   rebuilt dictionaries.
5. Failed builds leave the last known-good snapshot installed, emit health/error
   telemetry, and retry according to a bounded policy. They must never publish a
   partial or empty snapshot.
6. `RoutingService` remains scoped and retains request-specific preferences,
   cancellation, diagnostics, and exact matrix tasks. It receives one snapshot
   reference at planning start and uses that same version for the entire pass,
   including fallback and geometry enrichment.

Active-network mutations that require refresh are:

- publishing an inactive jeepney route in
  `AdminJeepneyRouteManagementService.PublishDraftAsync`;
- active-route deactivation/activation in
  `TransportRouteDeactivationController` (this direct repository mutation should
  be routed through a mutation service that can signal the provider);
- creation or update of an active tricycle point, and
  `AdminTricyclePointManagementService.SetActiveAsync`.

Draft-only metadata/geometry edits do not change the active snapshot and should
not rebuild it until publish.

After a successful database commit, the mutation workflow increments a durable
network revision and requests an eager single-flight rebuild. The old immutable
snapshot may continue serving in-flight requests while the new one is built; the
new revision is swapped only after complete validation. The admin response and
health telemetry must make a refresh failure visible. In a multi-instance
deployment, an in-memory event is insufficient: use the durable revision (or an
outbox/pub-sub notification backed by it) so every process detects and builds the
new version. Static caches use the same version and are discarded as a unit.

### Process-wide Valhalla gate

Add a singleton `IValhallaConcurrencyGate` that owns the process-wide permit pool.
Keep `ValhallaService` as the typed HTTP client; inject the singleton gate and
acquire it immediately before `PostAsJsonAsync`. Record wait and execution time
separately.

The limit must be configuration-driven and selected from Phase A measurements.
It should protect Valhalla and prevent request scopes from multiplying the limit.
This phase adds backpressure; it does not change route requests, costing models,
timeouts, or response interpretation. If Tuki runs in multiple processes, the
gate is process-wide, so the configured limit must account for instance count and
Valhalla's total capacity.

### Conditional progressive, diverse confirmation

Post-B/C measurements defer this architecture until candidate diversity/key
processing is optimized and remeasured. If confirmation later meets the decision
gate in Phase D, retain the existing ranked/diverse sequence and the 300 transit
hard ceiling, but consume it progressively:

1. Produce the same deterministic diverse ordering as today. Do not replace the
   diversity selector with a simple cost sort.
2. Confirm an initial batch containing representatives from every currently
   protected diversity/objective slice. Batch composition must preserve route,
   boarding occurrence, access-mode/TODA, cheapest, fastest, efficient, and
   preference-specific coverage.
3. Run the existing authoritative validation and pruning on accumulated confirmed
   candidates.
4. Stop only when a measured and regression-tested sufficiency condition is met,
   for example when every requested objective has a valid survivor and no
   unconfirmed candidate can beat the retained objective bounds. A simple
   “found N plans” rule is not sufficient.
5. Otherwise confirm the next diverse batch and repeat, eventually reaching the
   unchanged 300 ceiling for difficult cases.
6. Batch direct and access-path completion work as well. These alternatives must
   not be starved merely because a transit result survived.

Initial batch size and sufficiency rules are measurement assumptions, not fixed
design values. Add shadow-mode diagnostics first: compute where progressive
confirmation would have stopped while still executing the full current pipeline,
then compare outputs before enabling early stop.

### Static Valhalla work and caches

Safe long-lived entries are pairs whose complete coordinates and costing are
derived from one immutable network snapshot:

- pedestrian transfer walks between route interchange anchors;
- pedestrian route-anchor-to-TODA access legs;
- tricycle/TODA-to-route-anchor ride legs (in the direction and costing actually
  requested by confirmation);
- TODA-to-board route geometry used to prove access-path termination;
- repeated static matrix batches used during network construction or
  confirmation.

Prefer precomputing the bounded hot set during/after snapshot construction. If
the complete set is too large, use a bounded long-lived cache keyed by snapshot
version, costing, exact source, and exact ordered targets. Only successful,
validated responses should receive the normal lifetime; failures and timeouts
must not be cached as valid disconnections.

Passenger-dependent work remains request-specific unless an exact-coordinate,
bounded cache is deliberately introduced:

- live origin to boarding anchors;
- alighting anchors to live destination;
- live origin/destination to TODAs;
- direct live origin-to-destination access;
- reroute GPS coordinates;
- geometry whose endpoint is a passenger coordinate.

Do not quantize arbitrary live GPS coordinates. Nearby points can be separated by
walls, rivers, divided highways, or inaccessible crossings and route differently.
An optional cross-request cache for live pairs may only use exact coordinates,
costing, direction, Valhalla/network version, bounded size, and short TTL. Its hit
rate must justify its memory and privacy/cardinality cost. The existing
request-local exact task cache should remain because it safely deduplicates work
inside one plan.

### Routing admission control

Add a bounded singleton admission controller around route-planning work, before
network/candidate generation. It is separate from the Valhalla gate:

- a configurable maximum number of actively planning requests;
- a configurable bounded FIFO queue;
- cancellation-aware waiting using the request token;
- a configurable maximum queue wait;
- immediate saturation response when the queue is full, mapped consistently to
  `429` or `503` with `Retry-After`;
- queue-depth, active-count, wait-time, timeout, cancellation, and rejection
  telemetry.

Apply it to journey planning and rerouting paths that invoke `IRoutingService`,
without gating unrelated navigation, health, admin, or place-search endpoints.
Use a decorator or one outer routing boundary so fallback passes and nested calls
do not acquire twice. Limits are configuration, not hardcoded constants, and are
chosen only after finding the 10/15/20-VU saturation point.

## Phase A telemetry contract

Phase A emits one structured `TukiRoutingPlan` summary per
`POST /api/journeys/plan`, with a plan ID, source, outcome, total elapsed time,
per-pass snapshots, counts, scalar values, and observations. Observations retain
count, sum, and maximum so concurrently completed Valhalla calls are not reduced
to a misleading single duration.

Required fields/counters are:

| Area | Metric |
| --- | --- |
| Correlation/outcome | `PlanId`, `Source`, `Outcome`, `ElapsedMs`, pass `MaxTransfers` |
| Initialization | `network_initialization_ms`, builds/hits, route/TODA counts |
| Candidate pipeline | generated, after access expansion, after dedupe, selected by edge family, total selected, confirmed by family |
| Candidate substages | `access_discovery_ms`, `transfer_candidate_generation_ms`, `access_expansion_ms`, `hard_constraint_filter_ms`, `candidate_key_generation_ms`, `candidate_dedupe_ms`, `diversity_selection_ms` |
| Valhalla | route/matrix HTTP call counts, configured concurrency, gate wait time, execution time, plus access-discovery/confirmation attribution |
| Caches | request-local exact matrix hits/misses with stage attribution; later global/static hits/misses when applicable |
| Phases | candidate generation, confirmation, pruning, geometry enrichment, persistence, routing service, total request |
| Result/fallback | selected and eligible plan counts, fallback-used count |

Do not log raw passenger coordinates or unbounded candidate keys. Logs must be
queryable by plan ID and outcome. Existing `TukiRequest` HTTP metrics remain for
endpoint/status dashboards; `TukiRoutingPlan.ElapsedMs` is the correlated total
for the planning endpoint.

## Correctness invariants

Every phase must preserve:

- looping and retraced-route behavior;
- full-route progress and occurrence semantics;
- self-transfer handling;
- preferred/fallback transfer-depth behavior;
- route, boarding, access-mode, TODA, and objective diversity protections;
- boarding and alighting selection semantics;
- walk/tricycle/jeepney continuity and access limits;
- fare calculations and configured mode-speed normalization;
- fastest, cheapest, efficient, budget, and preference behavior;
- existing regression fixtures and the complete backend test suite.

For known fixtures, compare canonical outputs before and after each phase: route
sequence, mode sequence, occurrence/progress anchors, boarding/alighting
coordinates, transfers, access modes/TODAs, fare, time, generalized cost, and
recommendation/objective labels. Ignore only intentionally nondeterministic IDs.

## Incremental rollout and validation

Every phase follows the same gate:

1. Run the full backend test suite.
2. Compare canonical outputs for all known routing regression fixtures.
3. Run the deterministic benchmark at 1 VU and 10 VUs with Gemini/navigation
   speech disabled.
4. Compare plan average/median/p95, success rates, HTTP/flow failures, Valhalla
   calls and wait/execution, CPU, memory, GC, database time, and output parity to
   the 10-VU baseline above.
5. Do not proceed if output parity or existing tests regress.

### A. Instrumentation only

- Add correlated total, pass, phase, candidate, cache, fallback, and Valhalla
  telemetry.
- Add a reproducible constant-VU benchmark profile for 1 and 10 VUs.
- Make no concurrency, ranking, caching, or lifetime change.
- Use the results to quantify snapshot build cost, Valhalla calls per plan,
  confirmation fan-out, gate wait versus execution, and fallback frequency.

### B. Shared network snapshot

- Extract only immutable network loading/derivation from `RoutingService`.
- Introduce versioned, atomic provider/builder behavior and targeted active-network
  invalidation.
- Keep request state and repositories scoped.
- Measure removal of per-request DB/build work and verify one snapshot version per
  plan/fallback pair.

### C. Global Valhalla gate

- Replace per-instance semaphore ownership with the singleton process-wide gate.
- Tune from measured Valhalla capacity, initially changing only ownership, not
  the measured effective limit unless the existing aggregate behavior is unsafe.
- Validate cancellation, failures, permit release, wait metrics, and no deadlocks.

### D. Candidate diversity/key computation

The post-B/C p95 evidence changes this phase. Candidate generation consumes
72%--82% of tail routing time, and diversity selection is its largest measured
substage. Confirmation remains material but is not the primary tail bottleneck.
Do not introduce progressive confirmation first.

#### D1. Reuse existing candidate computations

- Profile allocations and invocation counts inside the diversity selector before
  changing it, with special attention to journey, boarding, access profile,
  occurrence, objective score, fare, time, and access scalar key construction.
- Compute each value once per candidate and carry it through dedupe and diversity
  selection instead of regenerating equivalent keys and scores for each diversity
  slice or ordering operation.
- Preserve the existing comparer sequence, stable ordering, quotas, seen-set
  behavior, candidate families, and `MaxCandidatesToConfirm = 300` ceiling.
- In a test/shadow harness, compare the exact ordered candidate keys selected by
  the current and optimized implementations. Cover known routing fixtures plus
  tie-heavy, looping, self-transfer, repeated-occurrence, TODA/access-profile,
  and randomized candidate sets.
- Benchmark D1 independently. Do not combine it with confirmation batching or a
  new selection algorithm, so its output parity and performance are attributable.

#### D2. Exact bounded selection, only if D1 leaves selection dominant

- Consider avoiding full repeated sorts only after D1 is measured. Any bounded
  selector must reproduce LINQ's stable ordering exactly, using original input
  position as the final tie-break and applying existing seen/diversity rules in
  the same order.
- Require exact equality of the full ordered selected-candidate sequence against
  the reference selector before enabling it.
- Re-profile after D2 before considering transfer candidate generation changes.

#### Deferred progressive confirmation

Progressive confirmation is now contingent, not part of the first Phase D
implementation. Reconsider it only if D1/D2 measurements show confirmation has
become at least 50% of p95 routing wall time, or candidate generation and
confirmation are both at least 30% in a comparable fixed-dataset run. These are
decision gates, not production tuning constants. Start with shadow-mode
batch/sufficiency telemetry while executing the full current pipeline, retain the
complete 300 ceiling for difficult cases, and compare all objective and diversity
outputs before any early stop is enabled.

### E. Static matrix precomputation/cache

- Classify exact static pairs from the snapshot and add versioned precomputation
  or a bounded cache.
- Keep live coordinate work exact and request-specific by default.
- Record hits, misses, build cost, entry count, evictions, and avoided Valhalla
  calls. Invalidate by atomic snapshot version change.

### F. Routing admission control

Implemented in `RoutingAdmissionControl.cs` as a singleton FIFO controller and
an outer `IRoutingService` decorator. One permit covers both preferred and
fallback transfer-depth passes. Nearby-route discovery and unrelated endpoints
are not gated. Queue cancellation/removal is O(1); full queues and queue-wait
timeouts return `429` with `Retry-After` through
`RoutingAdmissionExceptionMiddleware`.

The limits remain configuration under `Routing:AdmissionControl`. The initial
host benchmark found four active planners more stable than six: at 10 VUs, four
completed 111/111 plans while six increased intrinsic routing time and produced
a client timeout. The initial queue is bounded at eight with a 25-second wait;
these are deployment starting values, not universal constants. Overload clients
and k6 honor `Retry-After`, preventing immediate-retry storms. Repeat tuning on
an isolated/restarted Valhalla process because restarting only the backend does
not clear outstanding Valhalla work between probes.

## Expected impact and measurements still required

B/C already removed repeated network construction and made Valhalla backpressure
process-wide, improving 10-VU average latency and stability while leaving p95
nearly flat. Tail telemetry now identifies diversity selection and repeated key
work as the largest actionable area. D1 is therefore the most likely next p95
gain: it removes CPU and allocation work while keeping the current candidate set,
ordering, diversity rules, and confirmation workload unchanged.

Valhalla confirmation still warrants continued measurement. The instrumented
p95 cohort spent 27.6% in confirmation and showed substantial confirmation-gate
wait, so it may become dominant after candidate CPU work is reduced. That is the
measurement threshold for revisiting progressive confirmation rather than a
reason to change confirmation semantics now.

Measure before choosing implementation constants:

- p50/p95 snapshot initialization time and CPU allocation per request;
- candidate counts at each stage and by transfer depth/objective/access family;
- actual Valhalla HTTP calls per plan versus logical matrix cache hits/misses;
- Valhalla gate wait and execution distributions at 1, 10, 15, and 20 VUs;
- invocation/allocation counts for every key, score, and sort used by diversity
  selection;
- maximum simultaneous confirmation tasks and calls;
- preferred-pass no-route/fallback frequency and incremental fallback cost;
- geometry and persistence contribution to endpoint total;
- repeat rate for exact static and exact live coordinate pairs;
- Valhalla CPU/queue capacity and the application concurrency knee;
- routing queue wait users will tolerate before timeout.

## Recommended implementation order

1. **A — instrumentation and reproducible benchmark**
2. **B — shared immutable network snapshot**
3. **C — process-wide Valhalla gate**
4. **D1 — reuse diversity keys/scores with exact ordered-output parity**
5. **D2 — exact bounded diversity selection, only if D1 leaves it dominant**
6. **Deferred — progressive confirmation shadowing, only if confirmation becomes dominant**
7. **E — versioned static matrix/route cache**
8. **F — bounded routing admission control**

B precedes static caching so cache ownership and invalidation share one network
version. C provides known global backpressure for all subsequent measurements.
Candidate computation comes before progressive confirmation because measured
p95 is currently CPU-selection dominated. Admission control comes last because
it protects a tuned pipeline; it should not conceal avoidable internal work.

The smallest safe post-B/C diagnostic PR/commit is the telemetry change recorded
above: candidate substage timings, stage-attributed Valhalla/cache observations,
focused tests, benchmark evidence, and this revised plan. It contains no changed
limits, lifetimes, candidate selection, caching, or routing output behavior. The
smallest safe optimization PR after that is D1 only; do not combine it with D2 or
progressive confirmation.
