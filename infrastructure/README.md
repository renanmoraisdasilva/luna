# Infrastructure

This directory contains Luna's container definitions, local Compose stacks, and observability tooling.

## Compose Files

- `docker-compose.yml` provides the base local stack.
- `docker-compose.dev.yml` exposes backend service ports for local development.
- `docker-compose.prod.yml` is the production Compose definition. Dokploy reads it directly from this repository; server-only values are supplied through the Dokploy application environment.
- `docker-compose.observability.yml` is a local-development compatibility file. Production SigNoz is owned by the separate `server-infra` repository.
- `terraform/` declares the emulated AWS resources. See [Terraform](#terraform).

## Local AWS Emulator

Phase 2 messaging runs on [Floci](https://github.com/floci-io/floci), a free MIT-licensed local AWS emulator. It serves EventBridge, SQS, and SES on port `4566`, needs no account or auth token, and is a drop-in replacement for other LocalStack-compatible emulators.

- `floci` in `docker-compose.yml` uses the pinned `-compat` image, which ships the AWS CLI for debugging from inside the container. It needs no service selection, has no secret, and is probed on `/_floci/health`.
- `floci-ui` in `docker-compose.dev.yml` only, on port `4500`. It is a local browser for SQS queues, EventBridge rules, and captured SES mail. It is never deployed to the server.

Open [http://localhost:4500](http://localhost:4500) to browse queues and rules, or [http://localhost:4566/_aws/ses](http://localhost:4566/_aws/ses) to read captured emails directly. The UI is a viewer: it lists EventBridge buses and the SES mailbox but cannot create rules.

Emulator state is in-memory, so a recreated container starts empty. Apply the Terraform configuration to repopulate it.

## Terraform

`terraform/` declares the messaging resources that live inside the emulator: the event bus, the Notification queues and their DLQs, the routing rules, and the sender identity. It uses the standard `hashicorp/aws` provider, so the same files apply against the deployed stack and against real AWS in Phase 15. Terraform is a tool you run, not a service: it is not a container, has no health check, and never appears in Compose.

```bash
cd infrastructure/terraform
terraform init
terraform plan
terraform apply
```

Start the emulator first, because `plan` and `apply` call the endpoint. `plan` on an already-correct stack reports no changes; `terraform destroy` removes everything. Because emulator state is in-memory while the Terraform state file is on disk, recreating the emulator and then running `terraform apply` repairs any drift.

### Where Terraform runs

Terraform is a tool, not a service. It runs, does its work, and exits, so it is not a Compose service and has no health check. It is invoked in three places, and the split between them is the important part:

- **Locally, by a person.** `plan` before `apply`, every time. Reading the plan is the point, so this step is not automated.
- **In CI, as validation.** `.github/workflows/terraform.yml` runs `fmt -check`, `validate`, and `plan` against a throwaway emulator on every change that touches `infrastructure/terraform/`. It changes nothing and uploads the plan as an artifact.
- **At deploy time, as a gated step.** Applying is never automatic. A human triggers it, and on real AWS it would sit behind an environment approval rule. On the server this is part of the Phase 2 deployment, where the same files run with `-var endpoint=http://floci:4566` because the emulator is reached by container name rather than `localhost`.

The reason `plan` and `apply` are separate workflows is that they have different consequences. `plan` only reads. `apply` changes infrastructure, and on a real account a wrong apply can destroy production resources. Keeping them apart means ordinary commits can never cause that.

The local state file is not a production practice. Real deployments point the backend at S3 with locking, which is what Phase 15 sets up. Locally the file sits on disk and is never committed, which is fine for a single developer.

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

The local stack also starts the AWS emulator and its UI. Start or stop just the emulator when you want it without Luna's services:

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.dev.yml \
  up -d floci floci-ui
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
