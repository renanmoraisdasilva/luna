# Luna Roadmap

> A distributed commerce and logistics simulation built to explore what happens when real-world complexity, failure, and asynchronous behavior enter the picture.

## Contents

- [Why Build Luna?](#why-build-luna)
- [End Goal](#end-goal)
- [Customer Experience](#customer-experience)
- [Operations Console](#operations-console)
- [Architecture](#architecture)
  - [Bounded Contexts](#bounded-contexts)
  - [Communication](#communication)
  - [Reliability Patterns](#reliability-patterns)
  - [Payment Abstraction](#payment-abstraction)
  - [Shipping](#shipping)
- [Technology](#technology)
- [Development Philosophy](#development-philosophy)
- [Phase 0 Design](phase-0-design.md)
- [Phase 1 Design](phase-1-design.md)
- [Implementation Roadmap](#implementation-roadmap)
  - [Phase 0: Architecture and Foundation](#phase-0-architecture-and-foundation)
  - [Phase 1: Basic Commerce Flow](#phase-1-basic-commerce-flow)
  - [Phase 2: Messaging and Async Workflows](#phase-2-messaging-and-async-workflows)
  - [Phase 3: Realistic Time and Simulation](#phase-3-realistic-time-and-simulation)
  - [Phase 4: Reliability and Failure Handling](#phase-4-reliability-and-failure-handling)
  - [Phase 5: Transactional Outbox](#phase-5-transactional-outbox)
  - [Phase 6: Payment Abstraction](#phase-6-payment-abstraction)
  - [Phase 7: Fulfillment Service](#phase-7-fulfillment-service)
  - [Phase 8: Observability](#phase-8-observability)
  - [Phase 9: Operations Dashboard](#phase-9-operations-dashboard)
  - [Phase 10: Redundancy and Resilience](#phase-10-redundancy-and-resilience)
  - [Phase 11: Chaos and Failure Simulation](#phase-11-chaos-and-failure-simulation)
  - [Phase 12: Security](#phase-12-security)
  - [Phase 13: Testing and Quality](#phase-13-testing-and-quality)
  - [Phase 14: Production-like Local Infrastructure](#phase-14-production-like-local-infrastructure)
  - [Phase 15: AWS](#phase-15-aws)
  - [Phase 16: Production Scenarios and Edge Cases](#phase-16-production-scenarios-and-edge-cases)
- [Project Status](#project-status)

## Why Build Luna?

Luna is primarily a learning and experimentation platform. It explores concepts that are difficult to understand through isolated examples:

- Microservice boundaries and bounded contexts
- Asynchronous communication and event-driven architecture
- RabbitMQ and eventual consistency
- Transactional outbox and idempotency
- Retries and dead-letter queues
- Distributed tracing, structured logging, metrics, and health checks
- Service redundancy, failure recovery, and chaos testing
- API security and integration testing
- Containerized infrastructure and AWS equivalents

The architecture will evolve as the project exposes new problems. The objective is not to design the perfect architecture upfront, but to encounter realistic engineering problems and solve them deliberately.

## End Goal

Luna will be a locally hosted, production-inspired distributed system that simulates an e-commerce order from checkout through delivery. It will provide an operations dashboard for monitoring business health, infrastructure health, failures, performance, and recovery.

```text
Customer
   |
   v
Catalog ------> Orders
                 |  \
                 |   +------> Payments
                 |   +------> Inventory
                 |              |
                 |              v
                 |          Fulfillment
                 |              |
                 |              v
                 +----------> Shipping ---> Simulated Carrier
```

RabbitMQ connects the asynchronous workflows.

## Customer Experience

Customers can:

- Browse, search, and filter products
- Add products to a cart
- Select shipping methods and view shipping costs
- Checkout and place orders
- View order history
- Track order progress through a timeline

An order progresses through this simulated lifecycle:

```text
Order Created -> Payment -> Inventory Reserved -> Fulfillment
       -> Order Packed -> Shipping -> In Transit -> Delivered
```

## Operations Console

Luna will provide an internal operations dashboard covering both infrastructure and business health.

### Infrastructure health

```text
Orders        HEALTHY
Payments      HEALTHY
Inventory     HEALTHY
Fulfillment   HEALTHY
Shipping      HEALTHY
RabbitMQ      HEALTHY
```

### Business health

Examples include:

- Order stuck in fulfillment
- Shipment delayed
- Payment failure rate increasing
- Payment service unavailable
- RabbitMQ queue backlog increasing

The goal is to show not only whether a service is running, but whether the business is functioning correctly.

## Architecture

Luna is composed of independently deployable services. Each service owns its domain and persistence. Services do not directly access one another's databases; communication happens through APIs and asynchronous events.

During the initial web application phases, Next.js acts as a thin frontend gateway. It is the public application entry point, while the backend services remain HTTP APIs on the private Docker network and are not publicly exposed by default. The gateway forwards service contracts without introducing BFF DTOs or aggregation. That decision can be revisited when the Operations Console exposes a concrete need for aggregated views.

Authentication follows the same boundary: ASP.NET Core Identity owns users, passwords, roles, and account management; OpenIddict will eventually provide the OAuth 2.0/OpenID Connect authorization server; backend services validate access tokens but do not issue them. Next.js is the preferred OIDC client and server-side session boundary, keeping access tokens out of browser JavaScript by default.

### Bounded Contexts

Planned contexts include:

- Catalog
- Orders
- Payments
- Inventory
- Fulfillment
- Shipping

Initial databases:

```text
Catalog       -> CatalogDb
Orders        -> OrdersDb
Inventory     -> InventoryDb
Payments      -> PaymentsDb
Shipping      -> ShippingDb
```

Fulfillment is initially planned as a bounded context that can be extracted into its own service when the warehouse process becomes complex enough.

### Communication

The system will progressively move from simple synchronous calls to asynchronous events:

```text
Orders
   |
   | OrderCreated
   v
RabbitMQ
   +------> Payments
   +------> Inventory
   +------> other consumers
```

Example events:

`OrderCreated`, `PaymentAuthorized`, `PaymentFailed`, `InventoryReserved`, `InventoryReservationFailed`, `FulfillmentStarted`, `OrderPacked`, `ShipmentCreated`, and `ShipmentDelivered`.

### Reliability Patterns

The standard event flow will eventually include:

```text
Database transaction -> Outbox -> Event publisher -> RabbitMQ -> Consumer
                                                        |
                                                 Retry -> Idempotency -> DLQ
```

The system will also use timeouts and graceful shutdown so that failures can be observed and recovered from deliberately.

### Payment Abstraction

Payments depend on an `IPaymentProvider` rather than a specific provider.

```text
Payment Service -> IPaymentProvider -> FakePaymentProvider
```

The fake provider simulates authorization, capture, decline, timeout, provider errors, voids, and refunds. No real money is processed by Luna.

### Shipping

Shipping has two responsibilities:

1. Calculate and quote shipping during checkout.
2. Create and track shipments after fulfillment.

```text
Checkout:    Order -> Shipping -> Shipping Quote
Fulfillment: Fulfillment -> Shipping -> Carrier -> Tracking
```

Shipping owns the rate calculation. Orders snapshots the resulting shipping cost, along with product prices, so historical orders are not changed by future rate changes. Later, shipping quotes can expire and support multiple simulated carriers.

## Technology

| Area | Technologies |
| --- | --- |
| Backend | C#, .NET, ASP.NET Core, Entity Framework Core, SQL Server |
| Frontend | React, TypeScript, Next.js |
| Messaging | RabbitMQ |
| Infrastructure | Docker, Docker Compose |
| Observability | OpenTelemetry, Prometheus, Grafana, structured logging |
| Testing | xUnit, FluentAssertions, Moq, Testcontainers, Playwright |
| Cloud | AWS equivalents explored in later phases |

The initial implementation runs entirely locally. Later phases explore cloud deployment patterns.

## Development Philosophy

Complexity will be introduced when it solves a real problem:

```text
Simple synchronous workflow
        -> Asynchronous messaging
        -> Failure handling
        -> Outbox
        -> Idempotency
        -> Observability
        -> Redundancy
        -> Chaos testing
```

Each stage should build on the previous one without requiring large-scale rewrites. Testing begins in Phase 1 and expands as the system's risk and blast radius grow.

## Implementation Roadmap

### Phase 0: Architecture and Foundation

**Goal:** Establish service boundaries, projects, databases, Docker, configuration, health endpoints, logging, correlation IDs, API conventions, migrations, and testing conventions.

**Milestone:** All services run independently and communicate through defined APIs and events.

### Phase 1: Basic Commerce Flow

**Goal:** Build the customer storefront and complete the happy-path lifecycle: catalog, cart, checkout, order, payment, inventory, fulfillment, shipping, and delivery.

**Milestone:** A customer can place an order and eventually see it delivered.

### Phase 2: Messaging and Async Workflows

**Goal:** Introduce RabbitMQ, exchanges, queues, routing, consumers, acknowledgements, competing consumers, and eventual consistency.

**Milestone:** An order progresses asynchronously through the system.

### Phase 3: Realistic Time and Simulation

**Goal:** Add configurable simulation speed, randomized processing times, delays, and failures such as payment errors, carrier failures, inventory shortages, outages, and slow dependencies.

**Milestone:** A batch of orders can progress naturally while failures are observable locally.

### Phase 4: Reliability and Failure Handling

**Goal:** Add transient versus permanent failure handling, retries with backoff, idempotency, dead-letter queues, timeouts, and graceful shutdown.

**Milestone:** Deliberately broken components can recover correctly.

### Phase 5: Transactional Outbox

**Goal:** Persist domain changes and their events in one transaction, then publish through an outbox worker.

**Milestone:** A database commit cannot silently lose a business event.

### Phase 6: Payment Abstraction

**Goal:** Model payment attempts, idempotency keys, authorization, capture, void, refunds, and provider failures behind `IPaymentProvider`.

**Milestone:** Payments behave like an external dependency without processing real money.

### Phase 7: Fulfillment Service

**Goal:** Separate inventory (do we have the item?) from fulfillment (have we prepared it?) and shipping (where is the package?).

**Milestone:** Warehouse operations are independently modeled and communicated through events.

### Phase 8: Observability

**Goal:** Add structured logs, distributed traces, metrics, and health checks using OpenTelemetry, Prometheus, and Grafana.

Track order and payment durations, failures, queue depth, DLQ size, and correlation and trace IDs.

**Milestone:** An operator can determine where and why a workflow failed.

### Phase 9: Operations Dashboard

**Goal:** Build a control center showing service health, business alerts, queue depth, failed messages, processing times, payment success rate, latency, and stuck orders.

**Milestone:** The dashboard becomes a window into the distributed system.

### Phase 10: Redundancy and Resilience

**Goal:** Run multiple service and consumer instances behind load balancing. Add readiness and liveness checks, connection recovery, circuit breakers, timeouts, graceful shutdown, and recovery experiments.

**Milestone:** The platform continues operating when an individual instance is killed.

### Phase 11: Chaos and Failure Simulation

**Goal:** Add operations controls to pause services, inject payment or carrier failures, increase fulfillment delays, and observe backlog recovery.

**Milestone:** Failure, detection, recovery, and observability can be demonstrated end to end.

### Phase 12: Security

**Goal:** Add OAuth/OIDC, JWT authentication, authorization and RBAC, service-to-service security, HTTPS, secrets management, rate limiting, validation, and audit logging.

Roles include Customer, Warehouse Operator, Support, and Administrator.

### Phase 13: Testing and Quality

**Goal:** Expand unit, integration, contract, end-to-end, and failure testing.

Use real databases and RabbitMQ through Testcontainers. Cover duplicate events, service outages, database failures, consumer crashes, carrier timeouts, and other unhappy paths.

### Phase 14: Production-like Local Infrastructure

**Goal:** Run Luna in a production-like local environment and learn how infrastructure is provisioned and configured.

This phase includes Docker and Docker Compose, a reverse proxy or gateway, multiple service instances, databases, RabbitMQ, observability, CI/CD, backups, production-like configuration, and self-hosted infrastructure automation with Terraform and Ansible.

The learning progression is:

```text
Terraform
        ↓ Provision infrastructure
Ansible
        ↓ Configure Linux hosts
Docker
        ↓
Docker Compose
        ↓
Luna
```

### Phase 15: AWS

**Goal:** Explore how the production-like local architecture maps to AWS and learn infrastructure-as-code and managed container deployment.

Terraform provisions and configures the AWS infrastructure:

```text
Terraform
        ↓ AWS infrastructure
        ├── VPC
        ├── Networking
        ├── IAM
        ├── ECS/Fargate
        ├── RDS
        ├── S3
        ├── SQS
        ├── API Gateway
        └── CloudWatch
```

The local-to-cloud translation is:

| Local | AWS |
| --- | --- |
| Docker Compose | ECS/Fargate |
| SQL Server container | RDS |
| RabbitMQ | SQS |
| S3-like storage | S3 |
| Reverse proxy | ALB/API Gateway |
| Local configuration and secrets | Secrets Manager |
| Logs | CloudWatch |
| Terraform | Terraform |

The point is to understand how cloud infrastructure provides capabilities the local system already demonstrates.

### Phase 16: Production Scenarios and Edge Cases

**Goal:** Challenge the completed system with realistic business and distributed-system failures, then document ownership, persistence, events, retries, idempotency, customer impact, operational impact, and recovery.

Scenarios include:

- Duplicate messages and duplicate payment requests
- Payment success followed by order creation failure
- Payment failure after inventory reservation
- Inventory reservation expiration
- Order cancellation at different lifecycle stages
- Product price or shipping quote changes after checkout
- Delayed fulfillment and carrier failures
- Carrier and internal tracking discrepancies
- Multiple warehouses and multiple shipments per order
- RabbitMQ, database, and service instance outages
- Slow dependencies and message backlog recovery
- Event and schema version changes

## Project Status

**Early Development**

Luna is being built incrementally. Architecture and implementation decisions may change as new requirements and failure scenarios are introduced. The repository is intentionally a work in progress.
Why build Luna?

This project is primarily a learning and experimentation platform.

It is an opportunity to explore concepts that are difficult to understand through isolated examples:

Microservice boundaries
Asynchronous communication
Event-driven architecture
RabbitMQ
Eventual consistency
Transactional Outbox
Idempotency
Retries and dead-letter queues
Distributed tracing
Structured logging
Metrics and monitoring
Health checks
Service redundancy
Failure recovery
Chaos testing
API security
Integration and end-to-end testing
Containerized infrastructure
AWS equivalents of local infrastructure

The architecture will evolve as the project exposes new problems.

The objective is not to design the "perfect" architecture upfront, but to encounter realistic engineering problems and solve them deliberately.

Customer Experience

Customers can:

Browse products
Search and filter products
Add products to a cart
Checkout
Select shipping methods
View shipping costs
Place orders
View order history
Track order progress

An order progresses through a simulated lifecycle:

Order Created
      │
      ▼
Payment
      │
      ▼
Inventory Reserved
      │
      ▼
Fulfillment
      │
      ▼
Order Packed
      │
      ▼
Shipping
      │
      ▼
In Transit
      │
      ▼
Delivered

The customer can see this progress through an order timeline.

Operations Console

Luna will also provide an internal operations dashboard for monitoring the platform.

It will expose both infrastructure and business-level health.

Infrastructure
Orders        ● HEALTHY
Payments      ● HEALTHY
Inventory     ● HEALTHY
Fulfillment   ● HEALTHY
Shipping      ● HEALTHY
RabbitMQ      ● HEALTHY
Business Health

Examples include:

⚠ Order stuck in fulfillment
⚠ Shipment delayed
⚠ Payment failure rate increasing
🔴 Payment Service unavailable
⚠ RabbitMQ queue backlog increasing

The goal is to make it possible to understand not only whether a service is running, but whether the business is actually functioning correctly.

Simulation

Luna deliberately simulates real-world conditions.

Processing times can be randomized:

Payment          5–30 simulated minutes
Inventory        1–10 simulated minutes
Picking          20–60 simulated minutes
Packing          5–20 simulated minutes
Carrier pickup   30–120 simulated minutes
Transit          1–3 simulated days

Simulation speed is configurable so that long-running processes can be observed locally.

The system can also inject failures such as:

Payment failures
Carrier failures
Processing delays
Inventory shortages
Service outages
Slow dependencies

Eventually, the operations console will provide controls for intentionally breaking parts of the system.

Architecture

Luna is composed of independently deployable services.

Planned bounded contexts include:

Catalog
Orders
Payments
Inventory
Fulfillment
Shipping

Each service owns its domain and persistence.

Services do not directly access each other's databases.

Communication is performed through APIs and asynchronous events.

Example
Orders
   │
   │ OrderCreated
   ▼
RabbitMQ
   │
   ├──────────────► Payments
   │
   ├──────────────► Inventory
   │
   └──────────────► other consumers

The system will progressively introduce reliability patterns such as:

Service
   │
   ├── Database Transaction
   │       │
   │       └── Outbox
   │
   └── Event Publisher
           │
           ▼
        RabbitMQ
           │
           ▼
        Consumer
           │
           ├── Retry
           ├── Idempotency
           └── Dead Letter Queue
Payment Architecture

Payments are intentionally abstracted behind a provider interface.

Payment Service
       │
       ▼
IPaymentProvider
       │
       ▼
FakePaymentProvider

The simulated provider can produce successful payments, declines, timeouts, and other failures.

The architecture allows a real provider implementation to theoretically be introduced later without coupling the Payment domain to a specific provider.

No real money is processed by Luna.

Shipping

Shipping is responsible for both shipping quotes and shipment management.

During checkout:

Order
  │
  │ Calculate shipping
  ▼
Shipping
  │
  ▼
Shipping Quote

After fulfillment:

Fulfillment
     │
     │ OrderPacked
     ▼
Shipping
     │
     ▼
Carrier
     │
     ▼
Tracking

Shipping costs and product prices are captured in the order so historical orders are not affected by future pricing changes.

Technology
Backend
C#
.NET
ASP.NET Core
Entity Framework Core
SQL Server
Frontend
React
TypeScript
Next.js
Messaging
RabbitMQ
Infrastructure
Docker
Docker Compose
Observability
OpenTelemetry
Prometheus
Grafana
Structured logging
Testing
xUnit
FluentAssertions
Moq
Integration Testing
Testcontainers
Playwright
Cloud

The initial implementation is designed to run entirely locally.

Later phases will explore AWS equivalents and cloud deployment patterns.

Development Philosophy

Luna will be built incrementally.

The architecture will not start with every possible microservice, infrastructure component, and reliability pattern.

Instead, complexity will be introduced when it solves a real problem.

For example:

Simple synchronous workflow
        ↓
Asynchronous messaging
        ↓
Failure handling
        ↓
Outbox
        ↓
Idempotency
        ↓
Observability
        ↓
Redundancy
        ↓
Chaos testing

Each stage should build on the previous one without requiring large-scale rewrites.

Roadmap
Phase 0 — Architecture & Foundation

Establish service boundaries, projects, databases, Docker, configuration, and development conventions.

Phase 1 — Basic Commerce Flow

Build the customer storefront and complete the basic order lifecycle.

Phase 2 — Messaging & Async Workflows

Introduce RabbitMQ and event-driven communication.

Phase 3 — Simulation & Realistic Time

Introduce configurable processing times, delays, and simulated failures.

Phase 4 — Reliability & Failure Handling

Retries, timeouts, idempotency, and dead-letter queues.

Phase 5 — Transactional Outbox

Guarantee reliable publication of domain events.

Phase 6 — Payment Abstraction

Introduce provider abstraction and realistic payment lifecycle behavior.

Phase 7 — Fulfillment Service

Separate warehouse operations into their own bounded context.

Phase 8 — Observability

Logging, metrics, distributed tracing, and health checks.

Phase 9 — Operations Dashboard

Build the internal monitoring and operations console.

Phase 10 — Redundancy & Resilience

Multiple service instances, load balancing, graceful shutdown, and recovery.

Phase 11 — Chaos / Failure Simulation

Intentionally break services and observe system behavior.

Phase 12 — Security

Authentication, authorization, service-to-service security, secrets, and auditing.

Phase 13 — Testing & Quality

Expand unit, integration, contract, end-to-end, and failure testing.

Phase 14 — Production-like Local Infrastructure

Run the complete platform as a production-like local environment and learn self-hosted infrastructure automation with Terraform, Ansible, Docker, and Docker Compose.

Phase 15 — AWS

Translate the local architecture to AWS with Terraform, VPC networking, IAM, ECS/Fargate, RDS, S3, SQS, API Gateway, and CloudWatch.

Phase 16 — Production Scenarios & Edge Cases

Test and document realistic business and distributed-system failures.

Production Scenarios

The final system will be tested against scenarios such as:

Duplicate messages
Duplicate payment requests
Payment succeeds but order creation fails
Payment fails after inventory reservation
Inventory reservation expiration
Order cancellation at different lifecycle stages
Product price changes after checkout
Shipping quote expiration
Delayed fulfillment
Carrier failures
Carrier/Atlas state discrepancies
Multiple warehouses
Multiple shipments per order
RabbitMQ outages
Database outages
Service instance failures
Slow dependencies
Message backlog recovery
Event/schema version changes

Each scenario will document:

What can go wrong?
Which service owns the problem?
What state is persisted?
Which events are produced?
Can the operation be retried?
How is idempotency handled?
What does the customer see?
What does the operations team see?
How does the system eventually recover?
Project Status

🚧 Early Development

Luna is being built incrementally. Architecture and implementation decisions may change as new requirements and failure scenarios are introduced.

The repository is intentionally a work in progress.



