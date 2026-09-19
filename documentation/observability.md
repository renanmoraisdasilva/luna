# Luna Observability

Luna uses OpenTelemetry for instrumentation and OTLP for transport. SigNoz is only the self-hosted backend; no Luna application or domain project references SigNoz.

```text
Next.js
   |
   v
Orders
   |
   +----> Catalog
   |
   +----> Shipping
   |
   +----> Inventory
   |
   +----> Payments
   |
   v
OpenTelemetry
   |
   v
OTLP
   |
   v
SigNoz
```

## Telemetry model

Each host has a distinct `service.name`:

- `luna-frontend`
- `luna-identity`
- `luna-catalog`
- `luna-orders`
- `luna-inventory`
- `luna-payments`
- `luna-shipping`

The shared `Luna.Observability` project registers ASP.NET Core, `HttpClient`, SQL client, runtime, logs, and metrics instrumentation. Standard .NET W3C propagation carries trace context through the existing typed HTTP clients. The Next.js `instrumentation.ts` hook registers server-side OpenTelemetry instrumentation through `@vercel/otel`.

The API hosts continue to use Serilog for the existing console sink. They enable Serilog's `writeToProviders` option so Serilog forwards the existing `ILogger` events to the registered OpenTelemetry logging provider; no second application logging abstraction is introduced.

The existing `X-Correlation-ID` remains a separate application/debug identifier and is preserved by the current middleware. OpenTelemetry `trace_id` identifies one distributed operation, `span_id` identifies one operation within that trace, and `X-Correlation-ID` is Luna's existing request identifier. OpenTelemetry logs include trace and span context when an active span exists; application logs continue to use `ILogger` and Serilog.

Checkout stage logs record the customer and order identifiers, item/product counts, shipping method, order total, reservation identifiers where applicable, and failure stages. Payment methods, credentials, tokens, request bodies, and personal address contents are not logged.

## Local SigNoz

The repository includes an independent Docker Compose stack. It can run before or after the Luna application stack:

```bash
docker compose -f infrastructure/docker-compose.observability.yml up -d
```

The SigNoz UI is available at [http://localhost:8080](http://localhost:8080). OTLP is exposed on ports `4317` (gRPC) and `4318` (HTTP/protobuf).

Start Luna separately using the existing commands:

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.dev.yml \
  up --build
```

Local Luna containers default to `http://host.docker.internal:4317` for .NET OTLP and `http://host.docker.internal:4318` for Next.js OTLP. Override those values in the root `.env` when the collector is elsewhere. Host-side Next.js development uses `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318` in `src/Frontend/.env`.

Stop only SigNoz with:

```bash
docker compose -f infrastructure/docker-compose.observability.yml down
```

Add `-v` only when the local SigNoz ClickHouse, SQLite, and ZooKeeper data should be deleted.

Docker Desktop on Windows may encounter ClickHouse Keeper or filesystem issues in some SigNoz versions. The upstream SigNoz guidance recommends native Docker Engine inside WSL 2 if the stack repeatedly restarts.

## Configuration

The application Compose files support these environment variables:

- `TELEMETRY_ENABLED`: enables or disables .NET telemetry. Local default: `true`; production Compose default: `false` until explicitly enabled.
- `TELEMETRY_ENVIRONMENT`: value exported as `deployment.environment`.
- `OTEL_EXPORTER_OTLP_ENDPOINT`: .NET OTLP endpoint. Production deployments must provide a reachable endpoint when telemetry is enabled.
- `OTEL_EXPORTER_OTLP_HTTP_ENDPOINT`: Next.js OTLP HTTP endpoint.
- `OTEL_SERVICE_NAME_FRONTEND`: optional Next.js service name override.

The .NET configuration equivalents are `Telemetry__Enabled`, `Telemetry__Environment`, and `Telemetry__OtlpEndpoint`. Standard `OTEL_SDK_DISABLED=true` is also honored by the .NET setup.

If the collector is unavailable, OpenTelemetry export is asynchronous and best-effort. Luna requests and checkout do not synchronously depend on SigNoz availability.

## Verify checkout tracing

1. Start SigNoz and Luna with telemetry enabled.
2. Complete a checkout with a valid cart and customer session.
3. In SigNoz, open **Traces** and filter for `service.name = luna-orders`.
4. Open the checkout server span. Its child spans should include the Orders database operation and outbound HTTP spans to Catalog, Shipping, Inventory, and Payments.
5. Select the Inventory or Payments child span to inspect its downstream service trace. Logs emitted during the checkout stages should carry the same `trace_id`; the response's `X-Correlation-ID` remains available for Luna's application-level diagnostics.

This verification requires the dependent services and databases to be running. Tests validate registration, host startup compatibility, W3C-compatible platform instrumentation, and checkout behavior without depending on the SigNoz UI.
