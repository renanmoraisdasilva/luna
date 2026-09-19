#!/usr/bin/env bash
set -euo pipefail

container="${SIGNOZ_CLICKHOUSE_CONTAINER:-luna-signoz-clickhouse}"
health_url="${SIGNOZ_HEALTH_URL:-http://localhost:8080/api/v1/health}"

usage() {
  cat <<'USAGE'
Usage:
  query-signoz.sh health
  query-signoz.sh logs [service] [lookback_minutes] [limit] [body_search]
  query-signoz.sh traces [service] [lookback_minutes] [limit]
  query-signoz.sh trace <trace_id>

Environment:
  SIGNOZ_CLICKHOUSE_CONTAINER  ClickHouse container name. Default: luna-signoz-clickhouse
  SIGNOZ_HEALTH_URL            SigNoz health URL. Default: http://localhost:8080/api/v1/health
USAGE
}

require_uint() {
  local value="$1"
  local name="$2"
  if [[ ! "$value" =~ ^[0-9]+$ ]] || (( value == 0 )); then
    printf '%s must be a positive integer\n' "$name" >&2
    exit 2
  fi
}

require_trace_id() {
  local trace_id="$1"
  if [[ ! "$trace_id" =~ ^[0-9a-fA-F]{32}$ ]]; then
    printf 'trace_id must be a 32-character W3C hexadecimal trace ID\n' >&2
    exit 2
  fi
}

require_service_name() {
  local service="$1"
  if [[ -n "$service" && ! "$service" =~ ^[A-Za-z0-9._-]+$ ]]; then
    printf 'service must contain only letters, numbers, dots, underscores, and hyphens\n' >&2
    exit 2
  fi
}

sql_quote() {
  local value="$1"
  value=${value//\'/\'\'}
  printf "'%s'" "$value"
}

run_query() {
  local query="$1"
  docker exec "$container" clickhouse-client --query "$query"
}

health() {
  curl --fail --silent --show-error "$health_url"
  printf '\n'
  docker exec "$container" clickhouse-client --query "SELECT 1" >/dev/null
  printf 'clickhouse: ok\n'
}

logs() {
  local service="${1:-}"
  local lookback="${2:-60}"
  local limit="${3:-50}"
  local body_search="${4:-}"

  require_uint "$lookback" lookback_minutes
  require_uint "$limit" limit
  require_service_name "$service"
  if (( limit > 1000 )); then
    printf 'limit must be 1000 or less\n' >&2
    exit 2
  fi

  local service_sql
  local body_search_sql
  service_sql=$(sql_quote "$service")
  body_search_sql=$(sql_quote "$body_search")

  local query
  query=$(cat <<'SQL'
SELECT
  fromUnixTimestamp64Nano(l.timestamp) AS timestamp,
  l.resources_string['service.name'] AS service_name,
  l.resources_string['deployment.environment'] AS deployment_environment,
  l.severity_text,
  l.body,
  l.trace_id,
  l.span_id
FROM signoz_logs.logs_v2 AS l
WHERE l.timestamp >= toUInt64(toUnixTimestamp64Nano(now64(9) - INTERVAL LOOKBACK_MINUTES MINUTE))
  AND (SERVICE_NAME = '' OR l.resources_string['service.name'] = SERVICE_NAME)
  AND (BODY_SEARCH = '' OR positionCaseInsensitive(l.body, BODY_SEARCH) > 0)
ORDER BY timestamp DESC
LIMIT RESULT_LIMIT
FORMAT JSONEachRow
SQL
)

  query=${query//LOOKBACK_MINUTES/$lookback}
  query=${query//SERVICE_NAME/$service_sql}
  query=${query//BODY_SEARCH/$body_search_sql}
  query=${query//RESULT_LIMIT/$limit}
  run_query "$query"
}

traces() {
  local service="${1:-}"
  local lookback="${2:-60}"
  local limit="${3:-100}"

  require_uint "$lookback" lookback_minutes
  require_uint "$limit" limit
  require_service_name "$service"
  if (( limit > 2000 )); then
    printf 'limit must be 2000 or less\n' >&2
    exit 2
  fi

  local service_sql
  service_sql=$(sql_quote "$service")

  local query
  query=$(cat <<'SQL'
SELECT
  timestamp,
  trace_id,
  span_id,
  parent_span_id,
  serviceName AS service_name,
  name,
  round(duration_nano / 1000000, 3) AS duration_ms,
  status_code_string,
  has_error,
  httpMethod AS http_method,
  httpRoute AS http_route,
  dbSystem AS db_system,
  dbOperation AS db_operation
FROM signoz_traces.distributed_signoz_index_v3
WHERE timestamp >= now64(9) - INTERVAL LOOKBACK_MINUTES MINUTE
  AND (SERVICE_NAME = '' OR serviceName = SERVICE_NAME)
ORDER BY timestamp DESC
LIMIT RESULT_LIMIT
FORMAT JSONEachRow
SQL
)

  query=${query//LOOKBACK_MINUTES/$lookback}
  query=${query//SERVICE_NAME/$service_sql}
  query=${query//RESULT_LIMIT/$limit}
  run_query "$query"
}

trace() {
  local trace_id="$1"
  require_trace_id "$trace_id"

  local trace_id_sql
  trace_id_sql=$(sql_quote "$trace_id")

  local query
  query=$(cat <<'SQL'
SELECT
  timestamp,
  trace_id,
  span_id,
  parent_span_id,
  serviceName AS service_name,
  name,
  round(duration_nano / 1000000, 3) AS duration_ms,
  status_code_string,
  has_error,
  httpMethod AS http_method,
  httpRoute AS http_route,
  dbSystem AS db_system,
  dbOperation AS db_operation
FROM signoz_traces.distributed_signoz_index_v3
WHERE trace_id = TRACE_ID
ORDER BY timestamp ASC
FORMAT JSONEachRow
SQL
)

  query=${query//TRACE_ID/$trace_id_sql}
  run_query "$query"

  query=$(cat <<'SQL'
SELECT
  fromUnixTimestamp64Nano(timestamp) AS timestamp,
  resources_string['service.name'] AS service_name,
  severity_text,
  body,
  trace_id,
  span_id
FROM signoz_logs.logs_v2
WHERE trace_id = TRACE_ID
ORDER BY timestamp ASC
FORMAT JSONEachRow
SQL
)

  query=${query//TRACE_ID/$trace_id_sql}
  run_query "$query"
}

command="${1:-}"
shift || true

case "$command" in
  health)
    health
    ;;
  logs)
    logs "$@"
    ;;
  traces)
    traces "$@"
    ;;
  trace)
    if [[ "$#" -ne 1 ]]; then
      usage >&2
      exit 2
    fi
    trace "$1"
    ;;
  "")
    usage >&2
    exit 2
    ;;
  *)
    printf 'Unknown command: %s\n' "$command" >&2
    usage >&2
    exit 2
    ;;
esac
