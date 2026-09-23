# Infrastructure

This directory contains Luna's container definitions and deployment scripts.

## Compose Files

- `docker-compose.yml` provides the base local stack.
- `docker-compose.dev.yml` exposes backend service ports for local development.
- `docker-compose.prod.yml` is the application half of the production Compose definition. The server receives it from the published frontend image; server-only values stay in `/opt/luna/.env`.
- `docker-compose.observability.yml` defines the SigNoz backend and OTLP collector. It can run independently for local development and is deployed with the production file as one Compose project.

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

Start or stop observability independently:

```bash
docker compose -f infrastructure/docker-compose.observability.yml up -d
docker compose -f infrastructure/docker-compose.observability.yml down
```

See [observability documentation](../documentation/observability.md) for OTLP configuration and checkout trace verification.

Provision the tracked Luna Service Health dashboard with a SigNoz API key supplied through the shell:

```bash
SIGNOZ_API_TOKEN='paste-token-in-your-shell-only' \
   bash infrastructure/observability/provision-luna-dashboard.sh
```

## Deployment Synchronization

The frontend image packages the production deployment files under `/opt/luna-deployment/`. The server-side updater pulls that image first, extracts the production and observability Compose files plus their configuration directory into an immutable release directory, validates the complete Compose configuration, then pulls and starts the complete application and observability stack.

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
   |             +-- docker-compose.observability.yml
   |             +-- observability/
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
 validate Compose configuration
          |
          v
 pull all service images
          |
          v
 docker compose up --wait and verify health
```

`update.sh` is the versioned source for the updater. On the server, `/opt/luna/update.sh` follows `/opt/luna/current/update.sh`, while `/opt/luna/current` points to the last release that passed validation and health checks. Previous releases remain under `/opt/luna/releases/` for rollback.

The updater uses the fixed Compose project name `luna`, validates the merged production and observability configuration before changing containers, waits for Compose health checks, and probes the frontend and SigNoz health endpoints before activating the release.

To inspect the active release on the server:

```bash
readlink -f /opt/luna/current
docker compose \
   --project-name luna \
   --env-file /opt/luna/.env \
   -f /opt/luna/current/docker-compose.prod.yml \
   -f /opt/luna/current/docker-compose.observability.yml \
   ps
```
