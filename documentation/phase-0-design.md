Absolutely. Your current version is solid, but it's become more of a **technical specification** than a Phase 0 summary. I'd keep the important decisions while removing repetition and implementation-level detail.

Here is how I'd rewrite it as the **canonical Phase 0 document**. It keeps the architecture, tooling, security, testing, observability, and the things we're deliberately postponing.

---

# Phase 0 — Architecture & Foundation

## Goal

Phase 0 establishes the **architecture, service boundaries, development environment, and engineering conventions** that the rest of Luna will build upon.

No complex business workflow is implemented yet.

The goal is to reach a point where Phase 1 can begin implementing commerce functionality **without changing the fundamental architecture**.

---

## Contents

1. [Service Boundaries](#1-service-boundaries)
2. [Architecture](#2-architecture)
3. [Database Architecture](#3-database-architecture)
4. [Repository Structure](#4-repository-structure)
5. [API Architecture](#5-api-architecture)
6. [Internal Service Architecture](#6-internal-service-architecture)
7. [Validation & Mapping](#7-validation--mapping)
8. [Exception Handling & API Errors](#8-exception-handling--api-errors)
9. [Logging](#9-logging)
10. [Correlation & Request Tracking](#10-correlation--request-tracking)
11. [Configuration & Secrets](#11-configuration--secrets)
12. [Authentication & Security](#12-authentication--security)
13. [Time & Identifiers](#13-time--identifiers)
14. [Health Checks](#14-health-checks)
15. [Observability](#15-observability)
16. [Testing](#16-testing)
17. [Local Infrastructure](#17-local-infrastructure)
18. [Technology Baseline](#18-technology-baseline)
19. [Intentionally Deferred](#19-intentionally-deferred)
20. [Phase 0 Completion Criteria](#20-phase-0-completion-criteria)

---

# 1. Service Boundaries

Luna starts with six independent backend services:

| Service       | Responsibility                                         |
| ------------- | ------------------------------------------------------ |
| **Identity**  | Users, authentication, roles, authorization            |
| **Catalog**   | Products, descriptions, prices, availability           |
| **Orders**    | Carts, orders, checkout orchestration, order lifecycle |
| **Payments**  | Payment state, attempts, payment provider integration  |
| **Inventory** | Warehouses, stock, reservations                        |
| **Shipping**  | Shipping methods, quotes, shipments, tracking          |

**Fulfillment** is initially a module inside Orders and is planned to become its own service in **Phase 7**.

### Core rule

Every service owns its:

* Domain
* Database
* Migrations
* Business rules
* API

Services **never access another service's database or internal assemblies**.

Cross-service communication happens through APIs and, later, asynchronous messages.

---

# 2. Architecture

```text
                    ┌─────────────────────┐
                    │ React / Next.js UI   │
                    └──────────┬──────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
      Identity              Catalog              Orders
                                                + Fulfillment
                                                  │
                         ┌────────────┬────────────┼────────────┐
                         │            │            │            │
                      Catalog      Inventory     Payments     Shipping
```

Phase 0–1 uses **synchronous HTTP/REST**.

RabbitMQ is introduced in **Phase 2** for asynchronous workflows.

Messaging complements APIs; it does not replace them.

---

# 3. Database Architecture

Each service owns an independent database:

```text
SQL Server
├── IdentityDb
├── CatalogDb
├── OrdersDb
├── PaymentsDb
├── InventoryDb
└── ShippingDb
```

During local development these databases may share one SQL Server instance.

The physical hosting arrangement does **not** change the ownership boundaries.

### Rules

* Each service manages its own EF Core migrations.
* No cross-database foreign keys.
* No direct queries against another service's database.
* Cross-service references use opaque UUIDs.
* Services exchange data through APIs rather than shared EF entities.

---

# 4. Repository Structure

```text
Luna/
├── src/
│   ├── Services/
│   │   ├── Identity/
│   │   ├── Catalog/
│   │   ├── Orders/
│   │   ├── Payments/
│   │   ├── Inventory/
│   │   └── Shipping/
│   └── Frontend/
│
├── tests/
│   ├── Identity/
│   │   ├── Unit/
│   │   └── Integration/
│   ├── Catalog/
│   ├── Orders/
│   ├── Payments/
│   ├── Inventory/
│   ├── Shipping/
│   └── EndToEnd/
│
├── infrastructure/
│   └── docker-compose.yml
│
├── documentation/
├── AGENTS.md
├── README.md
└── Luna.sln
```

Each backend service is an independent .NET application.

Services **do not have to follow a fixed number of projects**.

Layers such as `Application`, `Domain`, and `Infrastructure` are introduced when the service's complexity justifies them.

---

# 5. API Architecture

All services expose **ASP.NET Core REST APIs**.

### API standards

* Resource-oriented REST
* Standard HTTP status codes
* API versioning beginning at `v1`
* OpenAPI specification for every service
* Swagger UI for exploration/testing
* Documented request/response contracts
* Typed frontend API clients
* Consistent identifiers
* Consistent error responses

Breaking API changes require a new version.

Services depend on **API contracts**, never on another service's implementation.

### API design decisions

* API style: ASP.NET Core Controllers.
* Controllers define the HTTP boundary and delegate business operations to application commands and queries.
* Controllers must not contain domain or business logic.
* REST endpoints use resource-oriented routes and standard HTTP status codes.

### Service APIs

| API           | Responsibility               |
| ------------- | ---------------------------- |
| Identity API  | Authentication, users, roles |
| Catalog API   | Products, search, filtering  |
| Orders API    | Cart, checkout, orders       |
| Payments API  | Payment authorization/state  |
| Inventory API | Stock and reservations       |
| Shipping API  | Quotes, shipping, tracking   |

---

# 6. Internal Service Architecture

Where appropriate:

```text
HTTP Request
     |
     v
Controller
     |
     v
Request Validation (FluentValidation)
     |
     v
Command or Query Handler
     |
     v
Domain Model / Application Services
     |
     v
Infrastructure / EF Core
     |
     v
Database
```

### CQRS

Luna uses **conceptual CQRS** from Phase 0:

* Commands change state.
* Queries retrieve state.
* Read/write responsibilities remain separate.

No CQRS framework is required.

### MediatR

**Not used initially.**

MediatR may be introduced later if the application develops a genuine need for mediator-based decoupling.

The goal is to learn CQRS without creating unnecessary indirection or files.

---

# 7. Validation & Mapping

### Validation

Two levels of validation:

**Request validation**

Handled by FluentValidation:

```text
quantity > 0
email has valid format
required fields exist
```

**Business validation**

Handled by the owning domain/application:

```text
Cannot reserve 10 units when only 4 exist.
Cannot cancel an already shipped order.
Cannot authorize payment for an invalid order.
```

### Mapping

DTOs are mapped **manually**.

No AutoMapper initially.

The goal is to keep service contracts explicit.

---

# 8. Exception Handling & API Errors

All services use centralized exception handling middleware.

Expected validation/business failures return structured errors.

Unexpected exceptions:

* Are logged.
* Include the correlation ID.
* Return a generic `500`.
* Never expose stack traces, database errors, or internal implementation details.

Example:

```json
{
  "code": "PRODUCT_NOT_FOUND",
  "message": "The requested product was not found.",
  "correlationId": "..."
}
```

Standard status codes are used consistently:

| Status | Meaning            |
| ------ | ------------------ |
| `400`  | Invalid request    |
| `401`  | Unauthenticated    |
| `403`  | Unauthorized       |
| `404`  | Resource not found |
| `409`  | Business conflict  |
| `500`  | Unexpected failure |

---

# 9. Logging

All services use **Serilog structured logging**.

Logs should provide enough context to understand what happened across the system.

Relevant fields include:

* Timestamp
* Log level
* Service
* Environment
* Correlation ID
* Request ID
* Operation
* Entity IDs
* Exception details

### Log levels

```text
Trace       Extremely detailed diagnostics
Debug       Development diagnostics
Information Normal application events
Warning     Recoverable/unexpected situations
Error       Failed operations
Critical    Service-level failures
```

Log **meaningful business events**, not every method call.

Examples:

```text
Order checkout started
Inventory reservation failed
Payment authorization succeeded
Shipment created
```

Never log:

* Passwords
* Tokens
* Payment credentials
* Secrets

---

# 10. Correlation & Request Tracking

Every incoming request receives or propagates a **correlation ID**.

The ID is:

* Included in logs.
* Returned in API errors.
* Propagated between synchronous service calls.

This establishes the foundation for distributed tracing later.

---

# 11. Configuration & Secrets

Configuration uses:

* ASP.NET Core configuration
* Environment-specific settings
* Environment variables
* Options pattern

Configuration includes:

* Database connections
* Service URLs
* Authentication settings
* Logging
* Provider configuration

Secrets are never committed to source control.

Local secrets may use excluded development configuration or `.env` files.

Production secrets will be supplied by the deployment environment in later phases.

---

# 12. Authentication & Security

Identity uses:

* **ASP.NET Core Identity** for user management
* **OpenIddict** for OAuth 2.0 / OpenID Connect capabilities
* ASP.NET Core authentication/authorization middleware in the other services

Phase 0 authentication flow:

```text
Register
   ↓
Login
   ↓
Access Token
   ↓
Authenticated Request
   ↓
Role / Policy Authorization
```

Identity issues tokens.

Other services validate those tokens and enforce authorization.

### Security rules

* Passwords are never stored in plaintext.
* Authentication is centralized in Identity.
* Authorization uses roles/policies.
* Tokens and secrets are never logged.
* A client-supplied user ID is never considered proof of identity.

Deferred to the dedicated security phase:

* MFA
* Password reset
* Email verification
* Refresh-token rotation
* Security auditing
* Service-to-service authentication
* Advanced security hardening

---

# 13. Time & Identifiers

### Time

* Store timestamps in UTC.
* Exchange timestamps as ISO 8601 UTC.
* Convert to local time only in the presentation layer.
* Domain logic must not depend on server local time.

### Identifiers

* Services generate and own their IDs.
* Use UUIDs for distributed identifiers.
* IDs are opaque and contain no business meaning.

---

# 14. Health Checks

Every service exposes a basic health endpoint.

Phase 0 checks that the application is running.

Later phases expand this into readiness checks for:

* Databases
* RabbitMQ
* Other infrastructure dependencies

---

# 15. Observability

Phase 0 establishes the **foundation**:

* Structured logging
* Service identification
* Correlation IDs
* Appropriate request/response logging
* Health checks
* Consistent error logging

Full observability is deferred to Phase 8:

* OpenTelemetry
* Distributed tracing
* Metrics
* Log aggregation
* Dashboards
* Cross-service visualization

---

# 16. Testing

Testing follows service ownership.

### Unit tests

Test:

* Domain behavior
* Application logic
* Business rules

### Integration tests

Test:

* API + database
* EF Core behavior
* Migrations
* Service boundaries

Use **Testcontainers** with real SQL Server containers.

### End-to-end tests

Test complete cross-service workflows.

Cross-service behavior belongs in contract/E2E testing rather than unit tests.

---

# 17. Local Infrastructure

Phase 0 provides a repeatable local environment using:

* .NET
* Next.js / React
* SQL Server
* Docker
* Docker Compose

The environment supports:

* Starting the system locally
* Independent service configuration
* Independent database migrations
* Reproducible development environments

RabbitMQ is intentionally **not included yet**.

---

# 18. Technology Baseline

| Area          | Technology                           | Notes                                                                  |
| ------------- | ------------------------------------ | ---------------------------------------------------------------------- |
| Backend       | .NET 8 / ASP.NET Core                |                                                                        |
| API           | REST / OpenAPI                       |                                                                        |
| API style     | ASP.NET Core Controllers             | Conventional, structured REST API boundary suitable for Luna's multiple services. |
| Identity      | ASP.NET Core Identity + OpenIddict   |                                                                        |
| ORM           | EF Core                              |                                                                        |
| Database      | SQL Server                           |                                                                        |
| Architecture  | Conceptual CQRS                      |                                                                        |
| Mediator      | None initially                       |                                                                        |
| Validation    | FluentValidation + domain validation |                                                                        |
| Mapping       | Manual                               |                                                                        |
| Logging       | Serilog                              |                                                                        |
| Testing       | xUnit + FluentAssertions             |                                                                        |
| Mocking       | Moq                                  |                                                                        |
| Integration   | Testcontainers                       |                                                                        |
| Frontend      | Next.js + React + TypeScript         |                                                                        |
| API Client    | Typed HTTP clients                   |                                                                        |
| Server state  | TanStack Query                      | Handles API data fetching, caching, mutations, loading, errors, and synchronization. |
| HTTP client   | Axios                               | Provides the initial HTTP transport beneath centralized typed API clients. |
| Configuration | ASP.NET Core Options                 |                                                                        |
| Health        | ASP.NET Core Health Checks           |                                                                        |
| Containers    | Docker + Docker Compose              |                                                                        |

---

# 19. Intentionally Deferred

Phase 0 **does not solve distributed reliability or complex business workflows**.

Deferred to later phases:

* Complete checkout workflow
* RabbitMQ
* Asynchronous consumers
* Transactional Outbox
* Retries
* Timeouts/resilience policies
* Idempotency
* Dead-letter queues
* Simulated processing delays
* Failure injection
* OpenTelemetry
* Metrics
* Operations dashboard
* Service redundancy
* Chaos testing
* Production-like deployment
* Advanced authentication/security
* Fulfillment service extraction

---

# 20. Phase 0 Completion Criteria

Phase 0 is complete when:

* [ ] All six service boundaries exist.
* [ ] Frontend project exists.
* [ ] Each service has independent persistence ownership.
* [ ] SQL Server databases can be created and migrated independently.
* [ ] Docker Compose starts the local environment.
* [ ] Every service exposes a health endpoint.
* [ ] REST/OpenAPI conventions are established.
* [ ] Centralized exception handling exists.
* [ ] Structured logging and correlation IDs work.
* [ ] Configuration/secrets conventions are established.
* [ ] Identity supports registration, login, tokens, and roles.
* [ ] Unit and integration test foundations exist.
* [ ] Service boundaries prevent direct database/internal assembly dependencies.
* [ ] Phase 1 can begin without redesigning the architecture.

---

## Confirmed Architectural Decisions

1. **Six services:** Identity, Catalog, Orders, Payments, Inventory, Shipping.
2. **Fulfillment remains inside Orders until Phase 7.**
3. **One database per service.**
4. **No cross-service database access.**
5. **REST/HTTP for Phase 0–1.**
6. **RabbitMQ begins in Phase 2.**
7. **OpenAPI defines service contracts.**
8. **Conceptual CQRS without MediatR initially.**
9. **FluentValidation for request validation; domain/application logic for business rules.**
10. **Manual DTO mapping; no AutoMapper.**
11. **Centralized exception handling and consistent API errors.**
12. **Serilog structured logging + correlation IDs from Phase 0.**
13. **ASP.NET Core Identity + OpenIddict for authentication.**
14. **Role/policy-based authorization.**
15. **UTC timestamps and opaque UUIDs.**
16. **Testcontainers for database integration testing.**
17. **Docker Compose for the local environment.**
18. **Advanced reliability, observability, security, and redundancy are deliberately learned in later phases.**

### The important part

I would use **this shorter version as the actual Phase 0 document** and keep the more detailed diagrams/technical explanations elsewhere if you need them.

The key philosophy of Luna is now very clear:

> **Don't add technology because a production system might use it. Add it when a problem in the simulation gives you a reason to learn it.**

That will make the progression from **simple .NET services → synchronous distributed system → RabbitMQ → reliability → observability → redundancy → production-like deployment** much more educational than starting with every "microservices best practice" at once.
