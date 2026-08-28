# Tuki k6 stress testing

This folder contains the k6 workload for the current `dev` API flow.

The goal is to model **real Tuki users**, not just hammer one endpoint. A virtual user authenticates as an isolated guest, searches a destination, plans a trip, starts navigation, spends most navigation time locally, occasionally sends meaningful backend events, optionally reroutes, then cancels the session.

High-frequency GPS progression is intentionally **not** sent to the backend every second because Tuki's production navigation architecture handles that locally on the device.

## Files

- `tuki-load.js` — main k6 workload
- `tuki-trips.example.json` — sample Porac / Angeles / Clark / Dau / Mabalacat cases

## Safety behavior

The script defaults to:

```text
BASE_URL=http://localhost:5129
PROFILE=smoke
GOOGLE_MORE_RATE=0
AI_RATE=0
```

A non-smoke profile will refuse to run against a remote host unless you explicitly set:

```bash
-e ALLOW_REMOTE_LOAD=YES
```

Google Places expansion and Gemini sampling are disabled by default. If you enable either one, you must also explicitly set:

```bash
-e ENABLE_EXTERNALS=YES
```

This is deliberate so a load test cannot accidentally burn external API quota or billing.

Also note that every VU gets its own guest identity. A large run can therefore create many `UserProfile`, `TripSearch`, `RouteRecommendation`, `RecommendationLeg`, `TripSession`, instruction, and landmark rows. Prefer staging or a disposable database snapshot for the big runs.

## Install k6

On Debian/Ubuntu, install k6 using the official repository, then verify:

```bash
k6 version
```

## 1. Local smoke test

```bash
cd stress
k6 run tuki-load.js
```

This is one VU for 30 seconds.

## Routing optimization benchmark: 1 and 10 VUs

Use the constant-VU benchmark profile before and after every routing optimization
phase. It keeps navigation speech AI disabled and uses the same duration and trip
fixtures at both loads.

One-VU control:

```bash
k6 run \
  --summary-export=tuki-routing-1vu.json \
  -e PROFILE=benchmark \
  -e BENCHMARK_VUS=1 \
  -e BENCHMARK_DURATION=5m \
  -e DISABLE_SERVER_AI=true \
  -e TRIPS_FILE=./tuki-trips.example.json \
  tuki-load.js
```

Ten-VU baseline comparison:

```bash
k6 run \
  --summary-export=tuki-routing-10vu.json \
  -e PROFILE=benchmark \
  -e BENCHMARK_VUS=10 \
  -e BENCHMARK_DURATION=5m \
  -e DISABLE_SERVER_AI=true \
  -e TRIPS_FILE=./tuki-trips.example.json \
  tuki-load.js
```

For a remote target, add `BASE_URL` and `ALLOW_REMOTE_LOAD=YES` explicitly. The
existing latency thresholds are health indicators and may fail against the
recorded pre-optimization baseline; retain the summary and compare the actual
average, median, p95, success, HTTP-failure, and flow-success values.

## 2. Azure smoke test

```bash
cd stress
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  tuki-load.js
```

Smoke is allowed remotely without the load-test confirmation flag.

## 3. Small test: up to 50 users

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=small \
  -e ALLOW_REMOTE_LOAD=YES \
  -e TRIPS_FILE=./tuki-trips.example.json \
  tuki-load.js
```

Ramp:

```text
0 -> 10 -> 25 -> 50 -> 0
```

## 4. Target load: up to 1000 active users

```bash
k6 run \
  --summary-export=tuki-summary.json \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=load \
  -e ALLOW_REMOTE_LOAD=YES \
  -e TRIPS_FILE=./tuki-trips.example.json \
  -e REROUTE_RATE=0.05 \
  -e NAV_HOLD_SECONDS=30 \
  -e BETWEEN_TRIPS_SECONDS=15 \
  tuki-load.js
```

Ramp:

```text
0 -> 50 -> 100 -> 250 -> 500 -> 1000 -> 0
```

Do this only after the smoke and 50-user runs are healthy.

## 5. Spike test

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=spike \
  -e ALLOW_REMOTE_LOAD=YES \
  tuki-load.js
```

The spike profile jumps from 50 to 1000 VUs in 10 seconds.

## 6. Stress test

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=stress \
  -e ALLOW_REMOTE_LOAD=YES \
  tuki-load.js
```

Ramp:

```text
100 -> 250 -> 500 -> 750 -> 1000 -> 1250 -> 1500 -> 0
```

The point of this profile is to find the first real bottleneck, not necessarily to pass every stage.

## 7. Soak test

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=soak \
  -e ALLOW_REMOTE_LOAD=YES \
  tuki-load.js
```

Default soak load is 500 VUs for 2 hours.

## Reroute-heavy testing

Default behavior reroutes roughly 5% of completed flows:

```text
REROUTE_RATE=0.05
REROUTE_REASON=MANUAL
```

Force every flow through a reroute:

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=small \
  -e ALLOW_REMOTE_LOAD=YES \
  -e REROUTE_RATE=1 \
  tuki-load.js
```

Explicit off-route test:

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=small \
  -e ALLOW_REMOTE_LOAD=YES \
  -e REROUTE_RATE=1 \
  -e REROUTE_REASON=OFF_ROUTE \
  tuki-load.js
```

The reroute request includes a fresh GPS fix directly, matching Tuki's current reroute contract.

## Optional Google Places / Gemini sampling

Keep these rates small. They can hit external quotas and billing.

Example:

```bash
k6 run \
  -e BASE_URL=http://tuki-api.japaneast.cloudapp.azure.com:5129 \
  -e PROFILE=small \
  -e ALLOW_REMOTE_LOAD=YES \
  -e ENABLE_EXTERNALS=YES \
  -e GOOGLE_MORE_RATE=0.01 \
  -e AI_RATE=0.005 \
  tuki-load.js
```

Do not set these to `1` during a 1000-user test unless you intentionally want to load-test the external providers too.

## Useful variables

| Variable | Default | Purpose |
| --- | ---: | --- |
| `PROFILE` | `smoke` | `smoke`, `benchmark`, `small`, `load`, `stress`, `spike`, `soak` |
| `BENCHMARK_VUS` | `1` | Constant virtual users for the `benchmark` profile |
| `BENCHMARK_DURATION` | `5m` | Duration for the `benchmark` profile |
| `REROUTE_RATE` | `0.05` | Fraction of trip flows that reroute |
| `REROUTE_REASON` | `MANUAL` | Reroute reason, e.g. `MANUAL` or `OFF_ROUTE` |
| `LOCATION_SYNC_RATE` | `0.05` | Fraction sending one meaningful `/location` sync |
| `ACTIVE_REFRESH_RATE` | `0.10` | Fraction refreshing `/api/navigation/active` |
| `NAV_HOLD_SECONDS` | `4` smoke / `30` others | Simulated local-navigation time |
| `BETWEEN_TRIPS_SECONDS` | `1` smoke / `15` others | Think/idle time between trips |
| `BUDGET_PESOS` | `150` | Planning budget; set `0` to omit |
| `GOOGLE_MORE_RATE` | `0` | Fraction calling `/api/places/search/more` |
| `AI_RATE` | `0` | Fraction asking the active-trip AI endpoint |
| `ENABLE_EXTERNALS` | `false` | Required for Google/Gemini sampling |
| `ALLOW_REMOTE_LOAD` | `false` | Required for non-smoke remote tests |
| `TRIPS_FILE` | built-in cases | JSON file containing custom origin/destination cases |

## Metrics

The workload reports endpoint-specific metrics including:

```text
tuki_auth_duration
tuki_place_search_duration
tuki_plan_duration
tuki_start_duration
tuki_reroute_duration
tuki_cancel_duration

tuki_place_search_success
tuki_plan_success
tuki_start_success
tuki_reroute_success
tuki_cancel_success
tuki_flow_success

tuki_no_route_count
tuki_unexpected_status_count
```

Initial thresholds are intentionally only a starting point:

```text
HTTP failures < 2%
Place search p95 < 2.5 s
Journey planning p95 < 8 s
Navigation start p95 < 4 s
Reroute p95 < 9 s
Cancel p95 < 3 s
```

Once a clean baseline exists, tighten them based on measured production behavior.

## What to monitor during the run

Run k6 from a separate machine or VM. Do not run it on the Azure machine hosting Tuki itself.

At the same time watch:

- Azure VM CPU, RAM, disk and network
- ASP.NET request latency, exceptions, GC and thread-pool pressure
- SQL Server CPU, connections, locks and slow queries
- Valhalla CPU/RAM, route/matrix latency and timeouts
- Pelias/Elasticsearch CPU/RAM and query latency

The useful result is not just "1000 users passed." The real goal is to identify which component saturates first and at what load.
