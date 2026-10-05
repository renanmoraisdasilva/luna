# Luna Project Guide

## Documentation

- `documentation/roadmap.md` is the high-level project roadmap, phase status, and next priorities. It is the **only** place phase status is recorded; the README lists phase focus without status so the two cannot disagree.
- `documentation/architecture.md` describes how the system is put together **today**, and is verified against the code rather than derived from a phase. When the architecture changes, this is the document to update. It records what it was checked against in its last section, so a stale copy is detectable.
- `documentation/codebase-tour.md` is a reading guide: where each layer lives, what a request does end to end, which patterns are in use, and the places the code will surprise you. It answers "where does this live" while `architecture.md` answers "why is it like this"; the tour links rather than restates. Read it before hunting through projects by guesswork.
- `documentation/dev_phases/` contains one folder per development phase (`phase-0/`, `phase-1/`, `phase-2/`, ...) holding that phase's design, specification, architecture, and acceptance-criteria documents, such as `phase-1/spec.md`, `phase-1/architecture.md`, and `phase-2/architecture.md`. These are **historical records** of why each phase was designed as it was, not descriptions of the current system; each carries a header pointing at `documentation/architecture.md`.
- `documentation/getting-started.md` is the local run guide: environment files, Compose, frontend development, observability, Terraform, tests, and regenerating the demo media. The root `README.md` is the front door and links here rather than repeating setup steps.
- `documentation/lessons-learned.md` records durable engineering findings: defects found and fixed, claims that were measured and retracted, and the reasoning behind policy decisions such as the dependency audit gate and the Dependabot major-version policy. It describes neither the current system nor phase status, so it cannot go stale against the code. Read it before repeating an investigation that is already written up here.
- `documentation/luna-demo.gif` and `documentation/images/*.webp` are committed documentation artifacts, not build output. They are produced by `src/Frontend/scripts/build-demo-gif.mjs` (`npm run demo:build` and `npm run demo:stills` from `src/Frontend`), which drives the running stack and writes the stills as WebP. Regenerate them when a screen or the demo data changes; do not hand-edit them.
- `documentation/dev_phases/` contains one folder per development phase (`phase-0/`, `phase-1/`, `phase-2/`, ...) holding that phase's design, specification, architecture, and acceptance-criteria documents, such as `phase-1/spec.md`, `phase-1/architecture.md`, and `phase-2/architecture.md`.
- Phase specifications describe what the system must do: business behavior, API capabilities, failure scenarios, and acceptance criteria.
- Phase architecture documents describe how the system is built: service boundaries, database ownership, backend layering, frontend architecture, authentication, persistence, testing, and architectural decisions.
- `documentation/ui_renderings/` contains the HTML UI references for the planned customer experience. Use these when implementing or reviewing frontend screens:
  - `catalog/shop.html` - storefront and catalog browsing
  - `catalog/product-details.html` - product details
  - `cart/cart.html` - cart
  - `checkout/checkout.html` - checkout
  - `checkout/order-confirmation.html` - order confirmation
  - `orders/order-list.html` - order history
  - `orders/order-details.html` - order details and customer tracking
  - `account/account.html` - account
  - `account/login.html` and `account/register.html` - authentication
  - `luna_ops/fullfilment.html` and `luna_ops/fullfilment_details.html` - fulfillment operations
  - `luna_ops/shipments.html` and `luna_ops/shipment_details.html` - shipment operations

## Repository areas

- `src/Services/` contains the backend services and their bounded contexts.
- `src/Frontend/` contains the Next.js customer application and gateway.
- `tests/Unit/`, `tests/Integration/`, and `tests/Smoke/` contain the centralized test suites.
- `infrastructure/` contains Docker Compose files, Dockerfiles, environment examples, and observability configuration.
- `.github/workflows/` contains CI validation and Docker image publishing workflows.
- `scripts/` contains project helper scripts.
- `src/Frontend/scripts/` contains frontend tooling, including the demo media generator. It is the only place Node scripts live; `scripts/` is for the .NET and shell helpers.

## Working with the documentation

Use the roadmap to understand what should be worked on next. Use the relevant phase specification to verify required behavior, and use the corresponding architecture document to verify that an implementation follows Luna's technical boundaries. UI renderings describe visual intent; the current frontend code and phase documents define the implemented behavior.

## Engineering Standards

Follow the roadmap and the relevant phase specification and architecture documents before changing behavior. When implementation and documentation disagree, identify the discrepancy and update the appropriate documentation rather than silently creating a new convention.

### Luna Structure Rules

1. Every business service uses `{Service}.Api`, `{Service}.Application`, `{Service}.Contracts`, `{Service}.Domain`, and `{Service}.Infrastructure` projects where the service boundary requires them.
2. Application code is organized by feature or capability.
3. Domain projects contain business entities and domain rules.
4. Contracts projects contain external and API contracts owned by that service.
5. `Luna.Contracts` contains only genuinely cross-service contracts.
6. Infrastructure contains persistence and external integration implementations.
7. Repository interfaces belong to the Application abstraction; repository implementations belong to Infrastructure.
8. Repositories do not orchestrate business workflows.
9. Application handlers own use-case orchestration.
10. Shared code requires explicit justification and should not be introduced merely to avoid local duplication.

**Exception:** Identity currently uses one `Identity.csproj` with Application, Controllers, Domain, Infrastructure, and Migrations folders. This is intentional because the OpenIddict identity host predates the five-project service template. Its internal folders still preserve the same responsibility boundaries; new services should follow the standard project layout.

### Frontend

- Use React Hook Form for interactive forms instead of manually duplicating field state and submit handling with `useState`.
- Use Zod schemas with `@hookform/resolvers` for client-side form validation. Keep validation messages close to the schema and use typed `z.infer` form values.
- Use TanStack Query for server state, caching, loading, mutation, and invalidation behavior. Do not treat server data as ordinary component-local state.
- Use Axios through centralized, typed API clients. Keep HTTP calls and response handling out of presentational components.
- Follow the Next.js server-first boundary: use Server Components where practical, and use Client Components for interactive workflows.
- Keep URL parameters as the source of truth for catalog filters and pagination.
- Keep frontend tests focused on user-visible behavior and important loading, validation, error, and mutation states.
- For every frontend API client and interactive workflow, cover the successful request, the relevant unauthorized or failed response, and the resulting user-visible error or empty state.
- Cover cart and account workflows for loading, validation, mutation success, mutation failure, retry or refresh behavior, and authorization boundaries.

### Backend

- Use Clean Architecture and Domain-Driven Design within each bounded context. Keep presentation, application, domain, and infrastructure responsibilities explicit.
- Keep controllers thin. Application code coordinates use cases, domain code owns business behavior and invariants, and infrastructure owns persistence and external integrations.
- Preserve bounded-context ownership: each service owns its domain and database, and services must not access one another's databases directly.
- Maintain the CQRS-style separation between read and write paths. Read repositories may project directly to application DTOs/read models; writes should use domain entities when behavior or invariants are required.
- Use the phase documents as the source of truth for contracts, failure behavior, architecture, and acceptance criteria. Record deliberate deviations in the relevant documentation.
- Backend tests should cover domain invariants, application error paths, controller validation and authorization, persistence behavior, concurrency-sensitive operations, and the response contract for expected failures.
- Integration tests should exercise each public endpoint through the real host and database boundary, including unauthenticated requests, invalid input, missing resources, and successful persistence.
