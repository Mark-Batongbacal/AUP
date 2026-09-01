#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "${script_directory}/.." && pwd)"
fixture_path="${ROUTING_FIXTURE:-${script_directory}/fixtures/routing-network-current-152.json}"
checksum_path="${fixture_path}.sha256"
benchmark_port="${BENCHMARK_PORT:-5132}"
benchmark_vus="${BENCHMARK_VUS:-1}"
benchmark_duration="${BENCHMARK_DURATION:-3m}"
result_prefix="${RESULT_PREFIX:-routing-${benchmark_vus}vu}"
summary_path="${SUMMARY_PATH:-${script_directory}/${result_prefix}.json}"
server_log_path="${SERVER_LOG_PATH:-${script_directory}/${result_prefix}-server.log}"

if [[ ! -f "${fixture_path}" || ! -f "${checksum_path}" ]]; then
  echo "Routing fixture or checksum is missing: ${fixture_path}" >&2
  exit 2
fi

(
  cd "$(dirname "${fixture_path}")"
  sha256sum --check "$(basename "${checksum_path}")"
)
expected_sha256="$(cut -d' ' -f1 "${checksum_path}")"

if curl --silent --fail "http://127.0.0.1:${benchmark_port}/health" >/dev/null 2>&1; then
  echo "Port ${benchmark_port} already has a responding server; choose BENCHMARK_PORT." >&2
  exit 2
fi

(
  cd "${repository_root}/backend"
  export RoutingBenchmarkNetwork__SnapshotPath="${fixture_path}"
  export RoutingBenchmarkNetwork__ExpectedSha256="${expected_sha256}"
  export ASPNETCORE_ENVIRONMENT=Development
  exec dotnet ./bin/Debug/net9.0/backend.dll \
    --urls "http://127.0.0.1:${benchmark_port}"
) > "${server_log_path}" 2>&1 &
server_pid=$!

cleanup() {
  kill -INT "${server_pid}" >/dev/null 2>&1 || true
  wait "${server_pid}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

for _ in $(seq 1 90); do
  if curl --silent --fail "http://127.0.0.1:${benchmark_port}/health" >/dev/null 2>&1; then
    break
  fi
  if ! kill -0 "${server_pid}" >/dev/null 2>&1; then
    echo "Benchmark server exited during startup. See ${server_log_path}." >&2
    exit 1
  fi
  sleep 1
done

if ! curl --silent --fail "http://127.0.0.1:${benchmark_port}/health" >/dev/null; then
  echo "Benchmark server did not become healthy. See ${server_log_path}." >&2
  exit 1
fi

cd "${script_directory}"
k6_arguments=(
  run
  "--summary-export=${summary_path}"
  -e "BASE_URL=http://127.0.0.1:${benchmark_port}"
  -e PROFILE=benchmark
  -e "BENCHMARK_VUS=${benchmark_vus}"
  -e "BENCHMARK_DURATION=${benchmark_duration}"
  -e ROUTING_ONLY=true
  -e THINK_TIME_SECONDS=0
  -e BETWEEN_TRIPS_SECONDS=0
  -e DISABLE_SERVER_AI=true
  -e TRIPS_FILE=./tuki-trips.example.json
)
if [[ -n "${BENCHMARK_TRIP_ID:-}" ]]; then
  k6_arguments+=(-e "BENCHMARK_TRIP_ID=${BENCHMARK_TRIP_ID}")
fi
if [[ -n "${FIXED_PREFERENCE:-}" ]]; then
  k6_arguments+=(-e "FIXED_PREFERENCE=${FIXED_PREFERENCE}")
fi
k6 "${k6_arguments[@]}" tuki-load.js
