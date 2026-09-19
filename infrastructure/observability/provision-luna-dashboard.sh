#!/usr/bin/env bash
set -euo pipefail

signoz_url="${SIGNOZ_URL:-http://localhost:8080}"
auth_header="${SIGNOZ_AUTH_HEADER:-SIGNOZ-API-KEY}"
auth_prefix="${SIGNOZ_AUTH_VALUE_PREFIX:-}"
dashboard_id="${SIGNOZ_DASHBOARD_ID:-}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dashboard_file="${SIGNOZ_DASHBOARD_FILE:-$script_dir/luna-service-health-dashboard.json}"

if [[ -z "${SIGNOZ_API_TOKEN:-}" ]]; then
  printf 'SIGNOZ_API_TOKEN must be set; the token is never read from the repository.\n' >&2
  exit 2
fi

if [[ ! -f "$dashboard_file" ]]; then
  printf 'Dashboard file not found: %s\n' "$dashboard_file" >&2
  exit 2
fi

if [[ -n "$dashboard_id" ]]; then
  method="PUT"
  endpoint="$signoz_url/api/v2/dashboards/$dashboard_id"
else
  method="POST"
  endpoint="$signoz_url/api/v2/dashboards"
fi

auth_value="$auth_prefix$SIGNOZ_API_TOKEN"

curl --fail --silent --show-error \
  --request "$method" \
  --url "$endpoint" \
  --header "$auth_header: $auth_value" \
  --header 'Content-Type: application/json' \
  --data-binary "@$dashboard_file"
printf '\nDashboard provisioning request completed using %s %s\n' "$method" "$endpoint"
