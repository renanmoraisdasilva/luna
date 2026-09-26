# Infrastructure

This directory contains Luna's container definitions, local Compose stacks, and observability tooling.

## Compose Files

- `docker-compose.yml` provides the base local stack.
- `docker-compose.dev.yml` exposes backend service ports for local development.
- `docker-compose.prod.yml` is the production Compose definition. Dokploy reads it directly from this repository; server-only values are supplied through the Dokploy application environment.
- `docker-compose.observability.yml` is a local-development compatibility file. Production SigNoz is owned by the separate `server-infra` repository.

Important: `docker-compose.prod.yml` uses `${...}` substitutions and required-value checks (`${VAR:?...}`). Compose rejects the deployment when a required value is missing or malformed, so every required variable must be present in the Dokploy application environment before the stack starts.

A sample file is provided at `.env.example` for reference. Use it as the checklist of required values when configuring the application environment in Dokploy, replacing the example values with the real database connection strings.

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

## Deployment

Luna is deployed by Dokploy, the only application deployment authority (Ansible
provisions it in the `server-infra` repository). Dokploy reads
`infrastructure/docker-compose.prod.yml` straight from this repository, supplies
the application environment, and pulls the service images from GHCR by
immutable commit-SHA tag. Health checks, Swarm updates, and rollback live in
Dokploy's application configuration, not in this repository. Production SigNoz
is reconciled separately by `server-infra`.

```text
Git repo
   |
   v
Luna CI (build, unit + integration tests, frontend checks, Compose smoke check)
   |
   v
Docker Publish -> GHCR (luna-frontend, luna-identity, ... tagged by commit SHA)
   |
   v
Dokploy compose project
   |
   +-- reads infrastructure/docker-compose.prod.yml from this repository
   +-- injects the application environment (secrets, LUNA_IMAGE_TAG, OTLP endpoints)
   +-- pulls the images by commit-SHA tag
   +-- runs the Compose health checks and owns deployment history and rollback
```

To ship a new release:

1. Merge to `main` and let Luna CI pass; the Docker Publish workflow pushes every image tagged with that commit SHA.
2. Set `LUNA_IMAGE_TAG` to the new SHA in the Dokploy application environment.
3. Deploy from Dokploy (or let the configured webhook trigger it) and confirm the frontend health check passes.

The application is published on host port `3001`. Services export OTLP to the
shared SigNoz collector on the host (`4317` gRPC, `4318` HTTP), and the SigNoz
UI is served from the infrastructure host on `8080`. Dokploy retains host ports
`80`, `443`, and `3000`.

To inspect a deployment, use Dokploy's deployment view for status, history, and
rollback; `docker compose ps` inside the project directory on the server shows
the same container state.
