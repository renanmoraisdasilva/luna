# Luna

### Distributed Commerce & Logistics Simulation

Luna is a production-inspired commerce and logistics platform built to explore what happens when a simple commerce workflow becomes a distributed system.

The project simulates the journey of an order from browsing and checkout through payment, inventory, fulfillment, shipping, and delivery. As Luna evolves, it deliberately introduces the kinds of complexity that appear in real platforms: asynchronous workflows, delays, failures, retries, recovery, observability, and operational intervention.

> **The goal is not to design the perfect distributed system upfront.**
> Luna is built incrementally so that each new problem gives the architecture a reason to evolve.

---

## The Idea

At its simplest, Luna looks like this:

```text
Customer
   │
   ▼
Catalog
   │
   ▼
Cart → Checkout → Order
                    │
             ┌──────┴──────┐
             ▼             ▼
          Payment       Inventory
                            │
                            ▼
                       Fulfillment
                            │
                            ▼
                         Shipping
                            │
                            ▼
                         Delivery
```

Initially, these workflows are intentionally simple. Later phases introduce asynchronous communication, realistic timing, failures, retries, and recovery.

The result is a system where you can observe not only **what happens when everything works**, but also what happens when things don't.

---

## Customer Experience

Luna will provide a customer-facing storefront where customers can:

- Browse products
- Search and filter products
- Add products to a cart
- Choose shipping methods
- Checkout and place orders
- View order history
- Follow order progress
- Track deliveries

An order will eventually progress through a lifecycle such as:

```text
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
```

---

## Operations

As the system grows, Luna will also include an internal operations console.

The goal is to make the platform observable from a business perspective, not just a technical one.

For example:

```text
Infrastructure

Orders        ● HEALTHY
Payments      ● HEALTHY
Inventory     ● HEALTHY
Shipping      ● HEALTHY


Business

⚠ Payment failures increasing
⚠ Order stuck in fulfillment
⚠ Shipment delayed
⚠ Work building up
```

Eventually, the operations experience will also allow deliberate failures and simulation controls so that the behavior of the distributed system can be explored interactively.

---

## The Journey

Luna is built in stages. Each phase introduces a new capability or a new problem to solve.

```text
Simple Commerce
       │
       ▼
Async Workflows
       │
       ▼
Realistic Simulation
       │
       ▼
Reliability & Recovery
       │
       ▼
Transactional Consistency
       │
       ▼
Observability
       │
       ▼
Operations
       │
       ▼
Resilience & Chaos
       │
       ▼
Production Scenarios
```

The current roadmap progresses through:

| Phase | Focus |
| --- | --- |
| 0 | Architecture & Foundation |
| 1 | Basic Commerce Flow |
| 2 | Messaging & Async Workflows |
| 3 | Realistic Time & Simulation |
| 4 | Reliability & Failure Handling |
| 5 | Transactional Outbox |
| 6 | Payment Abstraction |
| 7 | Fulfillment Service |
| 8 | Observability |
| 9 | Operations Dashboard |
| 10 | Redundancy & Resilience |
| 11 | Chaos & Failure Simulation |
| 12 | Security |
| 13 | Testing & Quality |
| 14 | Production-like Local Infrastructure |
| 15 | AWS |
| 16 | Production Scenarios & Edge Cases |

See the [Roadmap](documentation/roadmap.md) for the complete progression.

---

## Run Luna

### Prerequisites

- Docker Desktop

Start the Phase 0 environment from the repository root:

```bash
docker compose -f infrastructure/docker-compose.yml up --build
```

For local frontend development without rebuilding the frontend container on every change, start the backend services with the development Compose override:

```bash
docker compose \
-f infrastructure/docker-compose.yml \
-f infrastructure/docker-compose.dev.yml \
-f infrastructure/docker-compose.observability.yml \
up --build \
sqlserver identity catalog orders payments inventory shipping \
signoz signoz-migrator otel-collector
```

Then run the frontend from `src/Frontend`:

```bash
npm run dev
```

The development override publishes the backend services on ports `5001` through `5006`, matching the frontend's local `.env` rewrite destinations. Use `--force-recreate` if the services were previously started with the default Compose file. Restart `npm run dev` after changing `.env` because Next.js reads the rewrite destinations at startup.

This command also starts SigNoz and the OpenTelemetry collector. The SigNoz UI is available at `http://localhost:8080`, and OTLP receivers are available on ports `4317` and `4318`.

The storefront is available at:

```text
http://localhost:3000
```

The unified API explorer is available at:

```text
http://localhost:3000/swagger
```

Backend services are kept private to the Compose network and are reached through the frontend gateway.

SQL Server runs locally with a separate database for each service.

RabbitMQ is intentionally not part of Phase 0; it is introduced in Phase 2.

---

## Project Status

🚧 **Early Development**

**Phase 0 — Complete**

The architecture and development foundation are established, including service boundaries, databases, containerization, configuration, API conventions, health checks, logging, testing foundations, and the frontend foundation.

**Phase 1 — Next**

The focus is the first complete customer commerce experience: catalog, anonymous cart, authentication where required, checkout, orders, payments, inventory, fulfillment, shipping, and the customer-facing order lifecycle.

---

## Documentation

The repository separates **what Luna is**, **where it is going**, and **how each phase is designed**.

- [Roadmap](documentation/roadmap.md) — Project vision and progression
- [Phase 0 Design](documentation/phase-0-design.md) — Foundation and architectural decisions
- [Phase 1 Specification](documentation/dev_phases/phase-1-spec.md) — Basic commerce flow requirements and decisions
- [Phase 1 Architecture](documentation/dev_phases/phase-1-architecture.md) — Technical design, boundaries, and architectural decisions
- [Observability](documentation/observability.md) — OpenTelemetry, OTLP, SigNoz, and checkout trace verification

For frontend-specific implementation details, see the frontend documentation under `src/Frontend/`.

---

## Why Luna?

Luna is primarily a learning and experimentation platform.

The interesting part is not simply building an online store. It is progressively turning that store into a distributed platform and learning what changes when:

- services operate independently
- work happens asynchronously
- failures occur during workflows
- operations take time
- messages can be delayed or duplicated
- dependencies become unavailable
- the system needs to recover
- operators need to understand what is happening

The architecture is therefore expected to evolve.

```text
Simple
  │
  ▼
Useful
  │
  ▼
Distributed
  │
  ▼
Asynchronous
  │
  ▼
Failure-prone
  │
  ▼
Observable
  │
  ▼
Resilient
```

Luna is intentionally built one step at a time.
