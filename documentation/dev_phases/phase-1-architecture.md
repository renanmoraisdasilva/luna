# Phase 1 Architecture

## Contents

- [1. Architectural Goals](#1-architectural-goals)
- [2. Service Boundaries](#2-service-boundaries)
- [3. Communication and Gateway](#3-communication-and-gateway)
- [4. Database Ownership](#4-database-ownership)
- [5. Backend Layering](#5-backend-layering)
- [6. Domain Model](#6-domain-model)
- [7. Read and Write Architecture](#7-read-and-write-architecture)
- [8. Frontend Architecture](#8-frontend-architecture)
- [9. Authentication Architecture](#9-authentication-architecture)
- [10. Checkout Coordination](#10-checkout-coordination)
- [11. Persistence](#11-persistence)
- [12. Testing Architecture](#12-testing-architecture)
- [13. Architectural Decisions](#13-architectural-decisions)
- [14. Deferred Architecture](#14-deferred-architecture)
- [15. Architecture Implementation Checklist](#15-architecture-implementation-checklist)

# 1. Architectural Goals

Phase 1 architecture should preserve the service boundaries established in Phase 0, keep databases independently owned, keep backend services private, allow Orders to coordinate checkout, keep Fulfillment inside Orders, and leave a clean path for asynchronous communication in Phase 2.

# 2. Service Boundaries

## Checkout orchestration

### SPEC-ARCH-001 - Orders owns checkout

**Given** a customer submits checkout

**Then** Orders coordinates the checkout workflow.

Orders may call:

* Catalog
* Shipping
* Inventory
* Payments
* Fulfillment

Other services must not orchestrate the checkout process.

---

### SPEC-ARCH-002 - Service ownership

Each service owns:

* its domain
* its application logic
* its persistence
* its migrations
* its API

No service may directly access another service's internal implementation.

---

### SPEC-ARCH-003 - Database isolation

**Given** two services

**Then** neither service may query the other's database.

Cross-service relationships are represented using IDs and service APIs.

---

# 3. Communication and Gateway

Phase 1 uses synchronous HTTP. Next.js is the public HTTP boundary and thin gateway. The browser does not directly address Docker service names or private service URLs.

```text
Browser -> Next.js -> backend service
Orders -> Catalog, Shipping, Inventory, Payments
```

RabbitMQ is deferred to Phase 2.

# 4. Database Ownership

## Database Schema

Each service continues to own its own database.

## CatalogDb

```text
Category
Product
ProductImage
```

## OrdersDb

```text
Cart
CartItem
Order
OrderItem
ShippingAddress
```

## PaymentsDb

```text
Payment
PaymentAttempt
```

## InventoryDb

```text
Warehouse
Stock
InventoryReservation
```

## ShippingDb

```text
ShippingMethod
ShippingQuote
Shipment
TrackingEvent
```

Identity uses the ASP.NET Core Identity schema.

---
## Database Rules

## SPEC-DB-001 - Migrations

Database schema changes use EF Core migrations.

`EnsureCreated` is not used for application databases.

---

## SPEC-DB-002 - Migration startup

Services apply pending migrations before executing baseline seed logic.

Schema evolution belongs to migrations.

Baseline data belongs to seed logic.

---

## SPEC-DB-003 - Cross-service relationships

No cross-database foreign keys are created.

Cross-service relationships are represented using IDs.

---

# 5. Backend Layering

## Application Structure

Each backend service follows the Phase 0 structure:

```text
Service/
+---- Controllers/
+---- Application/
+---- Domain/
+---- Infrastructure/
+---- Program.cs
```

The structure is a boundary, not a requirement for excessive abstraction.

## Controllers

Controllers handle:

* HTTP routing
* request binding
* authentication/authorization metadata
* HTTP response mapping

Controllers do not contain:

* business rules
* EF queries
* checkout orchestration
* inventory calculations
* payment decisions
* domain state transitions

---

## Application

Application code coordinates use cases.

Examples:

```text
Checkout
ReserveInventory
ReleaseReservation
AuthorizePayment
CreateShipment
GetOrders
GetCart
```

---

## Domain

Domain code owns business behavior such as:

* order state transitions
* inventory invariants
* payment state
* domain concepts

---

## Infrastructure

Infrastructure owns:

* EF Core
* database configuration
* migrations
* seed data
* HTTP clients
* external integrations

---

# 6. Domain Model

The current bounded-context model is:

```text
Catalog: Product, Category, ProductImage
Orders: Cart, CartItem, Order, OrderItem, ShippingAddress
Payments: Payment, PaymentAttempt
Inventory: Stock, InventoryReservation
Shipping: ShippingMethod, ShippingQuote, Shipment, TrackingEvent
```

Fulfillment remains a module inside Orders during Phase 1.

## Domain-Driven Design Boundaries

Aggregate roots protect invariants within each bounded context. An aggregate is changed through its root; child entities should not be modified independently.

```text
Catalog:   Product -> ProductImage
Orders:    Cart -> CartItem
           Order -> OrderItem, ShippingAddress
Inventory: Stock -> InventoryReservation
Payments:  Payment -> PaymentAttempt
Shipping:  Shipment -> TrackingEvent
```

Important ownership rules include:

* `Product` owns its images, including image ordering and lifecycle.
* `Cart` owns item quantities and add/remove behavior.
* `Order` owns item price snapshots, address snapshots, and valid status transitions.
* `Stock` owns availability and reservation invariants, including concurrency-safe reserve and release behavior.
* `Payment` owns authorization state and payment-attempt history.
* `Shipment` owns its tracking lifecycle.

The most useful value objects are `Money`, `Sku`, `CategorySlug`, `Quantity`, and `ShippingAddress`. They should be introduced when related write behavior requires stronger validation; they are not required for every read model.

Domain services are reserved for behavior that does not naturally belong to one aggregate, such as shipping-rate calculation. Checkout coordination is an Orders application responsibility, not a domain service, because it coordinates Catalog, Shipping, Inventory, Payments, and Fulfillment. HTTP clients, provider adapters, EF Core repositories, and read projections remain outside the domain layer.

# 7. Read and Write Architecture

## Read and Write Behavior

## Reads

Read repositories project directly to Application DTOs/read models.

```text
Database
   v
EF Core projection
   v
Application DTO
   v
Handler
   v
Controller
```

Read operations should not materialize domain entities unnecessarily.

---

## Writes

Write operations may load domain entities when business behavior is required.

```text
Request
   v
Application
   v
Load domain state
   v
Execute domain behavior
   v
Persist
```

---

# 8. Frontend Architecture

The frontend uses Next.js, React, TypeScript, Axios, and TanStack Query.

- Catalog and product content are Server Components where practical.
- Cart, checkout, shipping quotes, and interactive inventory availability use Client Components with TanStack Query.
- Centralized API clients keep HTTP calls out of UI components.
- URL parameters remain the source of truth for catalog filters and pagination.

# 9. Authentication Architecture

Next.js is the browser/session boundary. The target design uses OpenIddict/OIDC, keeps access tokens out of browser JavaScript by default, and forwards authorized requests to private backend APIs. Backend services validate tokens and enforce authorization.

# 10. Checkout Coordination

Orders owns checkout orchestration. The intended synchronous sequence is:

```text
Orders -> Catalog: validate products and prices
Orders -> Shipping: calculate quote
Orders: create Pending order and snapshots
Orders -> Inventory: reserve
Orders -> Payments: authorize
Orders: Confirmed
Orders: Fulfillment.Prepare
Orders -> Shipping: create shipment
```

# 11. Persistence

Services use SQL Server and EF Core. EF Core migrations own schema evolution; startup applies pending migrations before separate seed logic runs. Seed data belongs in infrastructure rather than controllers or root-level service files.

# 12. Testing Architecture

## Testing Requirements

## SPEC-TEST-001 - Business behavior

Tests must cover meaningful business behavior rather than only implementation details.

Important examples include:

* order state transitions
* inventory reservation
* insufficient inventory
* concurrent reservation
* payment failure
* reservation release
* price snapshots
* shipping failure
* checkout orchestration

---

## SPEC-TEST-002 - Catalog reads

Catalog integration tests must verify real EF Core behavior such as:

* filtering
* search
* ordering
* relationships
* image ordering
* DTO projection
* not-found behavior

These tests should use real SQL Server/Testcontainers rather than mocking EF Core query translation.

---

## SPEC-TEST-003 - Cross-service behavior

Cross-service workflows are verified through integration/E2E tests.

Unit tests should not attempt to prove that the distributed system works by mocking every service.

---

## SPEC-TEST-004 - Coverage

Coverage is collected as a quality and regression signal.

Coverage does not have an arbitrary minimum threshold in Phase 1.

Tests should prioritize meaningful behavior and decision branches.

---

The repository also separates unit, integration, and smoke tests under the centralized tests/ structure. CI runs backend and frontend tests and a Docker Compose health smoke check.

# 13. Architectural Decisions

## Decisions

1. Orders owns checkout orchestration.
2. Phase 1 communication is synchronous HTTP.
3. RabbitMQ begins in Phase 2.
4. Fulfillment remains inside Orders until Phase 7.
5. Each service owns its database.
6. No cross-service database access is permitted.
7. Catalog owns current product prices.
8. Cart does not lock prices.
9. Orders snapshots prices during checkout.
10. Shipping owns shipping calculation.
11. Orders snapshots shipping cost.
12. Inventory owns stock and reservations.
13. Inventory reservation must be concurrency-safe.
14. Distributed locks are not required.
15. Payment authorization occurs only after successful inventory reservation.
16. Payment failure releases inventory.
17. Payments supports authorization only.
18. The fake payment provider is deterministic.
19. Phase 1 has exactly one shipment per order.
20. Shipment failure results in `ShippingPendingRetry`.
21. Automatic retry is deferred.
22. Customers cannot cancel orders.
23. Next.js remains the public frontend boundary.
24. Backend services remain private.
25. The browser does not directly address internal service URLs.
26. TanStack Query manages interactive frontend server state.
27. Catalog/product content is server-first where practical.
28. Controllers remain thin.
29. Application code coordinates use cases.
30. Domain code owns business behavior.
31. Infrastructure owns persistence and external integrations.
32. Read repositories project directly to Application DTOs/read models.
33. Writes use domain entities when domain behavior is required.
34. EF Core migrations own schema evolution.
35. Seed logic owns baseline data.
36. Phase 1 does not introduce a shared cross-service domain library.
37. Phase 1 does not introduce a full BFF.
38. Phase 1 does not attempt to solve distributed reliability.

# 14. Deferred Architecture

Phase 2 and later work includes RabbitMQ, asynchronous consumers, transactional outbox, retries, idempotency, compensation, distributed tracing, advanced metrics, operations tooling, redundancy, chaos testing, and extraction of Fulfillment into its own service.

# 15. Architecture Implementation Checklist

## Service boundaries

- [ ] Orders is the only service that orchestrates checkout.
- [ ] Each service owns its domain, application logic, persistence, migrations, and API.
- [ ] Services communicate through contracts and APIs rather than internal implementation details.
- [ ] No service directly accesses another service's database.

## Domain-Driven Design

- [x] Aggregate roots are identified and documented for each implemented bounded context.
- [x] `Product` owns `ProductImage` and controls image ordering and lifecycle.
- [ ] `Cart` owns `CartItem` and controls quantity and item changes.
- [ ] `Order` owns `OrderItem` and `ShippingAddress` snapshots and controls valid state transitions.
- [ ] `Stock` owns inventory reservations and enforces availability and concurrency invariants.
- [ ] `Payment` owns payment attempts and authorization state.
- [ ] `Shipment` owns tracking events and its shipment lifecycle.
- [ ] Aggregate child entities cannot be modified independently through application use cases.
- [ ] Value objects are introduced where they protect meaningful domain rules, especially money, quantity, SKU, and address data.
- [ ] Domain services are used only for behavior that does not naturally belong to one aggregate.
- [ ] Checkout coordination remains in the Orders application layer rather than becoming a domain service.

## Layering and persistence

- [ ] Controllers remain limited to HTTP concerns and response mapping.
- [ ] Application services coordinate use cases and cross-service calls.
- [ ] Domain code owns business rules and state transitions.
- [ ] Infrastructure contains EF Core, migrations, HTTP clients, and external-provider adapters.
- [ ] Read repositories project directly to read DTOs without unnecessarily materializing aggregates.
- [ ] Write operations load and modify aggregates when business behavior is required.

## Verification

- [ ] Aggregate invariants have focused unit tests.
- [ ] Persistence behavior has integration tests against real database infrastructure.
- [ ] Cross-service workflows have integration or end-to-end coverage.
- [ ] Architecture changes are reviewed against the bounded-context and ownership rules above.
