---
name: signoz-observability
description: "Query the local Luna SigNoz installation for application logs, distributed traces, trace-correlated records, and service health. Use when investigating checkout failures, HTTP 500s, missing logs, trace propagation, service latency, or telemetry ingestion."
argument-hint: "logs|traces|trace|health [service or trace id]"
user-invocable: true
disable-model-invocation: false
---

# SigNoz Observability

Use the bundled read-only script to inspect the local Luna telemetry store. The script queries ClickHouse through Docker and does not mutate SigNoz, ClickHouse, telemetry data, or application services.

## Commands

Run from the repository root:

```bash
bash .github/skills/signoz-observability/scripts/query-signoz.sh health
bash .github/skills/signoz-observability/scripts/query-signoz.sh logs luna-orders 60 50
bash .github/skills/signoz-observability/scripts/query-signoz.sh logs luna-orders 60 50 checkout
bash .github/skills/signoz-observability/scripts/query-signoz.sh traces luna-orders 60 100
bash .github/skills/signoz-observability/scripts/query-signoz.sh trace TRACE_ID
```

Arguments:

- `health` checks the SigNoz HTTP health endpoint and the ClickHouse container.
- `logs [service] [lookback_minutes] [limit] [body_search]` returns JSON lines with timestamp, service, severity, body, trace ID, span ID, and deployment environment.
- `traces [service] [lookback_minutes] [limit]` returns JSON lines with trace/span hierarchy, operation, duration, status, HTTP, database, and error fields.
- `trace [trace_id]` returns all spans and logs belonging to one W3C trace ID.

The default container is `luna-signoz-clickhouse`. Override it only when the local Compose project uses a different name:

```bash
SIGNOZ_CLICKHOUSE_CONTAINER=my-clickhouse \
  bash .github/skills/signoz-observability/scripts/query-signoz.sh logs luna-orders
```

## Investigation workflow

1. Check `health` before interpreting an empty result.
2. Query `logs` for the affected service and a narrow time window.
3. Use the returned `trace_id` to query `trace`.
4. Compare the Orders span with Catalog, Shipping, Inventory, and Payments child spans.
5. Correlate log `trace_id` and `span_id` with the span tree. `X-Correlation-ID` is a separate Luna request identifier.
6. Report whether the result is empty because there is no telemetry, the time window is wrong, the service filter is wrong, or the local containers are unavailable.

## Access and safety

The local self-hosted stack currently needs no SigNoz user for these Docker-local read-only queries. Docker access is required. Do not add SigNoz passwords, API tokens, ClickHouse credentials, or customer/payment data to the repository or command output.

For a remote SigNoz installation, use a read-only service account/token supplied through the environment or an approved secret store. Extend the script only with an authenticated read-only API path; do not hardcode credentials.

The script intentionally does not accept arbitrary SQL. If a query outside these views is needed, inspect the schema first and request explicit approval before adding a new read-only query.

## Resource

- [Read-only query script](./scripts/query-signoz.sh)
