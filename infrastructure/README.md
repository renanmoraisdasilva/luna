# Infrastructure

This directory contains Luna's container definitions and deployment scripts.

## Compose Files

- `docker-compose.yml` provides the base local stack.
- `docker-compose.dev.yml` exposes backend service ports for local development.
- `docker-compose.prod.yml` is the tracked production Compose definition. The server receives this file from the published frontend image; server-only values stay in `/opt/luna/.env`.

Important: the production server must have a real `/opt/luna/.env` file before running the updater. The updater sources this file and Compose uses it for `${...}` substitutions. If it is missing or malformed, the backend services will start with empty database connection strings and fail.

A sample file is provided at `.env.example` for reference. Copy it to `/opt/luna/.env` on the server and replace the example values with the real database connection strings.

Run the local stack:

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.dev.yml \
  up --build
```

## Deployment Synchronization

The frontend image packages the production deployment files under `/opt/luna-deployment/`. The server-side updater pulls that image first, extracts its deployment files, replaces the local Compose definition and updater, then pulls and starts all service images.

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
 extract deployment files
          |
          v
 replace local Compose/update.sh
          |
          v
 pull all service images
          |
          v
 docker compose up --wait
```

`update.sh` is the versioned source for the updater. On the server, it runs from `/opt/luna`, preserves the server-only `.env`, and uses `docker-compose.prod.yml` to pull and reconcile the stack.
