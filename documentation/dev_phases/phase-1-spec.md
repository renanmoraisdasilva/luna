# Phase 1 - Basic Commerce Flow Specification

## Contents

- [Purpose](#purpose)
- [1. Scope](#1-scope)
- [2. Business Constraints](#2-business-constraints)
- [3. Catalog](#3-catalog)
- [4. Cart](#4-cart)
- [5. Authentication](#5-authentication)
- [6. Checkout](#6-checkout)
- [7. Order Lifecycle](#7-order-lifecycle)
- [8. Inventory](#8-inventory)
- [9. Pricing](#9-pricing)
- [10. Shipping](#10-shipping)
- [11. Payments](#11-payments)
- [12. Fulfillment](#12-fulfillment)
- [13. Frontend Behavior](#13-frontend-behavior)
- [14. Service API Requirements](#14-service-api-requirements)
- [15. Failure Model](#15-failure-model)
- [16. Explicitly Out of Scope](#16-explicitly-out-of-scope)
- [17. Acceptance Criteria](#17-acceptance-criteria)
- [18. Testing](#18-testing)
- [23. Implementation Checklist](#23-implementation-checklist)

## Purpose

Phase 1 delivers Luna's first complete customer commerce flow:

Browse -> Cart -> Checkout -> Order -> Inventory -> Payment -> Fulfillment -> Shipment -> Delivery

The goal is to prove that the service boundaries established in Phase 0 can support a complete commerce workflow.

Phase 1 prioritizes:

- a deterministic happy path
- explicit business rules
- clear service ownership
- correct inventory reservation
- immutable order pricing
- basic authentication
- a usable customer frontend
- a small number of explicit failure outcomes

Phase 1 does **not** attempt to solve distributed reliability.

## Implementation status

**Substantially implemented; closure pending cross-service proof as of 2026-09-26.** The repository contains the required catalog, cart, authentication, checkout, order history/detail, inventory, payment authorization, fulfillment, shipping, Luna Ops, and customer frontend surfaces. Checkout also has customer-scoped idempotency for repeated requests using the same `Idempotency-Key`, and frontend checkout error-state coverage is present. The current checkout integration test verifies Orders orchestration with HTTP boundary handlers; a complete workflow across real service persistence boundaries, customer shipment tracking, and the fulfillment/shipment lifecycle integration tests remain outstanding.

---

# 1. Scope

The customer must be able to:

1. Browse products.
2. Search and filter products.
3. View product details.
4. Add products to a cart.
5. Modify and remove cart items.
6. Log in.
7. Enter checkout information.
8. Select a shipping method.
9. Submit an order.
10. Reserve inventory.
11. Authorize payment.
12. Confirm the order.
13. Fulfill the order.
14. Create a shipment.
15. View the resulting order and shipment state.

The primary happy path is:

```text
Browse products
      v
    Cart
      v
   Checkout
      v
 Pending Order
      v
Inventory reservation
      v
Payment authorization
      v
Confirmed Order
      v
Fulfillment
      v
Shipment
      v
Delivered
````

---

# 2. Business Constraints

Phase 1 behavior is intentionally limited to:

- synchronous commerce operations
- simulated payment authorization
- a small fixed set of shipping methods
- one shipment per order
- no customer cancellation
- no anonymous cart or checkout

Distributed reliability concerns such as messaging, retries, outbox processing, compensation after timeouts, and distributed recovery are outside this phase. Request-level checkout idempotency is implemented as a Phase 1 safety feature; distributed idempotency and recovery remain future work.

# 3. Catalog

## SPEC-CAT-001 - Browse products

**Given** active products exist

**When** the customer opens the shop

**Then** active products are displayed.

Each product may contain:

* ID
* SKU
* name
* description
* current price
* category
* images
* availability

Inactive products must not appear in the customer catalog.

---

## SPEC-CAT-002 - Search products

**Given** the customer enters a search term

**When** the shop is requested with that term

**Then** products matching the product name are returned.

Search state must be represented in the URL.

Example:

```text
/shop?search=keyboard
```

---

## SPEC-CAT-003 - Filter by category

**Given** the customer selects a category

**When** the shop is requested with that category

**Then** only products belonging to that category are returned.

Example:

```text
/shop?category=electronics
```

---

## SPEC-CAT-004 - Product details

**Given** an active product exists

**When** the customer opens the product

**Then** the product details page displays:

* name
* description
* current price
* images
* category
* availability

---
# 4. Cart

## SPEC-CART-001 - Add item

**Given** an authenticated customer

**When** they add a product to the cart

**Then** the cart contains the product reference and requested quantity.

The cart does not reserve inventory.

---

## SPEC-CART-002 - Change quantity

**Given** an item exists in the cart

**When** the customer changes its quantity

**Then** the cart reflects the new quantity.

Quantity must be greater than zero.

---

## SPEC-CART-003 - Remove item

**Given** an item exists in the cart

**When** the customer removes it

**Then** the item is no longer present.

---

## SPEC-CART-004 - Current pricing

The cart does not own authoritative product pricing.

**Given** a product exists in the cart

**When** the cart is displayed

**Then** its displayed price comes from current Catalog data.

The cart does not lock the product price.

---

## SPEC-CART-005 - Inventory is not reserved

Adding an item to a cart must not reserve inventory.

Inventory is reserved only during checkout.

---
# 5. Authentication

## SPEC-AUTH-001 - Customer authentication

Customers must authenticate before accessing:

* cart
* checkout
* orders
* account

The shop and product pages remain publicly accessible.

---

## SPEC-AUTH-002 - Roles

Phase 1 supports:

```text
Customer
Admin
```

The Admin role exists for authorization, identity testing, and access to the authenticated Operations interface defined below. A general-purpose administration UI remains out of scope.

---
# 6. Checkout

## SPEC-CHK-001 - Checkout requires authentication

**Given** an unauthenticated customer

**When** they attempt checkout

**Then** checkout is rejected and authentication is required.

---

## SPEC-CHK-002 - Validate products

**Given** a checkout request

**When** Orders begins checkout

**Then** Orders validates each product against Catalog.

The validation must establish that:

* the product exists
* the product is active
* the current price is available

---

## SPEC-CHK-003 - Calculate shipping

**Given** a valid checkout request

**When** Orders requires a shipping quote

**Then** Orders requests the quote from Shipping.

Orders does not calculate shipping prices itself.

---

## SPEC-CHK-004 - Create pending order

**Given** product and shipping information has been validated

**When** checkout proceeds

**Then** Orders creates a `Pending` order.

The order stores immutable snapshots of:

* product SKU
* product name
* unit price
* quantity
* line total
* shipping method
* shipping cost

---

## SPEC-CHK-005 - Reserve inventory

**Given** a Pending order

**When** inventory reservation is requested

**Then** Orders asks Inventory to reserve the requested quantities.

Payment authorization must not happen before successful inventory reservation.

---

## SPEC-CHK-006 - Authorize payment

**Given** inventory reservation succeeds

**When** Orders requests payment authorization

**Then** Payments attempts to authorize the order total.

---

## SPEC-CHK-007 - Confirm order

**Given** inventory reservation succeeds

**And** payment authorization succeeds

**When** checkout completes

**Then** the order becomes `Confirmed`.

---
# 7. Order Lifecycle

The supported lifecycle is:

```text
Pending
   |
   +---- payment failure ---> PaymentFailed
   |
   +---- invalidated -------> Cancelled
   |
   +---- payment success --> Confirmed
                              |
                              v
                          Preparing
                              |
                     ----------------
                     |                 |
                     v                 v
                  Shipped       ShippingPendingRetry
                     |                 |
                     |                 |
                     v                 |
                 Delivered             |
                                       |
                              future recovery
                                       |
                                       v
                                    Shipped
```

## SPEC-ORD-001 - Valid transitions

Only valid lifecycle transitions may occur.

For example:

```text
Pending -> Confirmed
Confirmed -> Preparing
Preparing -> Shipped
Shipped -> Delivered
```

An order must not skip directly from Pending to Delivered.

---

## SPEC-ORD-002 - Payment failure

**Given** an order is Pending

**When** payment authorization fails

**Then**:

* the order becomes `PaymentFailed`
* the inventory reservation is released
* the order is not confirmed

---

## SPEC-ORD-003 - Shipment failure

**Given** an order is Preparing

**When** the shipment creation workflow fails before a shipment is successfully established

**Then** the order becomes:

```text
ShippingPendingRetry
```

Orders owns this order transition; Shipping reports the shipment operation result and does not arbitrarily change the Order state.

No automatic retry is required in Phase 1.

---

## SPEC-ORD-004 - Customer cancellation

Customers cannot cancel orders during Phase 1.

---
# 8. Inventory

## SPEC-INV-001 - Available quantity

Inventory availability is calculated as:

```text
available = quantity_on_hand - quantity_reserved
```

---

## SPEC-INV-002 - Successful reservation

**Given**:

```text
quantity_on_hand = 5
quantity_reserved = 0
requested = 3
```

**When** the reservation is processed

**Then**:

```text
reservation succeeds
quantity_reserved = 3
available = 2
```

---

## SPEC-INV-003 - Insufficient inventory

**Given**:

```text
available = 2
requested = 3
```

**When** a reservation is requested

**Then** the reservation fails.

No payment authorization may occur.

---

## SPEC-INV-004 - Concurrent reservation

**Given** only one unit is available

**And** two customers attempt to reserve that unit concurrently

**Then** only one reservation may succeed.

The Inventory database must enforce this through an atomic/concurrency-safe operation.

Distributed locking is not required.

---

## SPEC-INV-005 - Release reservation

**Given** a payment authorization fails

**When** Orders requests reservation release

**Then** Inventory releases the reserved quantity.

---
# 9. Pricing

## SPEC-PRICE-001 - Catalog owns current price

Catalog is the source of truth for the current product price.

---

## SPEC-PRICE-002 - Cart does not snapshot price

The cart stores:

```text
product_id
quantity
```

It does not permanently store the authoritative product price.

---

## SPEC-PRICE-003 - Order snapshots price

**Given** the Catalog price is `$100`

**When** checkout succeeds

**Then** the OrderItem stores `$100` as its immutable unit-price snapshot.

If Catalog later changes the product to `$120`, the existing order remains `$100`.

---
# 10. Shipping

## SPEC-SHIP-001 - Shipping methods

Phase 1 provides two fixed shipping methods.

Each method has:

* stable code
* display name
* cost
* estimated delivery time

Example:

```text
STANDARD
EXPRESS
```

---

## SPEC-SHIP-002 - Quote ownership

Shipping calculates the shipping quote.

Orders must not contain shipping calculation logic.

---

## SPEC-SHIP-003 - Shipping snapshot

Once checkout creates the order, Orders stores the selected shipping information as an immutable snapshot.

---

## SPEC-SHIP-004 - Shipment

**Given** an order has entered Preparing

**When** Shipping successfully creates a shipment

**Then** the order becomes `Shipped`.

A Phase 1 order has exactly one shipment.

---

## SPEC-SHIP-005 - Create shipment from preparing order

**Given** an order is `Preparing`

**When** an authorized operations user creates a shipment

**Then** Shipping creates a shipment associated with that order.

The shipment must contain the information required to fulfill the order, including the order reference and shipping information required by the Shipping service.

Shipping stores the recipient and delivery address as an immutable shipment snapshot. This is a fulfillment-specific copy of the address captured by Orders at checkout; later customer profile changes must not alter an existing shipment destination.

A Phase 1 order may have exactly one shipment.

The operation must reject shipment creation when:

- the order does not exist
- the order is not `Preparing`
- a shipment already exists for the order

After successful shipment creation:

- the shipment has status `Created`
- the shipment receives its tracking identifier
- the order becomes `Shipped`

The tracking identifier must be generated as part of successful shipment creation and must not exist before the shipment is created.

Creating a shipment and associating it with an order is one logical operation from the Operations user's perspective. The frontend must initiate one shipment workflow; it must not coordinate separate requests to create a shipment and mark the order as shipped.

The workflow is rendered by [create_shipment.html](../ui_renderings/luna_ops/create_shipment.html).

---

## SPEC-SHIP-006 - Shipment created state

A newly created shipment must have the status:

```text
Created
```

A shipment in `Created` state represents a shipment that exists but has not yet entered transit.

A `Created` shipment may transition to `InTransit`. It must not transition directly to `Delivered`.

A shipment already in `Created` state must not be recreated for the same order.

---

## SPEC-SHIP-007 - Mark shipment in transit

**Given** a shipment is `Created`

**When** an authorized operations user marks the shipment as in transit

**Then** the shipment becomes `InTransit`.

The operation must reject attempts to mark a shipment in transit when the shipment is not currently `Created`.

The associated order remains `Shipped`.

The customer-facing order state may present this as:

```text
Shipped / On the way
```

The shipment state remains the authoritative state for shipment progress.

---

## SPEC-SHIP-008 - Mark shipment delivered

**Given** a shipment is `InTransit`

**When** an authorized operations user marks the shipment as delivered

**Then**:

- the shipment becomes `Delivered`
- the associated order becomes `Delivered`

The operation must reject attempts to deliver a shipment that is not currently `InTransit`.

After successful delivery, the Operations UI must no longer expose an action to advance the shipment. The customer order must display the order as delivered.

The shipment list and detail workflows are rendered by [shipments.html](../ui_renderings/luna_ops/shipments.html) and [shipment_details.html](../ui_renderings/luna_ops/shipment_details.html).

---
# 11. Payments

## SPEC-PAY-001 - Authorization only

Phase 1 supports payment authorization.

It does not support:

* capture
* void
* refunds

---

## SPEC-PAY-002 - Deterministic provider

The fake payment provider must allow deterministic success and failure scenarios.

Tests must not depend on random payment outcomes.

---

## SPEC-PAY-003 - Payment success

**Given** a valid payment request

**When** authorization succeeds

**Then** Payments returns a successful authorization result.

Orders may then transition to `Confirmed`.

---

## SPEC-PAY-004 - Payment failure

**Given** a payment authorization is rejected

**Then** Orders must not confirm the order.

Inventory must be released.

## SPEC-PAY-005 - Concurrent authorization safety

**Given** two authorization requests for the same order arrive concurrently

**Then** Payments maintains one payment aggregate per order

**And** optimistic concurrency prevents one request from silently overwriting the other

**And** repeated provider authorization keys return the same deterministic provider result.

---
# 12. Fulfillment

## SPEC-FUL-001 - Fulfillment remains inside Orders

Fulfillment is implemented as a module inside Orders.

It does not become a separate service or database during Phase 1.

---

## SPEC-FUL-002 - Prepare order

**Given** an order is Confirmed

**When** fulfillment begins

**Then** the order enters `Preparing`.

Phase 1 fulfillment is intentionally simple.

It does not model:

* warehouse optimization
* picking
* packing
* multiple fulfillment centers
* split shipments

## SPEC-FUL-003 - Operations fulfillment queue

The Operations interface must provide a fulfillment queue for orders requiring fulfillment action.

The queue must display orders whose lifecycle requires an operations action, including:

- `Confirmed`
- `Preparing`
- `ShippingPendingRetry`

Each order entry must provide enough information for an operator to identify the order and its current state.

At minimum, the queue displays:

- order ID
- customer
- item count
- total
- payment status
- order status
- creation date
- available action

The available action must correspond to the current order state:

```text
Confirmed
   -> Start Preparing

Preparing
   -> Create Shipment

ShippingPendingRetry
   -> Retry Shipment
```

The Operations UI must not expose invalid lifecycle actions. The queue does not change order state directly; actions must be processed by the Orders and Shipping APIs.

The queue is rendered by [fullfilment.html](../ui_renderings/luna_ops/fullfilment.html).

---

## SPEC-FUL-004 - Start preparing order

**Given** an order is `Confirmed`

**When** an authorized operations user starts fulfillment

**Then** the order becomes `Preparing`.

The transition must be performed through the Orders application and domain workflow.

The operation must reject attempts to prepare an order that is not currently `Confirmed`.

The operation must not modify:

- order pricing
- payment state
- inventory reservation state
- shipment state

After a successful transition, the order must appear as `Preparing` in the Operations fulfillment queue.

The order-level workflow is rendered by [fullfilment_details.html](../ui_renderings/luna_ops/fullfilment_details.html).

---
# 13. Frontend Behavior

## SPEC-FE-001 - Customer routes

Phase 1 provides the customer-facing routes required by the commerce flow:

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

The shop and product pages are publicly accessible. Cart, checkout, orders, and account operations require authentication.

---

## SPEC-FE-002 - Public catalog behavior

The customer can:

- open the shop
- refresh the page
- share a filtered catalog URL
- use browser back and forward navigation
- search by product name
- filter by category
- open a product details page

---

## SPEC-FE-003 - Interactive behavior

Interactive operations must provide loading feedback and prevent invalid repeated actions. Checkout submission must not be submitted twice while a request is in progress.

---

## SPEC-FE-004 - Error behavior

Backend failures must be presented as customer-friendly messages. Raw exceptions, stack traces, database errors, and internal implementation details must not be shown.

---

## SPEC-FE-005 - Operations fulfillment UI

The frontend must provide an authenticated Operations fulfillment interface.

The fulfillment interface must allow an authorized operations user to:

1. View the fulfillment queue.
2. Filter orders by fulfillment state.
3. Open an order.
4. Start preparation for a `Confirmed` order.
5. Create a shipment for a `Preparing` order.
6. Retry shipment creation for an order in `ShippingPendingRetry`, where supported.

The UI must only display actions valid for the current backend state. The frontend must not implement order state transitions locally.

After an operation succeeds, the frontend must refresh the order state from the backend.

If an operation fails:

- the current state must remain visible
- an appropriate error must be displayed
- the UI must not assume that the transition occurred

The Operations interface must not expose customer-only functionality as Operations functionality.

The queue and order workflow are represented by [fullfilment.html](../ui_renderings/luna_ops/fullfilment.html) and [fullfilment_details.html](../ui_renderings/luna_ops/fullfilment_details.html).

---

## SPEC-FE-006 - Operations shipment UI

The frontend must provide an authenticated Operations shipment interface.

The shipment interface must allow an authorized operations user to:

1. View shipments.
2. Filter shipments by status.
3. Open shipment details.
4. Mark a `Created` shipment as `InTransit`.
5. Mark an `InTransit` shipment as `Delivered`.

The shipment UI must display:

- tracking identifier
- order reference
- customer
- shipment status
- creation date
- available action

The UI must only display actions valid for the current shipment state:

```text
Created
   -> Mark In Transit

InTransit
   -> Mark Delivered

Delivered
   -> No further action
```

The frontend must not directly modify shipment or order state.

The shipment list and detail workflows are represented by [shipments.html](../ui_renderings/luna_ops/shipments.html) and [shipment_details.html](../ui_renderings/luna_ops/shipment_details.html). Shipment creation is represented by [create_shipment.html](../ui_renderings/luna_ops/create_shipment.html).

---

## SPEC-FE-007 - Customer shipment tracking

The customer order detail page must display fulfillment and shipment progress when shipment information is available.

The customer must be able to see the order lifecycle:

```text
Order placed
Payment authorized
Preparing
Shipped
Delivered
```

When a shipment exists, the customer order detail page must display:

- tracking identifier
- shipment status

The customer-facing presentation may translate internal shipment terminology into customer-friendly terminology. For example:

```text
Internal:
InTransit

Customer:
Shipped / On the way
```

The customer must not see internal Operations functionality or controls. The customer must not be able to modify order or shipment state from the tracking interface.

The customer-facing tracking presentation is represented by the existing order detail renderings under `documentation/ui_renderings/orders/`.

# 14. Service API Requirements

The exact request/response schemas are defined by each service's OpenAPI contract.

The following operations are required.

## Catalog

```text
GET /api/v1/catalog/products
GET /api/v1/catalog/products/{id}
GET /api/v1/catalog/categories
```

---

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

Orders must also expose operations for retrieving the fulfillment queue and starting fulfillment for a specific order. The exact request and response schemas are defined by the Orders OpenAPI contract.

---

## Inventory

```text
POST /api/v1/reservations
POST /api/v1/reservations/{id}/release
```

---

## Payments

```text
POST /api/v1/payments/authorize
```

---

## Shipping

```text
GET  /api/v1/shipping-methods
POST /api/v1/quotes
POST /api/v1/shipments
```

Shipping must also expose operations for listing and retrieving shipments and for marking a shipment `InTransit` or `Delivered`. Creating a shipment for an order and advancing the associated order to `Shipped` is one logical workflow from the Operations user's perspective; the frontend must not coordinate separate order and shipment state-changing requests.

---
# 15. Failure Model

Phase 1 intentionally supports only a small failure model.

| Failure                    | Expected result                          |
| -------------------------- | ---------------------------------------- |
| Product unavailable        | Checkout fails                           |
| Insufficient inventory     | Reservation fails; payment not attempted |
| Payment rejected           | `PaymentFailed`; inventory released      |
| Shipment creation fails    | `ShippingPendingRetry`                   |
| Unexpected service failure | Structured `500` response                |

The following are intentionally unresolved:

```text
service crashes after successful payment
request timeout after downstream success
lost response after inventory reservation
partial distributed transaction
automatic recovery
```

Repeated checkout requests with the same customer and `Idempotency-Key` are handled by the current Orders implementation. Distributed failure cases involving timeouts, crashes, partial completion, or recovery remain unresolved and are intentionally deferred.

These become important in later phases.

---
# 16. Explicitly Out of Scope

Phase 1 does not implement:

* RabbitMQ
* asynchronous consumers
* transactional outbox
* retries
* dead-letter queues
* distributed compensation/Sagas beyond the synchronous payment-failure reservation release
* distributed locks
* payment capture
* payment refunds
* real payment providers
* anonymous carts
* saved addresses
* multiple shipments
* multi-warehouse allocation
* split fulfillment
* customer cancellation
* warehouse optimization
* chaos testing
* advanced operational workflows beyond the current telemetry and SigNoz foundation
* advanced security hardening
* separate Fulfillment service

---
# 17. Acceptance Criteria

Phase 1 is complete when the following end-to-end scenario works:

```text
Customer
   v
Login
   v
Browse catalog
   v
Open product
   v
Add product to cart
   v
View cart
   v
Checkout
   v
Select shipping
   v
Submit order
   v
Catalog validation
   v
Shipping quote
   v
Pending order
   v
Inventory reservation
   v
Payment authorization
   v
Confirmed order
   v
Fulfillment
   v
Shipment
   v
Shipped
   v
Delivered
```

And the following failure scenarios are demonstrably correct:

```text
Insufficient inventory
    -> checkout fails
    -> payment is not attempted

Payment failure
    -> order becomes PaymentFailed
    -> inventory is released

Shipment failure
    -> order becomes ShippingPendingRetry
```

The phase is also complete when:

* [x] all required APIs are implemented
* [x] OpenAPI documents the APIs
* [x] service boundaries remain intact
* [x] database ownership remains isolated
* [x] migrations create the required schemas
* [x] meaningful unit tests exist
* [x] integration tests exercise real persistence
* [x] concurrency behavior is tested for inventory
* [x] customer frontend implements the required routes
* [x] authentication works
* [ ] checkout works end-to-end across real service boundaries
* [x] failure scenarios emit structured application and telemetry signals
* [x] Phase 2 can introduce asynchronous messaging without redesigning the basic commerce model

---

# 18. Testing

## SPEC-TEST-005 - Fulfillment lifecycle integration test

An integration test must verify the fulfillment lifecycle using real Orders persistence.

The test must:

1. Create an order in `Confirmed` state.
2. Start fulfillment.
3. Verify the order becomes `Preparing`.
4. Attempt an invalid preparation transition.
5. Verify the invalid transition is rejected.
6. Verify the order remains `Preparing`.

The test must verify that starting fulfillment does not alter:

- order total
- order item snapshots
- payment state
- inventory reservation state

The test should exercise the application and domain boundary rather than directly modifying the order status.

---

## SPEC-TEST-006 - Shipment lifecycle integration test

An integration test must verify the shipment lifecycle across Orders and Shipping.

The successful lifecycle must verify:

```text
Order: Confirmed
   v
Order: Preparing
   v
Create Shipment
   v
Shipment: Created
   v
Order: Shipped
   v
Shipment: InTransit
   v
Shipment: Delivered
   v
Order: Delivered
```

The test must verify that:

1. A shipment cannot be created for an order that is not `Preparing`.
2. A preparing order can have only one shipment.
3. Successful shipment creation produces a tracking identifier.
4. A newly created shipment has status `Created`.
5. A `Created` shipment can become `InTransit`.
6. An `InTransit` shipment can become `Delivered`.
7. A shipment cannot transition directly from `Created` to `Delivered`.
8. The associated order becomes `Delivered` when its shipment is delivered.

The test must use the real service persistence boundaries rather than directly manipulating domain state to simulate the lifecycle.

---

# 23. Implementation Checklist

## Catalog

- [x] SPEC-CAT-001 - Browse products
- [x] SPEC-CAT-002 - Search products
- [x] SPEC-CAT-003 - Filter by category
- [x] SPEC-CAT-004 - Product details

## Cart

- [x] SPEC-CART-001 - Add item
- [x] SPEC-CART-002 - Change quantity
- [x] SPEC-CART-003 - Remove item
- [x] SPEC-CART-004 - Current pricing
- [x] SPEC-CART-005 - Inventory is not reserved

## Authentication

- [x] SPEC-AUTH-001 - Customer authentication
- [x] SPEC-AUTH-002 - Roles
- [x] SPEC-AUTH-003 - Session boundary

## Checkout

- [x] SPEC-CHK-001 - Checkout requires authentication
- [x] SPEC-CHK-002 - Validate products
- [x] SPEC-CHK-003 - Calculate shipping
- [x] SPEC-CHK-004 - Create pending order
- [x] SPEC-CHK-005 - Reserve inventory
- [x] SPEC-CHK-006 - Authorize payment
- [x] SPEC-CHK-007 - Confirm order

## Order lifecycle

- [x] SPEC-ORD-001 - Valid transitions
- [x] SPEC-ORD-002 - Payment failure
- [x] SPEC-ORD-003 - Shipment failure
- [ ] SPEC-ORD-004 - Customer cancellation

## Inventory

- [x] SPEC-INV-001 - Available quantity
- [x] SPEC-INV-002 - Successful reservation
- [x] SPEC-INV-003 - Insufficient inventory
- [x] SPEC-INV-004 - Concurrent reservation
- [x] SPEC-INV-005 - Release reservation

## Pricing

- [x] SPEC-PRICE-001 - Catalog owns current price
- [x] SPEC-PRICE-002 - Cart does not snapshot price
- [x] SPEC-PRICE-003 - Order snapshots price

## Shipping

- [x] SPEC-SHIP-001 - Shipping methods
- [x] SPEC-SHIP-002 - Quote ownership
- [x] SPEC-SHIP-003 - Shipping snapshot
- [x] SPEC-SHIP-004 - Shipment
- [x] SPEC-SHIP-005 - Create shipment from preparing order
- [x] SPEC-SHIP-006 - Shipment created state
- [x] SPEC-SHIP-007 - Mark shipment in transit
- [x] SPEC-SHIP-008 - Mark shipment delivered

## Payments

- [x] SPEC-PAY-001 - Authorization only
- [x] SPEC-PAY-002 - Deterministic provider
- [x] SPEC-PAY-003 - Payment success
- [x] SPEC-PAY-004 - Payment failure

## Fulfillment

- [x] SPEC-FUL-001 - Fulfillment remains inside Orders
- [x] SPEC-FUL-002 - Prepare order
- [x] SPEC-FUL-003 - Operations fulfillment queue
- [x] SPEC-FUL-004 - Start preparing order

## Frontend

- [x] SPEC-FE-001 - Customer routes
- [x] SPEC-FE-002 - Public catalog behavior
- [x] SPEC-FE-003 - Interactive behavior
- [x] SPEC-FE-004 - Error behavior
- [x] SPEC-FE-005 - Operations fulfillment UI
- [x] SPEC-FE-006 - Operations shipment UI
- [ ] SPEC-FE-007 - Customer shipment tracking

## Database

- [x] SPEC-DB-001 - Migrations
- [x] SPEC-DB-002 - Migration startup
- [x] SPEC-DB-003 - Cross-service relationships

## Testing

- [x] SPEC-TEST-001 - Business behavior
- [x] SPEC-TEST-002 - Catalog reads
- [ ] SPEC-TEST-003 - Cross-service behavior across real service boundaries
- [x] SPEC-TEST-004 - Coverage collection and quality-gate reporting
- [ ] SPEC-TEST-005 - Fulfillment lifecycle integration test
- [ ] SPEC-TEST-006 - Shipment lifecycle integration test
