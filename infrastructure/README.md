# Infrastructure

This directory contains Luna's container definitions and deployment scripts.

## Compose Files

- `docker-compose.yml` provides the base local stack.
- `docker-compose.dev.yml` exposes backend service ports for local development.
- `docker-compose.prod.yml` is the application half of the production Compose definition. The server receives it from the published frontend image; server-only values stay in `/opt/luna/.env`.
- `docker-compose.observability.yml` remains a local-development compatibility file during migration. Production SigNoz is owned by the separate `server-infra` repository.

Important: the production server must have a real `/opt/luna/.env` file before running the updater. Compose uses it for `${...}` substitutions. If it is missing or malformed, the deployment is rejected before containers are changed.

A sample file is provided at `.env.example` for reference. Copy it to `/opt/luna/.env` on the server and replace the example values with the real database connection strings.

`ORDERS_SERVICE_CLIENT_SECRET` must be a long random secret shared only by Identity and Orders. It is used to issue and validate Orders' service-to-service tokens.

Run the local stack:

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.dev.yml \
   -f infrastructure/docker-compose.observability.yml \
  up --build
```

To start only the backend plus observability services during host-side frontend development:

```bash
docker compose \
   -f infrastructure/docker-compose.yml \
   -f infrastructure/docker-compose.dev.yml \
   -f infrastructure/docker-compose.observability.yml \
   up --build \
   sqlserver identity catalog orders payments inventory shipping \
   signoz signoz-migrator otel-collector
```

For local development only, start or stop the compatibility observability stack:

```bash
docker compose -f infrastructure/docker-compose.observability.yml up -d
docker compose -f infrastructure/docker-compose.observability.yml down
```

See the infrastructure repository's SigNoz documentation for production OTLP configuration and recovery.

Provision the tracked Luna Service Health dashboard with a SigNoz API key supplied through the shell:

```bash
SIGNOZ_API_TOKEN='paste-token-in-your-shell-only' \
   bash infrastructure/observability/provision-luna-dashboard.sh
```

## Deployment Synchronization

The frontend image packages the production application deployment files under `/opt/luna-deployment/`. The server-side updater pulls that image first, extracts the application Compose file into an immutable release directory, validates it, then pulls and starts the Luna application stack. Production SigNoz is reconciled separately by `server-infra`.

```text
Git repo
   |
   v
Luna CI
   |
   v
Docker Publish
   |
   +-- luna-frontend
   |      \-- /opt/luna-deployment/
   |             +-- docker-compose.prod.yml
   |             \-- update.sh
   |
   \-- other Luna images
          |
          v
         GHCR
          |
          v
      Luna server
          |
          v
   pull frontend image
          |
          v
 stage immutable release
          |
          v
 validate application Compose configuration
          |
          v
 pull all service images
          |
          v
 docker compose up --wait and verify frontend health
```

`update.sh` is the versioned source for the updater. On the server, `/opt/luna/update.sh` follows `/opt/luna/current/update.sh`, while `/opt/luna/current` points to the last release that passed validation and health checks. Previous releases remain under `/opt/luna/releases/` for rollback.

The updater uses the fixed Compose project name `luna`, validates the production application configuration before changing containers, waits for Compose health checks, and probes the frontend health endpoint before activating the release.

To inspect the active release on the server:

```bash
readlink -f /opt/luna/current
docker compose \
   --project-name luna \
   --env-file /opt/luna/.env \
   -f /opt/luna/current/docker-compose.prod.yml \
   ps
```
