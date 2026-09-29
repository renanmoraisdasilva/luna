# Luna

**Distributed commerce and logistics, built to see what breaks.**

Luna is an order-management system that treats the boring parts of an online
store as distributed-systems problems. A customer checks out; the order has to
be paid for, reserved against stock, picked, packed, shipped and tracked — and
every one of those steps can fail, take time, or arrive twice. Luna exists to
make those behaviours visible and recoverable rather than assumed away.

It is a learning platform. Each phase introduces a real problem so the
architecture has a reason to change.

![The storefront, checkout, and the Luna Ops fulfillment and shipments screens](documentation/luna-demo.gif)

*The demo is the real application, driven end to end: a customer registers, fills
a cart, places an order, and an operator prepares and ships it. Regenerate it
with `npm run demo:build`.*

---

## What actually happens to an order

Checkout does not "create an order". It starts a pipeline with a real state
machine, and every transition is guarded in the domain rather than assumed by
the caller.

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Confirmed: payment authorised,<br/>inventory reserved
    Pending --> PaymentFailed: payment declined
    Pending --> Cancelled
    Confirmed --> Preparing: operator starts preparation
    Preparing --> Shipped: shipment created
    Shipped --> Delivered
    Delivered --> [*]
    PaymentFailed --> [*]
    Cancelled --> [*]
```

The states are `OrderStatus` in `src/Services/Orders/Orders.Domain/Order.cs`,
and each move is a method on the aggregate that refuses to run from the wrong
state. `PaymentFailed` and `Cancelled` are terminal branches, not retries.

Fulfilment then interleaves the order with a shipment, and the two are not
allowed to drift apart:

```mermaid
flowchart LR
    O1["Order<br/><b>Confirmed</b>"] --> O2["Order<br/><b>Preparing</b>"]
    O2 -->|"Create Shipment<br/>(only while Preparing)"| S1["Shipment<br/><b>Created</b>"]
    S1 -->|"Mark In Transit<br/>(requires order Shipped)"| S2["Shipment<br/><b>InTransit</b>"]
    S2 --> D["Order + Shipment<br/><b>Delivered</b>"]
```

That coupling is the point. A shipment cannot be created before the order is
being prepared, and a shipment cannot move while the order has not shipped. The
guards live in `Order.cs` as `EnsureShipmentCreationAllowed`,
`EnsureStatusForShipmentInTransit` and `EnsureDeliveryAllowed`.

---

## The application

**Customer side**

| Storefront | Checkout |
| --- | --- |
| ![Storefront](documentation/images/storefront.webp) | ![Checkout](documentation/images/checkout.webp) |
| Catalogue with search and category filters. Every product is a real database record. | The transaction: contact, payment method, shipping address and a chosen shipping method, all validated before the order exists. |

**Operations side**

| Fulfillment queue | Fulfillment detail | Shipments |
| --- | --- | --- |
| ![Fulfillment queue](documentation/images/fulfillment-queue.webp) | ![Fulfillment detail](documentation/images/fulfillment-detail.webp) | ![Shipments](documentation/images/shipments.webp) |
| The operator's queue: confirmed orders awaiting preparation, with payment status and a one-click action. | The five-step pipeline, the order lines with their SKUs, the totals, and the next command available. | Every shipment with a real tracking number, its status, and the action to move it forward. |

The fulfillment detail screen is the clearest single picture of the whole
system: the pipeline at the top, the goods and money in the middle, and the
single operation you are allowed to perform right now on the right.

---

## Architecture

```mermaid
flowchart TB
    Browser["Browser"] -->|"HTTP only"| GW["Next.js gateway<br/>frontend : 3000"]

    subgraph Svc["Backend services — .NET 8, each owning its own database"]
        Identity["Identity<br/>OpenIddict"]
        Catalog["Catalog"]
        Orders["Orders"]
        Payments["Payments"]
        Inventory["Inventory"]
        Shipping["Shipping"]
    end

    subgraph Data["SQL Server — one database per service"]
        IdDb[("IdentityDb")]
        CatDb[("CatalogDb")]
        OrdDb[("OrdersDb")]
        PayDb[("PaymentsDb")]
        InvDb[("InventoryDb")]
        ShipDb[("ShippingDb")]
    end

    GW --> Identity
    GW --> Catalog
    GW --> Orders
    GW --> Payments
    GW --> Inventory
    GW --> Shipping

    Identity --> IdDb
    Catalog --> CatDb
    Orders --> OrdDb
    Payments --> PayDb
    Inventory --> InvDb
    Shipping --> ShipDb
```

Three decisions shape everything else:

**The browser talks to one service.** The Next.js gateway is the only public
HTTP boundary. Backend containers are not published; nothing in the frontend
addresses a Docker service name.

**A service owns its data.** Six databases, and no service reads another's
tables. Cross-service work goes through an application port and a service token,
never a shared connection string.

**Five-layer Clean Architecture, repeated.** Five of the six services use the
same `Api / Application / Contracts / Domain / Infrastructure` split, with
domain code holding the invariants and controllers staying thin. Identity is a
single project because its host predates the template; its folders still keep
the responsibilities apart.

Messaging arrives in Phase 2 on AWS primitives — an EventBridge bus, SQS with
dead-letter queues, and SES — declared in Terraform so the same files target
the local emulator, a server, and real AWS. The bus and all four queues carry
`prevent_destroy`, because Terraform cannot know whether running code still
needs them.

---

## What is built today

- **6 backend services** on .NET 8, across 29 projects, plus a Next.js frontend.
- **6 SQL Server databases**, one per service, migrated on startup.
- **A five-step fulfillment pipeline** with domain-guarded transitions and a
  live operations console.
- **OpenIddict authentication** with role-based access; the operations console is
  gated on `Admin` by a real authorization policy, not just a hidden link.
- **93 test files** — 57 backend, 36 frontend.
- **3 CI workflows** covering validation, Docker image publishing, and Terraform
  plan/apply.

Phase 1 is in closure. Phase 2 — messaging and asynchronous workflows — is the
next one.

---

## Run it

```bash
cp infrastructure/.env.example infrastructure/.env    # then edit it
docker compose -f infrastructure/docker-compose.yml up --build
```

That is the whole stack: SQL Server, the six services, the gateway and the AWS
emulator. Storefront at <http://localhost:3000>, API explorer at
<http://localhost:3000/swagger>.

Full instructions, including frontend hot reload, observability, Terraform and
the demo scripts, are in **[Getting Started](documentation/getting-started.md)**.

---

## Roadmap

| Phase | Focus | |
| --- | --- | --- |
| 0 | Architecture & Foundation | done |
| 1 | Basic Commerce Flow | closing |
| 2 | Messaging & Async Workflows | next |
| 3 | Realistic Time & Simulation | |
| 4 | Reliability & Failure Handling | |
| 5 | Transactional Outbox | |
| 6 | Payment Abstraction | |
| 7 | Fulfillment Service | |
| 8 | Observability | |
| 9 | Operations Dashboard | |
| 10 | Redundancy & Resilience | |
| 11 | Chaos & Failure Simulation | |
| 12 | Security | |
| 13 | Testing & Quality | |
| 14 | Production-like Local Infrastructure | |
| 15 | AWS | |
| 16 | Production Scenarios & Edge Cases | |

The reasoning behind each phase, and what has actually shipped, is in the
[roadmap](documentation/roadmap.md).

---

## Documentation

The repository separates **what Luna is**, **where it is going**, and **how each
phase is designed**.

- **[Getting Started](documentation/getting-started.md)** — run it locally
- [Roadmap](documentation/roadmap.md) — project vision and phase progression
- [Phase 0 Design](documentation/dev_phases/phase-0/design.md) — foundation and architectural decisions
- [Phase 1 Specification](documentation/dev_phases/phase-1/spec.md) — commerce flow requirements
- [Phase 1 Architecture](documentation/dev_phases/phase-1/architecture.md) — boundaries and decisions
- [Phase 1 Operations Architecture](documentation/dev_phases/phase-1/ops-architecture.md) — the fulfillment and shipment workflow
- [Phase 2 Specification](documentation/dev_phases/phase-2/spec.md) — messaging behaviour and acceptance criteria
- [Phase 2 Architecture](documentation/dev_phases/phase-2/architecture.md) — emulator choice, AWS mapping, resource ownership
- [Observability](documentation/observability.md) — OpenTelemetry, SigNoz, tracing a checkout

Frontend implementation detail lives in [`src/Frontend/`](src/Frontend/).

---

## License

MIT — see [LICENSE](LICENSE).
