# Getting Started

Everything needed to run Luna locally. The [README](../README.md) covers what the
project is; this file covers the mechanics.

## Prerequisites

- Docker Desktop
- Node.js 20+ (only for running the frontend outside Docker, and for the demo scripts)

## 1. Environment files

Both files are needed before the first start.

**`infrastructure/.env`** — copy from `.env.example` and set:

| Variable | Notes |
| --- | --- |
| `MSSQL_SA_PASSWORD` | SQL Server `sa` password. Must satisfy the SQL Server complexity rules. |
| `ORDERS_SERVICE_CLIENT_SECRET` | Client secret the frontend uses to obtain a token from Identity. |
| `LUNA_COOKIE_ENCRYPTION_KEY` | Base64-encoded 32-byte key. Must be identical in both env files. |

### Identity signing keys

The local stack runs Identity in Development, which generates an ephemeral
signing certificate at startup. Nothing is needed locally beyond the variables
above.

**Production does require a key.** Outside Development the authorization server
refuses to start unless `OpenIddict:Keys:SigningKeyPath` points at a readable
certificate, because the development certificate is not persisted: every restart
would otherwise generate a different key and invalidate every issued token and
every other service's cached JWKS.

Generate a pair and store them outside the repository:

```bash
mkdir -p infrastructure/secrets/identity
openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
  -keyout infrastructure/secrets/identity/signing.key \
  -out infrastructure/secrets/identity/signing.crt
openssl pkcs12 -export -out infrastructure/secrets/identity/signing.pfx \
  -inkey infrastructure/secrets/identity/signing.key \
  -in infrastructure/secrets/identity/signing.crt -passout pass:"$PASSWORD"
```

Then set in `.env`:

| Variable | Notes |
| --- | --- |
| `IDENTITY_SIGNING_KEY_PATH` | Defaults to `/run/secrets/identity/signing.pfx`. |
| `IDENTITY_SIGNING_KEY_PASSWORD` | Passphrase for the PKCS#12 file. Required. |
| `IDENTITY_KEY_DIRECTORY` | Host directory mounted read-only at `/run/secrets/identity`. Defaults to `./secrets/identity`. |
| `IDENTITY_ENCRYPTION_KEY_PATH` | Optional. Used for the encrypted JWKS document. |
| `IDENTITY_ENCRYPTION_KEY_PASSWORD` | Optional passphrase for the encryption key. |

PKCS#12 (`.pfx`) and PEM are both accepted. PEM files are detected by content
rather than by extension, so a `.pem` file holding a PKCS#12 bundle still loads.

`infrastructure/secrets/` must be gitignored. Confirm it is before committing
anything in that directory.

**`src/Frontend/.env`** — set the local service URLs to `http://localhost:5001`
through `http://localhost:5006`, and the same `LUNA_COOKIE_ENCRYPTION_KEY`.

Keep the Docker service URLs (`http://identity:8080`, `http://catalog:8080`, and
so on) for container-to-container traffic. Use `localhost` URLs only when
running Next.js directly on the host.

## 2. Start the stack

From the repository root:

```bash
docker compose -f infrastructure/docker-compose.yml up --build
```

That is the whole local stack: SQL Server, the six services, the frontend
gateway, and the AWS emulator. Each service runs its own EF Core migrations on
startup, so a fresh volume needs no separate migration step.

The storefront is then at:

```text
http://localhost:3000
```

and the unified API explorer, which fronts every service behind the gateway, at:

```text
http://localhost:3000/swagger
```

Backend containers are not published individually in this configuration. The
frontend is the public HTTP boundary; the browser never addresses a Docker
service name or a private service URL.

## 3. Frontend development

The `up --build` above rebuilds the frontend image on every change, which is
slow. For iterating on the frontend, start the backends with the development
override and run Next.js on the host instead:

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.dev.yml \
  up --build
```

```bash
cd src/Frontend
npm install
npm run dev
```

The development override publishes the backend services on ports `5001`–`5006`,
which is what the frontend's local `.env` rewrites point at. Add
`--force-recreate` if the services were previously started without the override,
because Compose will not rebuild an existing container just because an override
appeared.

Restart `npm run dev` after changing `src/Frontend/.env`. Next.js reads the
rewrite destinations at startup and does not pick up changes to them at runtime.

## 4. Observability

```bash
docker compose \
  -f infrastructure/docker-compose.yml \
  -f infrastructure/docker-compose.observability.yml \
  up --build signoz signoz-migrator otel-collector
```

SigNoz is at <http://localhost:8080>, with OTLP receivers on `4317` (gRPC) and
`4318` (HTTP). All services emit OpenTelemetry traces; see
[observability.md](observability.md) for how to follow a single checkout through
the system.

## 5. Messaging resources

Phase 2 declares its AWS resources — the EventBridge bus, two SQS queues, their
dead-letter queues, two rules and an SES identity — in Terraform rather than in
a seed script, so the same files apply to the local emulator, a self-hosted
server, and real AWS later.

```bash
cd infrastructure/terraform
terraform init
terraform plan
terraform apply
```

Against the local emulator the endpoint defaults to `http://localhost:4566`.
For the Compose network it is `-var endpoint=http://floci:4566`. Leaving it
empty targets real AWS. See the [Phase 2 architecture](dev_phases/phase-2/architecture.md)
for the resource mapping and the decision record.

## 6. Tests

```bash
# backend, from the repository root
dotnet test

# frontend
cd src/Frontend
npm test
```

## 7. Regenerating the demo media

`documentation/luna-demo.gif` and `documentation/images/*.png` are committed
rather than built in CI, because they are documentation and rebuilding them on
every push would put a binary diff in every commit.

They are produced by driving the **running** stack, so start it first. The script
registers a throwaway customer, grants itself the `Admin` role in the local
database so it can reach the operations console, places a real order and walks
it through fulfilment — an empty cart, an empty queue and a zero shipment count
are all truthful renderings of an unused system, and none of them makes a
useful demo.

```bash
cd src/Frontend
npm run demo:build    # documentation/luna-demo.gif
npm run demo:stills   # documentation/images/*.png
```

Both read the SQL Server `sa` password from the gitignored
`infrastructure/.env`, so no credential is stored in the repository. They expect
a freshly seeded stack: on a database that already holds orders, the
fulfillment queue shows all of them rather than the single demo order.

## Troubleshooting

**A service reports healthy but every request returns an error.** The container
healthcheck only verifies the process is listening; it does not touch the
database. Check `docker compose logs <service>` for SQL error 4060, which means
the connection could not open the database at all.

**The frontend cannot reach a backend.** Confirm the dev override is in use so
the services are published on `5001`–`5006`, and confirm `src/Frontend/.env`
points at those ports rather than the container names.

**Changing `src/Frontend/.env` appears to do nothing.** Restart `npm run dev`.
