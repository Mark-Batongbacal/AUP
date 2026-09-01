#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 8 ]]; then
  echo "usage: $0 ES_CONTAINER STAGE_DIR INDEX REPO_NAME SNAPSHOT_NAME EXPECTED_COUNT VERIFY_QUERY TARGET_STATUS" >&2
  exit 64
fi

es="$1"
stage="$2"
index="$3"
repo_name="$4"
snapshot_name="$5"
expected_count="$6"
verify_query="$7"
target_status="$8"
temp="tuki-pelias-snapshot-target-${snapshot_name}"

image_ref="$(
  docker inspect "$es" |
  python3 -c 'import json,sys; print(json.load(sys.stdin)[0]["Config"]["Image"])'
)"

wait_es() {
  local container="$1"
  local ready=0
  for _ in $(seq 1 90); do
    if docker exec "$container" curl --fail --silent \
      'http://127.0.0.1:9200/_cluster/health?wait_for_status=yellow&timeout=2s' \
      >/dev/null 2>&1; then
      ready=1
      break
    fi
    sleep 2
  done
  [[ "$ready" -eq 1 ]]
}

cleanup() {
  docker rm -f "$temp" >/dev/null 2>&1 || true
  docker start "$es" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker rm -f "$temp" >/dev/null 2>&1 || true
docker stop "$es" >/dev/null

docker run --detach --name "$temp" \
  --volumes-from "$es" \
  --volume "$stage:/snapshots" \
  --env discovery.type=single-node \
  --env ES_JAVA_OPTS=-Xms512m -Xmx512m \
  --ulimit nofile=65536:65536 \
  --ulimit memlock=-1:-1 \
  --cap-add IPC_LOCK \
  "$image_ref" -Epath.repo=/snapshots >/dev/null

wait_es "$temp"

if [[ "$target_status" == "200" ]]; then
  docker exec "$temp" curl --fail --silent \
    -H 'Content-Type: application/json' \
    -X PUT \
    -d '{"type":"fs","settings":{"location":"/snapshots/previous"}}' \
    http://127.0.0.1:9200/_snapshot/tuki_previous >/dev/null

  docker exec "$temp" curl --fail --silent \
    -H 'Content-Type: application/json' \
    -X PUT \
    -d "{\"indices\":\"$index\",\"include_global_state\":false}" \
    "http://127.0.0.1:9200/_snapshot/tuki_previous/before-replace-${snapshot_name}?wait_for_completion=true" |
  python3 -c 'import json,sys; d=json.load(sys.stdin); assert d["snapshot"]["state"]=="SUCCESS"'

  docker exec "$temp" curl --fail --silent \
    -X DELETE "http://127.0.0.1:9200/$index" >/dev/null
fi

docker exec "$temp" curl --fail --silent \
  -H 'Content-Type: application/json' \
  -X PUT \
  -d '{"type":"fs","settings":{"location":"/snapshots/source","readonly":true}}' \
  "http://127.0.0.1:9200/_snapshot/$repo_name" >/dev/null

docker exec "$temp" curl --fail --silent \
  -H 'Content-Type: application/json' \
  -X POST \
  -d "{\"indices\":\"$index\",\"include_global_state\":false}" \
  "http://127.0.0.1:9200/_snapshot/$repo_name/$snapshot_name/_restore?wait_for_completion=true" \
  >/dev/null

count="$(
  docker exec "$temp" curl --fail --silent "http://127.0.0.1:9200/$index/_count" |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["count"])'
)"
[[ "$count" -eq "$expected_count" ]]

phrase="$(
  docker exec "$temp" curl --fail --silent --get \
    --data-urlencode "q=phrase.default:\"$verify_query\"" \
    "http://127.0.0.1:9200/$index/_count" |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["count"])'
)"
[[ "$phrase" -gt 0 ]]

docker stop "$temp" >/dev/null
docker rm "$temp" >/dev/null 2>&1 || true
docker start "$es" >/dev/null
wait_es "$es"

trap - EXIT
