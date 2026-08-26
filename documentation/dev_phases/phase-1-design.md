# Phase 1 — Basic Commerce Flow

## Scope

Phase 1 delivers the first complete customer commerce flow:

```text
Browse products
      ↓
    Cart
      ↓
   Checkout
      ↓
 Pending Order
      ↓
Inventory reservation
      ↓
Payment authorization
      ↓
Confirmed Order
      ↓
Fulfillment
      ↓
Shipment
      ↓
Delivered
```

Phase 1 focuses on a **working, deterministic happy path** with a small set of explicit failure outcomes.

Communication is synchronous HTTP. RabbitMQ, retries, dead-letter queues, transactional outbox processing, simulated delays, idempotency, compensation, and advanced recovery are later-phase concerns.

**Orders owns checkout orchestration** and coordinates Catalog, Shipping, Inventory, Payments, and the Fulfillment module.

---

## Quick Navigation

### Commerce Flow
1. [Phase 1 Assumptions](#1-phase-1-assumptions)
    - [Phase 1 Implementation Structure](#phase-1-implementation-structure)
2. [Order Lifecycle](#2-order-lifecycle)
3. [Checkout Workflow](#3-checkout-workflow)
4. [Failure Behavior](#4-failure-behavior)

### Data & Service Contracts
5. [Database Entity Model](#5-database-entity-model)
6. [Database Ownership](#6-database-ownership)
7. [Cross-Service References](#7-cross-service-references)
8. [Service API Contracts](#8-service-api-contracts)

### Frontend
9. [Frontend Architecture](#9-frontend-architecture)
10. [Frontend Routes](#10-frontend-routes)
11. [Catalog & Search](#11-catalog--search)
12. [Cart](#12-cart)
13. [Checkout UI](#13-checkout-ui)
14. [Authentication & Authorization](#14-authentication--authorization)
15. [Frontend State, Loading & Errors](#15-frontend-state-loading--errors)

### Correctness & Constraints
16. [Inventory Reservation](#16-inventory-reservation)
17. [Pricing & Snapshots](#17-pricing--snapshots)
18. [Payments](#18-payments)
19. [Shipping](#19-shipping)
20. [Fulfillment](#20-fulfillment)

### Scope Boundaries
21. [Intentionally Not Solved in Phase 1](#21-intentionally-not-solved-in-phase-1)
22. [Phase 1 Completion Criteria](#22-phase-1-completion-criteria)
23. [Confirmed Decisions](#23-confirmed-decisions)

---

# 1. Phase 1 Assumptions

- Identity, Catalog, Orders, Payments, Inventory, and Shipping are separate application boundaries.
- Each application owns its own database and data.
- Fulfillment is an Orders module in Phase 1 and can become a separate service in Phase 7.
- IDs are shared as opaque references between services; cross-database foreign keys are not used.
- Orders stores immutable snapshots of product prices and shipping costs at checkout.
- The cart does not lock product prices.
- Catalog owns the current product price until checkout.
- Shipping calculates the shipping quote at checkout; Orders stores the returned cost as a snapshot.
- Inventory reserves available stock without warehouse allocation or split-fulfillment logic.
- Users are stored in `IdentityDb` and receive roles such as `Customer` and `Admin`.
- A Phase 1 order has exactly one shipment.
- Customers cannot cancel orders.
- Payments supports authorization only. Capture, void, and refunds are later capabilities.
- Phase 1 uses synchronous HTTP between services.
- RabbitMQ is introduced in Phase 2; messaging complements HTTP rather than replacing it.

### Phase 1 Implementation Structure

Phase 1 must build inside the service boundaries established in Phase 0. Each backend service keeps the following structure:

```text
Service/
├── Controllers/
├── Application/
├── Domain/
├── Infrastructure/
└── Program.cs
```

This is a structural constraint, not a requirement to create a separate project for every folder or feature. Organize within these areas as the service grows, but do not introduce a second architectural style for individual services.

#### Layer responsibilities

**Controllers** contain HTTP concerns only:

- Route and endpoint declarations
- Request binding and basic transport validation
- Authentication and authorization metadata
- Mapping application results to HTTP status codes and response contracts

Controllers delegate business operations to application commands, queries, or handlers. They do not contain EF queries, transaction management, inventory calculations, payment decisions, checkout orchestration, or domain state transitions.

**Application** coordinates use cases. It receives commands or queries, calls domain behavior and infrastructure abstractions, coordinates transactions where required, and returns application results. Examples include creating or releasing an inventory reservation, authorizing a payment, calculating a shipping quote, and orchestrating checkout in Orders.

**Domain** contains service-owned business concepts and rules. Entities, value objects, status transitions, and invariants belong here. Domain code must not depend on ASP.NET Core controllers, HTTP request types, or another service's database.

**Infrastructure** contains persistence and external integration concerns. EF Core `DbContext`, entity configurations, migrations, seed data, HTTP clients, and service-specific adapters belong here. Infrastructure implements the interfaces required by the application layer; it does not become a place for unrelated business workflows.

#### Read repository projection

Phase 1 read repositories project database entities directly into application DTOs or read models. They do not materialize domain entities for the application handler to map afterward.

The standard read flow is:

```text
Database
   |
   v
EF Core projection
   |
   v
Application DTO/read model
   |
   v
Application handler
   |
   v
Controller
```

For example, Catalog read repositories return `ProductResponse` and `CategoryResponse` directly:

```text
IProductReadRepository
    |
    v
ProductReadRepository
    |
    v
ProductResponse
```

```text
ICategoryReadRepository
    |
    v
CategoryReadRepository
    |
    v
CategoryResponse
```

Repositories should express these projections as EF Core expressions so the database query selects only the fields required by the read model. Handlers remain responsible for application orchestration and return the repository result without repeating DTO construction or mapping logic.

This convention keeps read-only application flows from exposing domain entities beyond the repository boundary, avoids unnecessary materialization of full entities and related objects, and gives all services one consistent read pattern. Read DTOs belong to the Application layer; the Infrastructure repository owns the EF Core query and its projection.

Domain entities remain appropriate for write operations. A write use case may load domain state, execute business rules and state transitions, modify the entity, and persist the result.

The Phase 1 rule is:

> Reads project directly to application DTOs; writes use domain entities when domain behavior is required.

#### Database schema lifecycle

Each service evolves its production database through EF Core migrations stored under its Infrastructure database boundary:

```text
Infrastructure/
└── Database/
    ├── <Service>DbContext.cs
    └── Migrations/
        ├── <timestamp>_Initial<Service>.cs
        ├── <timestamp>_<SchemaChange>.cs
        └── <Service>DbContextModelSnapshot.cs
```

Service startup applies pending migrations with `MigrateAsync` before running separate baseline/application seed logic. `EnsureCreated` is not used for the application's real database. Migrations own schema evolution; seed code owns baseline data and may evolve independently from schema changes.

#### Scope of the structure

The structure applies to Catalog, Orders, Payments, Inventory, and Shipping. Fulfillment remains an application/domain module inside Orders in Phase 1 and does not receive a separate service or database.

Phase 1 intentionally does not require dozens of abstractions, separate projects for each layer, or a shared cross-service domain library. The goal is a clear boundary that keeps business logic testable and makes later asynchronous processing, retries, idempotency, and recovery possible without rewriting controllers.

The implementation rule is:

> Do not introduce business logic, entities, EF configuration, seed data, or application workflows directly into controllers or root-level service files. Follow the `Controllers` / `Application` / `Domain` / `Infrastructure` structure for every service, and keep controllers thin.

### Test Organization

Tests follow the same service boundary as production code while remaining centralized under `tests/`:

```text
tests/
├── Unit/
│   ├── Catalog/
│   ├── Orders/
│   ├── Inventory/
│   ├── Payments/
│   ├── Shipping/
│   └── Identity/
├── Integration/
│   ├── Catalog/
│   ├── Orders/
│   ├── Inventory/
│   ├── Payments/
│   ├── Shipping/
│   └── Identity/
└── Smoke/
    └── PhaseZeroSmokeTests.cs
```

Unit tests are organized by service and named after the use case or handler they exercise, such as `GetProductsHandlerTests` and `GetCategoriesHandlerTests`. They test application behavior without requiring SQL Server or other external services.

Integration tests are organized by service and exercise real service boundaries, persistence, or cross-service interactions as those capabilities are implemented. Smoke tests are separate from unit and integration tests and verify that the deployed or running system is basically alive.

The test projects remain centralized by test type. Phase 1 does not create a separate test project for every service; service folders provide ownership and navigation without multiplying project overhead.

### Coverage

Luna collects line and branch coverage from automated tests at the test-project level. Coverlet produces Cobertura reports for the unit test project, and CI publishes those reports as build artifacts so coverage can be inspected over time. Generated EF Core migration source files are excluded through `tests/coverage.runsettings`; application infrastructure such as repositories, controllers, middleware, and database startup remains included.

The same workflow is available locally through `scripts/test-coverage.sh`:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
./scripts/test-coverage.sh
```

The script runs the unit tests, collects Cobertura coverage, generates `coverage-report/index.html`, and opens the report. The global tool installation is needed once per development machine.

Coverage is a quality signal and regression indicator, not a standalone quality target. Tests should prioritize meaningful business behavior and decision branches over achieving an arbitrary percentage. For example, Catalog repository coverage should exercise active-product filtering, search and category branches, ordering, DTO projection, image ordering, single-product lookup, and not-found behavior. Thin handlers should be tested for orchestration and parameter forwarding rather than inflated with assertions that duplicate repository behavior.

Repository query behavior belongs in integration tests against real SQL Server or a SQL Server test container when the required fixture is available. EF Core translation, collation, relationships, ordering, and database behavior should not be approximated with mocks. Phase 1 does not enforce a minimum coverage threshold until the meaningful test surface and baseline coverage are established.

The SQL Server integration-test fixture starts one disposable SQL Server container and applies the Catalog migrations once for the shared test database. Each test resets that database before arranging its data through a reliable reset tool such as Respawn:

```text
Shared fixture:
Start container -> Apply migrations -> Reset data -> Run each test
```

**Shared integration-test databases are serialized.**

Tests sharing a database fixture must disable parallelization for that collection because each test resets the shared database before execution. This prevents one test from resetting database state while another test is running.

---

# 2. Order Lifecycle

Orders owns the order lifecycle.

```mermaid
stateDiagram-v2
    [*] --> Pending

    Pending --> Confirmed: Payment authorized
    Pending --> PaymentFailed: Payment rejected
    Pending --> Cancelled: Checkout abandoned/invalidated

    Confirmed --> Preparing: Fulfillment prepared
    Preparing --> Shipped: Shipment created
    Preparing --> ShippingPendingRetry: Shipment creation failed
    ShippingPendingRetry --> Shipped: Future retry/recovery

    Shipped --> Delivered: Delivery recorded

    PaymentFailed --> [*]
    Cancelled --> [*]
    Delivered --> [*]
```

### State meanings

| State | Meaning |
|---|---|
| `Pending` | Order exists and checkout is being completed. |
| `Confirmed` | Inventory is reserved and payment has been authorized. |
| `PaymentFailed` | Payment authorization failed; reservation has been released. |
| `Preparing` | Fulfillment has accepted/prepared the order. |
| `ShippingPendingRetry` | Shipment creation failed; shipment creation requires future recovery/retry. |
| `Shipped` | Shipment exists and the order has shipped. |
| `Delivered` | Delivery has been recorded. |
| `Cancelled` | Order was invalidated/abandoned before confirmation. |

### Transition ownership

| Transition | Owner | Trigger |
|---|---|---|
| → `Pending` | Orders | Checkout creates the order |
| `Pending` → `Confirmed` | Orders | Payment authorization succeeds |
| `Pending` → `PaymentFailed` | Orders | Payment authorization fails |
| `Pending` → `Cancelled` | Orders | Checkout cannot proceed and the order is invalidated |
| `Confirmed` → `Preparing` | Orders / Fulfillment module | Fulfillment prepares the order |
| `Preparing` → `Shipped` | Orders / Shipping | Shipment is successfully created |
| `Preparing` → `ShippingPendingRetry` | Orders | Shipment creation fails |
| `ShippingPendingRetry` → `Shipped` | Orders / Shipping | Later recovery successfully creates shipment |
| `Shipped` → `Delivered` | Orders / Shipping | Delivery is recorded |

### Why `ShippingPendingRetry`?

`ShippingPending` sounds like a normal intermediate state in the happy path. The Phase 1 state specifically means:

> **Shipment creation failed and the order is waiting for a future retry/recovery mechanism.**

Therefore `ShippingPendingRetry` is more explicit.

It is still a temporary state. Automatic retry does **not** exist in Phase 1; later phases implement the recovery mechanism.

---

# 3. Checkout Workflow

## Happy path

```mermaid
sequenceDiagram
    participant C as Customer
    participant N as Next.js
    participant O as Orders
    participant Ca as Catalog
    participant S as Shipping
    participant I as Inventory
    participant P as Payments
    participant F as Fulfillment

    C->>N: Submit checkout
    N->>O: POST /checkout

    O->>Ca: Validate products + current prices
    Ca-->>O: Product data

    O->>S: Calculate shipping quote
    S-->>O: Quote

    O->>O: Create Pending order + snapshots

    O->>I: Reserve stock
    I-->>O: Reservation confirmed

    O->>P: Authorize payment
    P-->>O: Authorization confirmed

    O->>F: Prepare order
    F-->>O: Prepared

    O->>S: Create shipment
    S-->>O: Shipment created

    O-->>N: Confirmed order
    N-->>C: Checkout result
```

The order is not `Confirmed` until:

1. Products/prices have been validated.
2. Shipping has been quoted.
3. Inventory has been reserved.
4. Payment has been authorized.

Fulfillment and shipment creation happen after confirmation.

## Checkout sequence

```text
1. Customer submits checkout.
2. Orders validates product identity, active status, and current price through Catalog.
3. Orders requests the shipping quote from Shipping.
4. Orders creates the Pending order and stores immutable snapshots.
5. Orders requests Inventory to reserve stock.
6. If inventory succeeds, Orders requests Payments to authorize the order amount.
7. If payment succeeds, Orders marks the order Confirmed.
8. Fulfillment prepares the order.
9. Shipping creates the shipment.
10. If shipment creation fails, the order enters ShippingPendingRetry.
```

---

# 4. Failure Behavior

Phase 1 deliberately implements only a small set of deterministic failure outcomes.

### Inventory unavailable

```text
Checkout
   ↓
Pending order
   ↓
Inventory reservation fails
   ↓
No payment authorization
   ↓
Order invalidated
```

Payment is never called when inventory reservation fails.

### Payment failure

```text
Pending
   ↓
Inventory reserved
   ↓
Payment rejected
   ↓
PaymentFailed
   ↓
Inventory reservation released
```

No automatic payment retry is attempted.

### Shipment creation failure

```text
Confirmed
   ↓
Preparing
   ↓
Shipment creation fails
   ↓
ShippingPendingRetry
```

No automatic retry exists in Phase 1.

The order remains visible in this recovery-required state until a later phase implements retry/recovery.

### Important limitation

Phase 1 does not solve every distributed failure window. For example:

- Payment can succeed while order completion subsequently fails.
- A downstream service can become unavailable during checkout.
- A request can time out after a downstream operation succeeded.
- A service can crash after changing its database but before returning a response.

These are intentionally deferred to later phases.

---

# 5. Database Entity Model

Each service owns an independent database. Same-database relationships are shown in the diagrams. Relationships that cross a database boundary are references, not database-enforced foreign keys, and are listed separately below.

## IdentityDb

```mermaid
erDiagram
    ASP_NET_IDENTITY_TABLES {
        string asp_net_users
        string asp_net_roles
        string asp_net_user_roles
        string additional_identity_tables
    }
```

`IdentityDb` uses the schema provided by ASP.NET Core Identity, including `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, and its other framework tables. Phase 1 does not add a custom customer profile.

## CatalogDb

```mermaid
erDiagram
    CATEGORY {
        uuid id PK
        string name UK
    }

    PRODUCT {
        uuid id PK
        string sku UK
        uuid category_id FK
        string name
        string description
        decimal current_price
        boolean is_active
        datetime created_at
        datetime updated_at
    }

    PRODUCT_IMAGE {
        uuid id PK
        uuid product_id FK
        string image_url
        string alt_text
        int display_order
    }

    CATEGORY ||--o{ PRODUCT : contains
    PRODUCT ||--|{ PRODUCT_IMAGE : has
```

## OrdersDb

```mermaid
erDiagram
    CART {
        uuid id PK
        uuid user_id
        string status
        string currency
        datetime created_at
        datetime updated_at
    }

    CART_ITEM {
        uuid id PK
        uuid cart_id FK
        uuid product_id
        int quantity
    }

    ORDER {
        uuid id PK
        string order_number UK
        uuid user_id
        string status
        decimal subtotal
        string shipping_method_code
        string shipping_method_name
        decimal shipping_cost
        decimal total
        datetime created_at
    }

    ORDER_ITEM {
        uuid id PK
        uuid order_id FK
        uuid product_id
        string sku_snapshot
        string product_name_snapshot
        decimal unit_price_snapshot
        int quantity
        decimal line_total
    }

    SHIPPING_ADDRESS {
        uuid id PK
        uuid order_id FK
        string recipient_name
        string street
        string city
        string state
        string postal_code
        string country
    }

    CART ||--|{ CART_ITEM : contains
    ORDER ||--|{ ORDER_ITEM : contains
    ORDER ||--|| SHIPPING_ADDRESS : uses
```

## PaymentsDb

```mermaid
erDiagram
    PAYMENT {
        uuid id PK
        uuid order_id
        string status
        decimal amount
        string currency
        datetime created_at
    }

    PAYMENT_ATTEMPT {
        uuid id PK
        uuid payment_id FK
        string status
        string provider_reference
        string failure_reason
        datetime attempted_at
    }

    PAYMENT ||--|{ PAYMENT_ATTEMPT : has
```

## InventoryDb

```mermaid
erDiagram
    WAREHOUSE {
        uuid id PK
        string name
        string location
    }

    STOCK {
        uuid id PK
        uuid warehouse_id FK
        uuid product_id
        int quantity_on_hand
        int quantity_reserved
    }

    INVENTORY_RESERVATION {
        uuid id PK
        uuid order_id
        uuid product_id
        uuid warehouse_id
        int quantity
        string status
    }

    WAREHOUSE ||--o{ STOCK : stores
    WAREHOUSE ||--o{ INVENTORY_RESERVATION : fulfills
    STOCK ||--o{ INVENTORY_RESERVATION : reserves
```

`Stock` has a unique constraint on `(warehouse_id, product_id)`, allowing only one stock record per warehouse and product.

## ShippingDb

```mermaid
erDiagram
    SHIPPING_METHOD {
        uuid id PK
        string code UK
        string name
        decimal base_cost
        int estimated_delivery_days
        boolean is_active
    }

    SHIPPING_QUOTE {
        uuid id PK
        string method_code
        decimal cost
        int estimated_delivery_days
        datetime created_at
    }

    SHIPMENT {
        uuid id PK
        uuid order_id
        string status
        string tracking_number
        datetime created_at
        datetime shipped_at
        datetime delivered_at
    }

    TRACKING_EVENT {
        uuid id PK
        uuid shipment_id FK
        string status
        string location
        datetime occurred_at
    }

    SHIPPING_METHOD ||--o{ SHIPPING_QUOTE : offers
    SHIPMENT ||--o{ TRACKING_EVENT : records
```

---

# 6. Database Ownership

| Database | Entities | Ownership |
|---|---|---|
| `IdentityDb` | ASP.NET Identity tables | User accounts, roles, authentication |
| `CatalogDb` | Category, Product, ProductImage | Product information and current prices |
| `OrdersDb` | Cart, CartItem, Order, OrderItem, ShippingAddress | Cart, order lifecycle, checkout snapshots |
| `PaymentsDb` | Payment, PaymentAttempt | Payment state and authorization attempts |
| `InventoryDb` | Warehouse, Stock, InventoryReservation | Stock and reservations |
| `ShippingDb` | ShippingMethod, ShippingQuote, Shipment, TrackingEvent | Shipping options, quotes, shipments, tracking |

Fulfillment remains a module inside Orders and has no separate database.

---

# 7. Cross-Service References

These are exchanged through APIs and stored as IDs. They are not cross-database foreign keys.

```text
CartItem.product_id
    → CatalogDb.Product.id

Cart.user_id
    → IdentityDb.AspNetUsers.Id

Order.user_id
    → IdentityDb.AspNetUsers.Id

OrderItem.product_id
    → CatalogDb.Product.id

Payment.order_id
    → OrdersDb.Order.id

InventoryReservation.order_id
    → OrdersDb.Order.id

InventoryReservation.product_id
    → CatalogDb.Product.id

Shipment.order_id
    → OrdersDb.Order.id
```

No service queries another service's database.

---

# 8. Service API Contracts

Phase 1 APIs are RESTful ASP.NET Core Controller APIs with OpenAPI documentation.

The detailed field-level contract is maintained in each service's OpenAPI document. This section defines the operations and ownership that the implementation must provide.

## Catalog

```text
GET /api/v1/catalog/products
GET /api/v1/catalog/products/{id}
GET /api/v1/catalog/categories
```

Responsibilities:

- Browse active products
- Search products by name
- Filter by category
- Return current product prices
- Return product images
- Validate product existence, active status, and current price for checkout

Catalog is exposed under the `/api/v1/catalog` service boundary. Resource-specific routes are defined by the controller actions so future Catalog resources can be added without changing the service prefix.

### API errors

All services return the same error shape:

```json
{
    "code": "PRODUCT_NOT_FOUND",
    "message": "The product could not be found."
}
```

`ApiError` is defined locally in each service with this same shape. Phase 1 does not introduce a shared C# contracts or common library solely for errors. Each service owns its error definitions and stable error codes, such as Catalog's `PRODUCT_NOT_FOUND` and `CATEGORY_NOT_FOUND`.

The error code is the stable client-facing identifier. The message is human-readable and may change without changing the meaning of the error.

## Orders

```text
GET    /api/v1/cart
POST   /api/v1/cart/items
PATCH  /api/v1/cart/items/{id}
DELETE /api/v1/cart/items/{id}

POST /api/v1/checkout

GET /api/v1/orders
GET /api/v1/orders/{id}
```

Responsibilities:

- Manage the customer's cart
- Enrich cart responses with product information
- Orchestrate checkout
- Create orders and snapshots
- Expose order history and status

## Inventory

Internal operations support:

```text
POST /api/v1/reservations
POST /api/v1/reservations/{id}/release
```

Reservation requests contain the order ID and requested product quantities.

## Payments

Internal operation:

```text
POST /api/v1/payments/authorize
```

Phase 1 supports authorization only.

## Shipping

Operations include:

```text
GET  /api/v1/shipping-methods
POST /api/v1/quotes
POST /api/v1/shipments
```

## Identity

Phase 0 framework Identity endpoints remain the authentication foundation. Phase 1 introduces the OpenIddict/OIDC flow described in the authentication section.

### Contract rules

Every endpoint defines:

- Request contract
- Response contract
- Authentication/authorization requirement
- Expected HTTP status codes
- Expected business error codes

Controllers contain HTTP concerns only and delegate business operations to application commands/queries.

---

# 9. Frontend Architecture

The customer application uses:

```text
Next.js
React
TypeScript
TanStack Query
Axios
```

The browser communicates with the Next.js thin gateway using same-origin routes.

```text
Browser
   │
   ▼
Next.js
Thin Gateway
   │
   ├── Catalog API
   ├── Orders API
   ├── Identity / OIDC
   └── other private APIs
```

The browser does not directly address Docker service names or internal service URLs.

### Gateway boundary

Next.js:

- Proxies requests to private backend services.
- Provides the public HTTP boundary.
- Handles the browser/session boundary for authentication.
- Does not contain commerce business logic.
- Does not duplicate service DTOs by default.
- Does not orchestrate checkout.

A full BFF is not introduced in Phase 1.

---

# 10. Frontend Routes

Phase 1 customer routes:

```text
/shop
/shop/products/[productId]
/cart
/checkout
/orders
/orders/[orderId]
/account
/login
```

Authentication requirements:

| Route | Auth |
|---|---|
| `/shop` | Anonymous |
| `/shop/products/[productId]` | Anonymous |
| `/cart` | Login required in Phase 1 |
| `/checkout` | Login required |
| `/orders` | Login required |
| `/orders/[orderId]` | Login required |
| `/account` | Login required |
| `/login` | Anonymous |

Anonymous shopping and anonymous carts are outside Phase 1.

---

# 11. Catalog & Search

### Product behavior

- Products have one or more images.
- Products belong to a category.
- Products do not have ratings or reviews in Phase 1.
- Search matches product name.
- Category filtering is supported.
- Product availability is displayed from inventory quantity:
  - `0` → `Unavailable`
  - `> 0` → `Available`
- Customers can choose quantity when multiple units are available.
- Insufficient inventory is ultimately enforced at checkout.
- Phase 1 uses simple image URLs; storage/CDN infrastructure is deferred.

### URL state

Search and filtering are represented in the URL:

```text
/shop?search=keyboard&category=electronics
```

This supports refresh, browser navigation, and shareable storefront state.

### Availability vs reservation

The frontend's availability display is informational.

```text
Catalog / Inventory availability
            ↓
      Can I show/browse it?

Checkout reservation
            ↓
   Can I fulfill this quantity?
```

The final correctness check occurs in Inventory during checkout.

---

# 12. Cart

- The Orders API returns an enriched cart.
- The frontend does not call Catalog separately for every cart item.
- Cart items contain product references and quantities, not authoritative product prices.
- Cart supports quantity changes and item removal.
- Cart prices are derived from current Catalog data.
- Cart does not reserve inventory.
- Cart does not lock prices.
- The frontend prevents obvious invalid quantities, but Orders validates the request server-side.
- An unavailable product may remain in a cart; checkout is the final inventory check.

### Price change example

```text
Add product
   ↓
Cart contains product reference
   ↓
Catalog price changes
   ↓
Cart displays current Catalog price
   ↓
Checkout
   ↓
Current price is validated and snapshotted
```

---

# 13. Checkout UI

Checkout is a single page with:

1. Shipping information
2. Shipping method
3. Payment
4. Order summary

Phase 1 uses:

- One simulated payment method.
- Two fixed, always-available shipping options.
- No saved addresses.
- Customer-entered shipping address.
- Subtotal, shipping cost, and total displayed separately.

The UI disables repeated submission while checkout is in progress.

The backend remains authoritative; frontend state cannot finalize an order.

---

# 14. Authentication & Authorization

Phase 1 requires login.

### Target flow

```mermaid
sequenceDiagram
    participant B as Browser
    participant N as Next.js
    participant I as Identity / OpenIddict
    participant O as Backend API

    B->>N: Login
    N->>I: OIDC authorization
    I-->>N: Authorization result / tokens
    N-->>B: Secure session cookie

    B->>N: API request
    N->>O: Authorization: Bearer <access-token>
    O-->>N: API response
    N-->>B: Response
```

The target design keeps backend access tokens out of browser JavaScript by default.

### Responsibilities

**ASP.NET Core Identity**

- Users
- Passwords
- Roles
- Claims
- Account lifecycle

**OpenIddict**

- OAuth 2.0 / OpenID Connect authorization
- Token issuance
- OIDC client integration

**Next.js**

- Browser-facing session boundary
- OIDC client
- Server-side access-token handling
- Propagation of access tokens to private APIs

**Backend APIs**

- Validate access tokens
- Enforce roles/policies
- Never issue user access tokens

### Roles

Phase 1 defines:

- `Customer`
- `Admin`

The Admin role exists for authorization testing and identity foundations. A dedicated operations/admin UI is later-phase work.

---

# 15. Frontend State, Loading & Errors

### Server state

TanStack Query manages:

- Fetching
- Caching
- Refetching
- Mutations
- Loading state
- Error state

Axios is the initial transport underneath centralized typed API clients.

API calls are not scattered directly through UI components.

After mutations, affected queries are invalidated or updated.

### UI state

Local component/feature state handles transient UI concerns such as:

- Form fields
- Modal state
- Selection
- UI-only toggles

Frontend state is never authoritative for backend business data.

### Loading behavior

- Product/catalog loading uses a loading or skeleton state.
- Mutating controls are disabled while requests are pending.
- Checkout cannot be submitted twice through the UI.

### Error behavior

- Backend errors are converted to customer-friendly messages.
- Raw exceptions and internal implementation details are never shown.
- Checkout errors communicate whether the failure occurred during validation, inventory reservation, payment, or shipping.

---

# 16. Inventory Reservation

Inventory correctness is a Phase 1 requirement even though distributed locking is not.

Available quantity is:

```text
available = quantity_on_hand - quantity_reserved
```

Reservation must atomically verify:

```text
available >= requested_quantity
```

and then update the reservation/stock state within an InventoryDb transaction.

### Concurrent checkout

```text
                Stock = 1
                   │
          ┌────────┴────────┐
          ▼                 ▼
     Customer A        Customer B
       reserve             reserve
          │                 │
          └──────┬──────────┘
                 ▼
       InventoryDb transaction
                 │
          only one succeeds
```

The implementation must prevent two concurrent reservations from consuming the same available unit.

Distributed locks are not used.

### Release

When payment authorization fails:

```text
Reservation
     ↓
Release
     ↓
quantity_reserved decreases
```

Reservation release is owned by Inventory.

---

# 17. Pricing & Snapshots

Catalog owns the current selling price.

Orders owns historical checkout snapshots.

### Cart

The cart stores:

```text
product_id
quantity
```

It does not store an authoritative unit price.

The current Catalog price is displayed.

### Checkout

Orders validates the current Catalog price and stores:

```text
sku_snapshot
product_name_snapshot
unit_price_snapshot
quantity
line_total
```

Shipping is handled the same way:

```text
Shipping quote
      ↓
Orders
      ↓
shipping_method_code
shipping_method_name
shipping_cost
```

Once the order is created, these values are immutable historical snapshots.

---

# 18. Payments

Phase 1 supports **authorization only**.

```text
Orders
   │
   │ authorize amount
   ▼
Payments
   │
   ▼
Fake Payment Provider
   │
   ├── Success
   └── Failure
```

The fake provider is deterministic rather than random.

The implementation must provide a predictable way to simulate successful and failed authorization scenarios.

A successful authorization causes Orders to mark the order `Confirmed`.

A failed authorization causes:

```text
PaymentFailed
     +
Inventory reservation released
```

Not included:

- Capture
- Void
- Refund
- Real provider integration
- Automatic payment retry

---

# 19. Shipping

Phase 1 defines two fixed, always-available shipping methods.

Each method has a stable code, display name, price, and estimated delivery time.

Example:

```text
STANDARD
EXPRESS
```

The exact prices and delivery-day values are implementation configuration, not frontend logic.

### Quote

```text
Orders
   ↓
Shipping
   ↓
Shipping Quote
   ↓
Orders snapshots cost
```

### Shipment

Shipment creation occurs after fulfillment preparation.

```text
Confirmed
   ↓
Preparing
   ↓
Create shipment
   ├── success → Shipped
   └── failure → ShippingPendingRetry
```

Phase 1 does not automatically retry shipment creation.

---

# 20. Fulfillment

Fulfillment remains a module inside Orders.

Phase 1 fulfillment is intentionally simple:

```text
Confirmed
    ↓
Fulfillment.Prepare(order)
    ↓
Preparing
```

It does not implement:

- Warehouse selection
- Picking optimization
- Packing workflow
- Random delays
- Background workers
- Separate database
- Separate service

The module prepares the order and then requests shipment creation through Shipping.

Fulfillment can become a separate service in Phase 7.

---

# 21. Intentionally Not Solved in Phase 1

Phase 1 does not attempt to solve complete distributed consistency or recovery.

Deferred:

- RabbitMQ and asynchronous consumers
- Transactional Outbox
- Automatic retries
- Dead-letter queues
- Idempotency
- Timeout/retry policies
- Compensation/Sagas
- Failure injection
- Simulated processing delays
- Distributed tracing and full metrics
- Multiple shipments per order
- Multi-warehouse allocation
- Split fulfillment
- Customer cancellation
- Payment capture, void, and refunds
- Real payment providers
- Anonymous shopping/cart
- Saved addresses
- Product reviews/ratings
- Image storage/CDN
- Operations dashboard
- Dedicated Fulfillment service

Known failure windows remain intentionally observable and unresolved until later phases.

---

# 22. Phase 1 Completion Criteria

Phase 1 is complete when:

### Commerce

- [ ] Customer can browse active products.
- [ ] Customer can search by product name and filter by category.
- [ ] Customer can view product details and images.
- [ ] Customer can add products to a cart.
- [ ] Customer can update quantities and remove cart items.
- [ ] Cart displays current Catalog prices.
- [ ] Checkout validates current products/prices.
- [ ] Checkout calculates a Shipping quote.
- [ ] Checkout creates immutable product-price and shipping-cost snapshots.
- [ ] Inventory reservation is concurrency-safe.
- [ ] Inventory failure prevents payment authorization.
- [ ] Payment authorization is deterministic and testable.
- [ ] Payment failure releases inventory and produces `PaymentFailed`.
- [ ] Successful payment produces `Confirmed`.
- [ ] Fulfillment produces `Preparing`.
- [ ] Successful shipment creation produces `Shipped`.
- [ ] Shipment failure produces `ShippingPendingRetry`.
- [ ] Shipment/delivery can reach `Delivered`.
- [ ] Customer can view order history and current order status.

### Services

- [ ] Service APIs are documented through OpenAPI.
- [ ] Orders owns checkout orchestration.
- [ ] No service accesses another service's database.
- [ ] Cross-service references use IDs.
- [ ] Controllers contain HTTP concerns only.
- [ ] Each backend service follows the `Controllers` / `Application` / `Domain` / `Infrastructure` structure.
- [ ] Business workflows, domain rules, persistence, and seed data are kept out of controllers and root-level service files.
- [ ] Application database schemas are created and evolved through EF Core migrations.
- [ ] Service startup applies pending migrations before running separate seed logic.
- [ ] `EnsureCreated` is not used for the application's real database.
- [ ] Tests are organized by test type and service under the centralized `tests/` structure.
- [ ] Unit tests are named after the handler or use case they exercise.
- [ ] Smoke tests are separate from unit and integration tests.
- [ ] Backend services remain private behind the Next.js gateway.

### Authentication

- [ ] Customer login is required for Phase 1 commerce operations.
- [ ] Customer and Admin roles exist.
- [ ] Phase 1 implements the agreed OpenIddict/OIDC integration.
- [ ] Next.js manages the browser session boundary.
- [ ] Backend APIs validate access tokens and enforce authorization.

### Frontend

- [ ] Routes for shop, product details, cart, checkout, orders, account, and login exist.
- [ ] API access uses centralized typed clients.
- [ ] TanStack Query manages server state.
- [ ] Loading and mutation states are handled.
- [ ] Backend errors are translated into customer-friendly messages.
- [ ] Checkout cannot be double-submitted through the UI.

---

# 23. Confirmed Decisions

1. **Orders owns checkout orchestration.**
2. **Phase 1 uses synchronous HTTP.**
3. **RabbitMQ begins in Phase 2 and complements HTTP.**
4. **Fulfillment remains an Orders module until Phase 7.**
5. **Each service owns its database and domain.**
6. **No cross-database foreign keys or database queries are used.**
7. **Product prices are owned by Catalog until checkout.**
8. **Cart items do not lock or snapshot prices.**
9. **Orders snapshots product and shipping prices at checkout.**
10. **Shipping owns shipping quotes and methods.**
11. **Inventory owns stock and reservations.**
12. **Inventory reservation is concurrency-safe using database transactions/atomic updates; no distributed locks are used.**
13. **Inventory failure prevents payment authorization.**
14. **Payment failure produces `PaymentFailed` and releases the reservation.**
15. **Payments supports authorization only.**
16. **The fake payment provider is deterministic and testable.**
17. **Phase 1 uses two fixed, always-available shipping methods with stable codes.**
18. **Shipment creation occurs after fulfillment preparation.**
19. **Shipment creation failure produces `ShippingPendingRetry`.**
20. **`ShippingPendingRetry` means recovery/retry is required; automatic retry is not implemented in Phase 1.**
21. **A Phase 1 order has exactly one shipment.**
22. **Customer cancellation is not supported.**
23. **Customer and Admin roles exist; no dedicated Admin UI is implemented.**
24. **Next.js is the public HTTP boundary and thin gateway.**
25. **Backend services are private network services.**
26. **A full BFF with frontend-specific DTOs/aggregation is not introduced unless a concrete need appears later.**
27. **The browser does not directly call internal service URLs.**
28. **Next.js is the target OIDC/session boundary.**
29. **Backend access tokens are kept out of browser JavaScript by default.**
30. **Backend APIs validate access tokens and enforce authorization.**
31. **TanStack Query manages frontend server state.**
32. **Axios is used through centralized typed API clients.**
33. **Frontend routes and search/filter state are URL-addressable where appropriate.**
34. **Frontend state is not authoritative for business data.**
35. **Phase 1 deliberately does not solve retries, idempotency, outbox, messaging, compensation, or distributed recovery.**
36. **Every backend service follows the `Controllers` / `Application` / `Domain` / `Infrastructure` structure; controllers remain thin and delegate business operations to the application layer.**
37. **Phase 1 favors clear layer boundaries without requiring separate projects or excessive abstractions.**
38. **Tests are organized centrally by test type and service boundary; Phase 1 does not create a test project for every service.**
39. **Unit tests are named for their handler or use case, integration tests cover service boundaries, and smoke tests verify basic system health separately.**
40. **Each service uses EF Core migrations for application database schema evolution; startup applies pending migrations before separate seed logic runs.**
41. **`EnsureCreated` is not used for an application's real database.**
42. **Read repositories project directly to Application DTOs/read models; write operations use Domain entities when domain behavior is required.**
43. **Luna collects line and branch coverage in CI with Coverlet; coverage guides regression detection and test priorities but does not have an arbitrary minimum threshold in Phase 1.**
44. **Phase 1 integration fixtures favor isolated databases and straightforward setup; shared databases with reliable reset are a later optimization if test-suite growth makes setup cost significant.**
