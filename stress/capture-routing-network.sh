#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:5129}"
output_path="${2:-fixtures/routing-network-current.json}"
fixture_id="${3:-routing-network-current}"
temporary_directory="$(mktemp -d)"
trap 'rm -rf "${temporary_directory}"' EXIT

routes_path="${temporary_directory}/routes.json"
todas_path="${temporary_directory}/todas.json"
route_documents_path="${temporary_directory}/route-documents.jsonl"

curl --fail --silent --show-error \
  "${base_url%/}/api/transport-routes" > "${routes_path}"
curl --fail --silent --show-error \
  "${base_url%/}/api/tricycle-points" > "${todas_path}"

while IFS=$'\t' read -r route_id route_code route_name; do
  points_path="${temporary_directory}/route-${route_id}.json"
  curl --fail --silent --show-error \
    "${base_url%/}/api/transport-routes/${route_id}/points" > "${points_path}"
  jq --compact-output \
    --arg routeId "${route_code}" \
    --arg routeName "${route_name}" \
    '{
      routeId: $routeId,
      routeName: $routeName,
      coordinates: [.points | sort_by(.pointOrder)[] | [.longitude, .latitude]]
    }' "${points_path}" >> "${route_documents_path}"
done < <(jq --raw-output \
  'sort_by([.routeName, .routeCode])[] | [.routeId, .routeCode, .routeName] | @tsv' \
  "${routes_path}")

mkdir -p "$(dirname "${output_path}")"
jq --null-input --sort-keys \
  --arg fixtureId "${fixture_id}" \
  --slurpfile routes "${route_documents_path}" \
  --slurpfile todas "${todas_path}" \
  '{
    schemaVersion: 1,
    fixtureId: $fixtureId,
    routes: $routes,
    trikePoints: [
      $todas[0]
      | sort_by([.pointCode, .tricyclePointId])[]
      | {
          id: .pointCode,
          name: .pointName,
          latitude: .centerLatitude,
          longitude: .centerLongitude
        }
    ]
  }' > "${output_path}"

(
  cd "$(dirname "${output_path}")"
  sha256sum "$(basename "${output_path}")" > "$(basename "${output_path}").sha256"
)

route_count="$(jq '.routes | length' "${output_path}")"
toda_count="$(jq '.trikePoints | length' "${output_path}")"
echo "Captured ${route_count} routes and ${toda_count} TODAs in ${output_path}."
echo "Checksum: $(cut -d' ' -f1 "${output_path}.sha256")"
