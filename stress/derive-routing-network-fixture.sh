#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 INPUT_JSON TODA_COUNT OUTPUT_JSON FIXTURE_ID" >&2
  exit 2
fi

input_path="$1"
toda_count="$2"
output_path="$3"
fixture_id="$4"

if ! [[ "${toda_count}" =~ ^[0-9]+$ ]]; then
  echo "TODA_COUNT must be a non-negative integer." >&2
  exit 2
fi

available_count="$(jq '.trikePoints | length' "${input_path}")"
if (( toda_count > available_count )); then
  echo "Requested ${toda_count} TODAs, but ${input_path} contains ${available_count}." >&2
  exit 2
fi

mkdir -p "$(dirname "${output_path}")"
jq --sort-keys \
  --arg fixtureId "${fixture_id}" \
  --argjson todaCount "${toda_count}" \
  '.fixtureId = $fixtureId | .trikePoints = (.trikePoints[0:$todaCount])' \
  "${input_path}" > "${output_path}"

(
  cd "$(dirname "${output_path}")"
  sha256sum "$(basename "${output_path}")" > "$(basename "${output_path}").sha256"
)

echo "Derived ${fixture_id} with ${toda_count} TODAs in ${output_path}."
