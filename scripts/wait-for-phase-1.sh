#!/usr/bin/env bash
set -euo pipefail

base_url="${PLAYWRIGHT_BASE_URL:-http://localhost:5173}"
identity_base_url="${PLAYWRIGHT_IDENTITY_BASE_URL:-http://localhost:5111}"
deadline=$((SECONDS + 180))

until curl --fail --silent --show-error "$base_url" >/dev/null; do
  if (( SECONDS >= deadline )); then
    echo "Timed out waiting for the frontend at $base_url." >&2
    exit 1
  fi

  sleep 2
done

until curl --fail --silent --show-error "$identity_base_url/health" >/dev/null; do
  if (( SECONDS >= deadline )); then
    echo "Timed out waiting for the Identity service at $identity_base_url." >&2
    exit 1
  fi

  sleep 2
done

curl --fail --silent --show-error \
  --request POST "$identity_base_url/api/v1/tenants" \
  --header 'Content-Type: application/json' \
  --data "$(jq -n \
    --arg tenantName 'Phase 1 Pilot' \
    --arg ownerName "${PLAYWRIGHT_PILOT_NAME:?PLAYWRIGHT_PILOT_NAME is required}" \
    --arg ownerEmail "${PLAYWRIGHT_PILOT_EMAIL:?PLAYWRIGHT_PILOT_EMAIL is required}" \
    --arg initialPassword "${PLAYWRIGHT_PILOT_PASSWORD:?PLAYWRIGHT_PILOT_PASSWORD is required}" \
    '{tenantName: $tenantName, ownerName: $ownerName, ownerEmail: $ownerEmail, initialPassword: $initialPassword}')" \
  >/dev/null
