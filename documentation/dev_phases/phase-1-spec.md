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

Distributed reliability concerns such as messaging, retries, idempotency, outbox processing, compensation, and distributed recovery are outside this phase.

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

The Admin role exists for authorization and identity testing.

A dedicated administration UI is out of scope.

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

**When** shipment creation fails

**Then** the order becomes:

```text
ShippingPendingRetry
```

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
duplicate checkout request
duplicate payment authorization
lost response after inventory reservation
partial distributed transaction
automatic recovery
```

These become important in later phases.

---
# 16. Explicitly Out of Scope

Phase 1 does not implement:

* RabbitMQ
* asynchronous consumers
* transactional outbox
* retries
* idempotency
* dead-letter queues
* compensation/Sagas
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
* operations dashboard
* chaos testing
* advanced observability
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

* [ ] all required APIs are implemented
* [ ] OpenAPI documents the APIs
* [ ] service boundaries remain intact
* [ ] database ownership remains isolated
* [ ] migrations create the required schemas
* [ ] meaningful unit tests exist
* [ ] integration tests exercise real persistence
* [ ] concurrency behavior is tested for inventory
* [ ] customer frontend implements the required routes
* [ ] authentication works
* [ ] checkout works end-to-end
* [ ] failure scenarios are observable
* [ ] Phase 2 can introduce asynchronous messaging without redesigning the basic commerce model

---

# 23. Implementation Checklist

## Catalog

- [x] SPEC-CAT-001 - Browse products
- [x] SPEC-CAT-002 - Search products
- [x] SPEC-CAT-003 - Filter by category
- [x] SPEC-CAT-004 - Product details

## Cart

- [ ] SPEC-CART-001 - Add item
- [ ] SPEC-CART-002 - Change quantity
- [ ] SPEC-CART-003 - Remove item
- [ ] SPEC-CART-004 - Current pricing
- [ ] SPEC-CART-005 - Inventory is not reserved

## Authentication

- [ ] SPEC-AUTH-001 - Customer authentication
- [ ] SPEC-AUTH-002 - Roles
- [ ] SPEC-AUTH-003 - Session boundary

## Checkout

- [ ] SPEC-CHK-001 - Checkout requires authentication
- [ ] SPEC-CHK-002 - Validate products
- [ ] SPEC-CHK-003 - Calculate shipping
- [ ] SPEC-CHK-004 - Create pending order
- [ ] SPEC-CHK-005 - Reserve inventory
- [ ] SPEC-CHK-006 - Authorize payment
- [ ] SPEC-CHK-007 - Confirm order

## Order lifecycle

- [ ] SPEC-ORD-001 - Valid transitions
- [ ] SPEC-ORD-002 - Payment failure
- [ ] SPEC-ORD-003 - Shipment failure
- [ ] SPEC-ORD-004 - Customer cancellation

## Inventory

- [ ] SPEC-INV-001 - Available quantity
- [ ] SPEC-INV-002 - Successful reservation
- [ ] SPEC-INV-003 - Insufficient inventory
- [ ] SPEC-INV-004 - Concurrent reservation
- [ ] SPEC-INV-005 - Release reservation

## Pricing

- [ ] SPEC-PRICE-001 - Catalog owns current price
- [ ] SPEC-PRICE-002 - Cart does not snapshot price
- [ ] SPEC-PRICE-003 - Order snapshots price

## Shipping

- [ ] SPEC-SHIP-001 - Shipping methods
- [ ] SPEC-SHIP-002 - Quote ownership
- [ ] SPEC-SHIP-003 - Shipping snapshot
- [ ] SPEC-SHIP-004 - Shipment

## Payments

- [ ] SPEC-PAY-001 - Authorization only
- [ ] SPEC-PAY-002 - Deterministic provider
- [ ] SPEC-PAY-003 - Payment success
- [ ] SPEC-PAY-004 - Payment failure

## Fulfillment

- [ ] SPEC-FUL-001 - Fulfillment remains inside Orders
- [ ] SPEC-FUL-002 - Prepare order

## Frontend

- [ ] SPEC-FE-001 - Customer routes
- [ ] SPEC-FE-002 - Public catalog behavior
- [ ] SPEC-FE-003 - Interactive behavior
- [ ] SPEC-FE-004 - Error behavior

## Database

- [ ] SPEC-DB-001 - Migrations
- [ ] SPEC-DB-002 - Migration startup
- [ ] SPEC-DB-003 - Cross-service relationships

## Testing

- [ ] SPEC-TEST-001 - Business behavior
- [ ] SPEC-TEST-002 - Catalog reads
- [ ] SPEC-TEST-003 - Cross-service behavior
- [ ] SPEC-TEST-004 - Coverage
