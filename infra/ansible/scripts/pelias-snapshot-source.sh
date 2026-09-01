#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 6 ]]; then
  echo "usage: $0 ES_CONTAINER REPO_DIR ARCHIVE INDEX REPO_NAME SNAPSHOT_NAME" >&2
  exit 64
fi

es="$1"
repo_dir="$2"
archive="$3"
index="$4"
repo_name="$5"
snapshot_name="$6"
temp="tuki-pelias-snapshot-source-${snapshot_name}"

image_ref="$(
  docker inspect "$es" |
  python3 -c 'import json,sys; print(json.load(sys.stdin)[0]["Config"]["Image"])'
)"

wait_es() {
  local container="$1"
  local running

  for _ in $(seq 1 90); do
    running="$(
      docker inspect "$container" |
      python3 -c 'import json,sys; x=json.load(sys.stdin); print(str(bool(x and x[0]["State"]["Running"])).lower())'
    )"

    if [[ "$running" != "true" ]]; then
      echo "Elasticsearch container '$container' exited before becoming healthy." >&2
      docker logs "$container" >&2 || true
      return 1
    fi

    if docker exec "$container" curl --fail --silent \
      'http://127.0.0.1:9200/_cluster/health?wait_for_status=yellow&timeout=2s' \
      >/dev/null 2>&1; then
      return 0
    fi

    sleep 2
  done

  echo "Timed out waiting for Elasticsearch container '$container'." >&2
  docker logs "$container" >&2 || true
  return 1
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
  --volume "$repo_dir:/snapshots" \
  --env "discovery.type=single-node" \
  --env "path.repo=/snapshots" \
  --env "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
  --ulimit nofile=65536:65536 \
  --ulimit memlock=-1:-1 \
  --cap-add IPC_LOCK \
  "$image_ref" >/dev/null

wait_es "$temp"

docker exec "$temp" curl --fail --silent \
  -H 'Content-Type: application/json' \
  -X PUT \
  -d '{"type":"fs","settings":{"location":"/snapshots"}}' \
  "http://127.0.0.1:9200/_snapshot/$repo_name" >/dev/null

docker exec "$temp" curl --fail --silent \
  -H 'Content-Type: application/json' \
  -X PUT \
  -d "{\"indices\":\"$index\",\"include_global_state\":false}" \
  "http://127.0.0.1:9200/_snapshot/$repo_name/$snapshot_name?wait_for_completion=true" |
python3 -c 'import json,sys; d=json.load(sys.stdin); assert d["snapshot"]["state"]=="SUCCESS"'

docker stop "$temp" >/dev/null
docker rm "$temp" >/dev/null 2>&1 || true
docker start "$es" >/dev/null
wait_es "$es"

tar -C "$repo_dir" -czf "$archive" .
chmod 0600 "$archive"
test -s "$archive"

trap - EXIT
