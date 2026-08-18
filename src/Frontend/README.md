# Luna Frontend

The Luna frontend is the customer-facing web application for the commerce and logistics simulation. It is intentionally small during Phase 0: the project establishes the UI foundation, frontend tooling, service configuration, and unified API documentation before commerce workflows are implemented.

## Responsibilities

The frontend will eventually provide:

- Product browsing, searching, and filtering
- Cart management
- Checkout
- Order history and order details
- Order progress and delivery tracking
- Customer authentication
- Access to the operations experience in later phases

The current scaffold provides:

- A blank application entry point reserved for the product design
- A unified Swagger API explorer at `/swagger`
- Proxy rewrites for the six backend OpenAPI documents
- A thin same-origin gateway for service API calls
- A production-ready standalone Docker build

## Technology

| Area | Technology | Purpose |
| --- | --- | --- |
| Framework | Next.js 16 | React framework and application runtime |
| UI | React 18 | Component-based user interface |
| Language | TypeScript | Static typing and safer frontend contracts |
| Server state | TanStack Query | Planned API fetching, caching, mutations, and synchronization |
| HTTP client | Axios | Planned centralized HTTP transport |
| API documentation | Swagger UI React | Unified browsing of service OpenAPI documents |
| Runtime | Node.js 22 | Local and container runtime |
| Container | Docker, `node:22-alpine` | Reproducible production image |

## Project Structure

```text
src/Frontend/
├── app/
│   ├── globals.css                 Swagger page styles and global reset
│   ├── layout.tsx                  Root layout and metadata
│   ├── page.tsx                    Blank application entry point
│   └── swagger/
│       ├── page.tsx                API explorer route
│       └── SwaggerExplorer.tsx     Client-side service selector and Swagger UI
├── types/
│   └── swagger-ui-react.d.ts       Local TypeScript declaration for the package
├── .env.example                    Browser-facing API URL examples
├── next.config.mjs                 Standalone output and Swagger proxy rewrites
├── next-env.d.ts                   Next.js generated type references
├── package.json                    Scripts and dependencies
├── package-lock.json               Locked npm dependency versions
└── tsconfig.json                   TypeScript compiler configuration
```

## Local Development

From this directory:

```bash
npm install
npm run dev
```

The development server runs at [http://localhost:3000](http://localhost:3000).

For host-side Next.js development, start the backend services with the development Compose override:

```bash
docker compose \
	-f ../../infrastructure/docker-compose.yml \
	-f ../../infrastructure/docker-compose.dev.yml \
	up -d sqlserver identity catalog orders payments inventory shipping
```

The override publishes the backend ports only for local development. The default Compose file does not publish them.

The local rewrite destinations then use:

```text
Identity   http://localhost:5001
Catalog    http://localhost:5002
Orders     http://localhost:5003
Payments   http://localhost:5004
Inventory  http://localhost:5005
Shipping   http://localhost:5006
```

Start the complete stack, including the frontend gateway, from the repository root with:

```bash
docker compose -f infrastructure/docker-compose.yml up --build
```

## Docker Development

The repository-level Compose file runs the frontend together with SQL Server and all backend services:

```bash
docker compose -f infrastructure/docker-compose.yml up --build
```

The frontend is available at [http://localhost:3000](http://localhost:3000).

The image uses a three-stage build:

1. `dependencies` installs the locked npm dependencies with `npm ci`.
2. `build` compiles the Next.js application and creates the standalone output.
3. `runtime` runs only the standalone Next.js server on port `3000`.

The root `.dockerignore` excludes `node_modules`, `.next`, .NET build output, and other non-runtime files from the Docker build context.

## Unified Swagger Explorer

Open:

```text
http://localhost:3000/swagger
```

The page presents one explorer with a selector for:

- Identity API
- Catalog API
- Orders API
- Payments API
- Inventory API
- Shipping API

Each backend service still owns its own OpenAPI document. Next.js rewrites the browser-facing paths below to the corresponding service:

```text
/api/swagger/identity  -> identity/swagger/v1/swagger.json
/api/swagger/catalog   -> catalog/swagger/v1/swagger.json
/api/swagger/orders    -> orders/swagger/v1/swagger.json
/api/swagger/payments  -> payments/swagger/v1/swagger.json
/api/swagger/inventory -> inventory/swagger/v1/swagger.json
/api/swagger/shipping  -> shipping/swagger/v1/swagger.json
```

In Docker, the rewrites use Compose service names such as `http://catalog:8080`. During local development, they default to the host ports `5001` through `5006`. This proxy keeps browser CORS out of the documentation workflow.

## Configuration

The frontend is the only published application port in the default Docker Compose setup. Browser code should call same-origin gateway paths rather than backend host ports:

```text
/api/services/identity/...
/api/services/catalog/...
/api/services/orders/...
/api/services/payments/...
/api/services/inventory/...
/api/services/shipping/...
```

Next.js rewrites those paths to private Docker service URLs. For example:

```text
Browser -> /api/services/catalog/api/v1/products
Next.js -> http://catalog:8080/api/v1/products
```

The services remain HTTP APIs, but they are not publicly exposed by default. They can later be deliberately exposed through an API gateway or load balancer without changing their contracts.

When authentication is introduced, Next.js will be the preferred OIDC client and server-side session boundary. The browser should use the Next.js session rather than receiving access tokens directly in browser JavaScript by default.

`.env.example` is retained for future public configuration, but no `NEXT_PUBLIC_*` API URLs are required for the current thin-gateway setup.

The internal rewrite destinations are supplied as Docker build arguments:

```text
IDENTITY_API_INTERNAL_URL
CATALOG_API_INTERNAL_URL
ORDERS_API_INTERNAL_URL
PAYMENTS_API_INTERNAL_URL
INVENTORY_API_INTERNAL_URL
SHIPPING_API_INTERNAL_URL
```

## Scripts

| Command | Description |
| --- | --- |
| `npm install` | Install dependencies locally |
| `npm run dev` | Start the Next.js development server |
| `npm run build` | Create and type-check a production build |
| `npm run start` | Run the previously built production application |
| `npm run lint` | Run the configured lint command |

## Phase 0 Boundaries

This frontend does not yet implement:

- Product or order API clients
- Typed service clients through the thin gateway
- Authentication screens or token handling
- Cart state and checkout
- TanStack Query providers
- Operations dashboards
- E2E tests with Playwright
- Production analytics or monitoring

Those pieces belong to later implementation phases. The current gateway deliberately forwards backend contracts without DTO aggregation or mapping. A real BFF should be introduced only if a demonstrated frontend or operations-console problem justifies it.
