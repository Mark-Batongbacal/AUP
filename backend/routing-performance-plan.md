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

Validation after B/C: all 461 backend tests pass. The isolated 1-VU run recorded
4.78 s average, 5.29 s median, 8.63 s p95, and 100% plan/flow success. The
isolated 10-VU run recorded 6.78 s average, 8.11 s median, 20.07 s p95, 100%
plan/flow success, and 0% failed HTTP requests. Relative to the supplied 10-VU
baseline, average improved 34.8%, median improved 16.1%, and p95 was effectively
flat (20.12 s to 20.07 s).

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

### Progressive, diverse confirmation

Retain the existing ranked/diverse sequence and the 300 transit hard ceiling, but
consume it progressively:

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
| Valhalla | route/matrix HTTP call counts, configured concurrency, gate wait time, execution time |
| Caches | request-local exact matrix hits/misses; later global/static hits/misses when applicable |
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

### D. Progressive confirmation

- First add shadow-mode batch/sufficiency telemetry with full current execution.
- Enable diverse batches only after shadow output proves the stop rule safe.
- Retain the full 300 ceiling and expand to it whenever required.
- Compare every objective and diversity fixture, not just the top recommendation.

### E. Static matrix precomputation/cache

- Classify exact static pairs from the snapshot and add versioned precomputation
  or a bounded cache.
- Keep live coordinate work exact and request-specific by default.
- Record hits, misses, build cost, entry count, evictions, and avoided Valhalla
  calls. Invalidate by atomic snapshot version change.

### F. Routing admission control

- Add bounded active planning and queue limits with cancellation, timeout, and
  saturation behavior.
- Tune from the established capacity knee and define operational overload SLOs.
- Load-test 15 and 20 VUs after 1/10-VU parity to prove graceful degradation
  instead of request/Valhalla collapse.

## Expected impact and measurements still required

The largest likely throughput/latency gain is progressive confirmation because
the current pipeline can schedule up to 300 transit and 300 access-path edges,
with several Valhalla operations per transit candidate. It directly attacks the
resource whose overload shape matches the nonlinear 10-to-20-VU degradation.
This remains a hypothesis until Phase A shows that confirmation/Valhalla call
count dominates total time.

The shared snapshot is the highest-confidence early optimization: the code proves
that identical DB reads, geometry sampling, anchor generation, and interchange
construction repeat per request (and again on fallback). Its gain may be smaller
than progressive confirmation if Valhalla dominates, but it has a narrower
correctness surface.

Measure before choosing implementation constants:

- p50/p95 snapshot initialization time and CPU allocation per request;
- candidate counts at each stage and by transfer depth/objective/access family;
- actual Valhalla HTTP calls per plan versus logical matrix cache hits/misses;
- Valhalla gate wait and execution distributions at 1, 10, 15, and 20 VUs;
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
4. **D — progressive confirmation, shadow mode first**
5. **E — versioned static matrix/route cache**
6. **F — bounded routing admission control**

B precedes static caching so cache ownership and invalidation share one network
version. C precedes progressive confirmation so batch measurements occur under
known global backpressure. Admission control comes last because it protects a
tuned pipeline; it should not conceal avoidable internal work.

The smallest safe first PR/commit is Phase A only: telemetry primitives and
wiring, focused telemetry tests, the constant-VU benchmark profile, and this
plan. It must contain no changed limits, lifetimes, candidate selection, caching,
or routing output behavior.
