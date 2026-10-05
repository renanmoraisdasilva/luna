# Codebase tour

A reading guide. It answers "where does this live and what happens when I click
X", in plain language, with file paths you can open.

It is **not** a design document. [`architecture.md`](architecture.md) explains *why* the system is shaped
this way; this document explains *where the code is*. Where the two overlap, follow the link rather than
expecting a restatement.

It is written for someone reading code they did not write, in a repository that was largely written by an
AI. That shapes a few things: the explanations assume no prior context, the patterns are named explicitly
so you can search for them later, and there is a section on places the code will surprise you.

---

## How to use this

If you have ten minutes, read sections 1 to 4 and section 6. That is the whole system.

If you are about to change the order flow, read section 6 properly and section 10. If you are about to add
a new service, read sections 3, 4, and 7 first.

---

## 1. The five things to know

**1. Six independent services, each with its own database.**

```
Identity     customers, logins, tokens        → IdentityDb
Catalog      products, categories, prices    → CatalogDb
Cart+Orders  carts and orders                 → OrdersDb
Payments     payment attempts                → PaymentsDb
Inventory    stock levels, reservations       → InventoryDb
Shipping     quotes, shipments               → ShippingDb
```

No service reads another's database. Ever. When Orders needs stock, it *asks* Inventory over HTTP. This
single rule causes most of the awkwardness in the codebase, including the checkout saga.

**2. The Next.js frontend is a gateway, not just a UI.**

It renders pages *and* proxies API calls to the backend services. The browser never talks to a .NET service
directly. That rewrite table lives in `src/Frontend/next.config.mjs`.

**3. Each service is split into five projects, and the split is a rule, not a suggestion.**

The projects are named `{Service}.Api`, `.Application`, `.Contracts`, `.Domain`, `.Infrastructure`. Code
belongs in the layer it belongs to, and an architecture test enforces it
(`tests/Unit/Architecture/LayeringArchitectureTests.cs`).

**4. There is exactly one program entry point per service, and it is short.**

`src/Services/Orders/Orders.Api/Program.cs` is about 50 lines: register services, build, migrate the
database, add middleware, map controllers, run. All the interesting code is in the layers behind it.

**5. Everything is synchronous HTTP. There is no message broker.**

No queue, no event bus, no background jobs. Two services talk by one calling the other and waiting. Phase 2
plans to change this; see section 9.

---

## 2. Repository map

```
src/
  Contracts/Luna.Contracts/          small types shared across services (errors, pagination, claims)
  Services/
    Orders/     Api  Application  Contracts  Domain  Infrastructure     ← the five layers
    Catalog/    Api  Application  Contracts  Domain  Infrastructure
    Inventory/  Api  Application  Contracts  Domain  Infrastructure
    Payments/   Api  Application  Contracts  Domain  Infrastructure
    Shipping/   Api  Application  Contracts  Domain  Infrastructure
    Identity/   Identity.csproj                                        ← the one exception, see section 8
  Shared/
    Luna.Authentication/             JWT validation for APIs, service-to-service tokens
    Luna.Observability/              OpenTelemetry + SigNoz wiring
  Frontend/                          Next.js app, gateway, and customer UI
tests/
  Unit/                              domain rules, handlers, architecture rules
  Integration/                       each public endpoint through the real host and a real database
infrastructure/                      Docker Compose, Dockerfiles, environment examples, observability config
documentation/                       this file and its neighbours
```

Size, roughly: 180 C# files in `src/`, 231 unit tests, 147 integration tests. `Orders` is the largest
service at 88 files, which makes sense since it holds carts, orders, *and* fulfilment.

---

## 3. Anatomy of one service

All five services except Identity follow this. Use Orders as the worked example.

| Layer | Project | What lives there | What must not live there |
| --- | --- | --- | --- |
| **Api** | `Orders.Api` | Controllers, middleware, `Program.cs`, authorization policies | Business rules |
| **Application** | `Orders.Application` | Handlers that orchestrate use cases, repository *interfaces*, outbound *port* interfaces | SQL, HTTP clients |
| **Domain** | `Orders.Domain` | Entities and the rules that protect them. No dependencies at all | Anything else |
| **Contracts** | `Orders.Contracts` | The request/response shapes this service exposes | Logic |
| **Infrastructure** | `Orders.Infrastructure` | EF Core, the DbContext, repository *implementations*, HTTP *adapters* | Business rules |

The Application layer declaring interfaces that Infrastructure implements is the dependency-inversion
pattern. It exists so the handler can be tested without a database or a network.

### The concrete file layout for Orders

```
Orders.Domain/
  Order.cs                 the order aggregate: creation rules, status transitions, invariants
  Cart.cs, CartItem.cs     the cart aggregate
  OrderStatus              the lifecycle enum

Orders.Application/
  Checkout/                the checkout saga
    CheckoutCommands.cs    CheckoutHandler — the orchestration (read this one)
    CheckoutPorts.cs       interfaces for the four other services + snapshot records
    CheckoutExceptions.cs  CheckoutRejectedException
  Orders/
    FulfillmentCommands.cs  handlers for prepare / create shipment / mark shipped / etc.
    FulfillmentQueries.cs   handlers for the operations-console reads
    IOrderReadRepository.cs, IOrderWriteRepository.cs
  Carts/
    CartCommands.cs, CartQueries.cs, ICartReadRepository.cs, ICartWriteRepository.cs

Orders.Infrastructure/
  Database/OrdersDbContext.cs
  Repositories/
    OrderRepository.cs          implements IOrderWriteRepository   (loads tracked entities)
    OrderReadRepository.cs      implements IOrderReadRepository   (projects straight to DTOs)
    CartRepository.cs, CartReadRepository.cs
  Checkout/CheckoutHttpClients.cs   implements the four ports over HTTP

Orders.Api/
  Controllers/  CheckoutController, OrdersController, CartsController, FulfillmentController
  Middleware/   CorrelationIdMiddleware, ExceptionHandlingMiddleware
  Authorization/OrdersAuthorizationPolicies.cs
  Program.cs
```

---

## 4. Two kinds of service call

This trips people up, so it is worth being explicit. There are two entirely different credentials.

**A human calling us.** The browser holds a cookie containing an OpenIddict access token. `Program.cs` calls
`AddLunaJwtValidation`, which validates it. The controller reads the customer id from the claims:

```csharp
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme)]
public sealed class CheckoutController(CheckoutHandler checkout, ...) : ControllerBase
{
    public Task<CheckoutResponse> Submit(CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!User.TryGetSubjectId(out var customerId))
        {
            throw new UnauthenticatedCustomerException();
        }

        return checkout.HandleAsync(new CheckoutCommand(customerId, request, idempotencyKey ?? string.Empty), cancellationToken);
    }
}
```

Note the controller does almost nothing: check who is calling, build a command, call the handler. That is
the "thin controllers" rule in practice.

**One service calling another.** No user is involved. Orders does *not* forward the customer's token. It
fetches its own service token from Identity using `ServiceAuthentication:ClientId` and
`ServiceAuthentication:ClientSecret`, which carry narrow scopes. `AuthorizeAsync` on the Payments client
needs the `PaymentsAuthorize` scope and nothing else.

The scopes are declared in `src/Services/Orders/Orders.Infrastructure/OrdersInfrastructureExtensions.cs`
and defined in `src/Shared/Luna.Authentication/ServiceAuthentication/`.

---

## 5. CQRS, concretely

CQRS here is a *convention*, not a framework. There is no MediatR, no command bus.

The convention: **read paths and write paths use different repository interfaces.**

- `IOrderWriteRepository` returns real `Order` entities, tracked by EF Core. Used when you need to change
  something.
- `IOrderReadRepository` returns response records and never materialises an entity. It uses
  `.AsNoTracking()` and projects inside the query, so SQL Server does the work.

```csharp
// read side — the database does the arithmetic
var items = await ordersQuery
    .OrderByDescending(order => order.CreatedAt)
    .Skip((query.Page - 1) * query.PageSize)
    .Take(query.PageSize)
    .Select(order => new
    {
        order.Id,
        ItemCount = order.Items.Count,
        Total = order.Items.Sum(item => item.LineTotal) + order.ShippingCost,
        PaymentStatus = order.PaymentId.HasValue ? "Authorized" : "NotAuthorized",
        order.Status,
    })
    .ToArrayAsync(cancellationToken);
```

Compare with the write side, which must load and return a tracked entity so it can be mutated:

```csharp
public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
    dbContext.Orders.SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
```

**Why it matters:** the read side can skip loading `ShippingAddress` and every `OrderItem` for list views,
and the domain cannot be corrupted by a read query. **When writing code, ask which side you are on.** If
you are building a screen or a list, you want the read repository. If you are changing state, you want the
write repository and a domain method.

---

## 6. The order flow

This is the part worth reading carefully, because it is the only genuinely hard flow in the system.

### 6.1 Lifecycle

`OrderStatus` in `Orders.Domain/Order.cs`:

```
Pending ──┬── PaymentFailed      (card declined, or authorisation failed)
          ├── Cancelled          (insufficient inventory)
          └── Confirmed          (money authorised, stock reserved)
                 │
                 └── Preparing ── Shipped ── Delivered
```

Every transition is a method on `Order` that calls `EnsureStatus(...)` first. You cannot skip a step; the
domain throws if you try. `PaymentFailed` and `Cancelled` are terminal. So is `Delivered`.

### 6.2 Checkout, step by step

Trigger: the customer presses Pay on the storefront.

```
1. Browser                POST /api/v1/orders/checkout
                          + Idempotency-Key: <uuid>
                          + cookie with the customer's access token

2. Gateway                Next.js proxies to the Orders service
                          (src/Frontend/next.config.mjs)

3. Controller             Orders.Api/Controllers/CheckoutController.cs
                          - confirms the caller is authenticated
                          - pulls customerId out of the claims
                          - builds CheckoutCommand
                          - calls the handler. Nothing else.

4. Handler                Orders.Application/Checkout/CheckoutCommands.cs
                          CheckoutHandler.HandleCoreAsync, in order:

   4a. ValidateCommand            customer id and idempotency key must exist
   4b. Normalize + replay check   have we already done this exact checkout?
                                  → yes and confirmed  → return the original result
                                  → yes and pending    → refuse (CHECKOUT_IN_PROGRESS)
                                  → no                  → continue
   4c. ValidatePayment            payment method present, currency is 3 letters
   4d. LoadCartAsync              read the customer's cart; must not be empty
   4e. Parallel fetch             shipping quote  ─┐
                                   product details ├─ run at the same time
                                  (Task.WhenAll)  ─┘
   4f. CreateOrder                build the Order aggregate in memory (still Pending)
   4g. Save                       ← the order now exists in SQL Server

   --- from here on, other services are involved ---

   4h. ReserveInventoryAsync      POST to Inventory. On failure: order.Cancel(), save, throw
   4i. Save reservation id        ← added in the last commit
   4j. AuthorizePaymentAsync      POST to Payments.
                                  On decline: release reservation, MarkPaymentFailed, save, throw
   4k. Save payment id            ← added in the last commit
   4l. ConfirmOrderAsync          order.Confirm(), save
```

**Why steps 4i and 4k exist.** They were added recently. Previously both ids were written once, on the
last line. So a crash between 4g and 4l left *no record* that a reservation or a payment existed — the
stock was held by Inventory and the money was held by Payments, and nothing in Orders said so. Now each
step is saved before the next begins, so a crash leaves a trail. The trail is currently written and
nothing reads it yet; see section 9.

### 6.3 Why this is called a saga

Steps 4g through 4l touch three databases with no transaction spanning them. There is no way to roll all of
it back atomically. So the code compensates by hand: if payment declines, undo the reservation; if
inventory is unavailable, cancel the order.

This works for *expected* failures. It cannot handle the process dying, because the code doing the
unwinding is itself dead. `lessons-learned.md` section 4 has the worked examples.

### 6.4 Fulfilment, the other half of Orders

Fulfilment is **not a separate service**. It is a module inside Orders, exposed by `FulfillmentController`.
`architecture.md` explains why; briefly, it lets the operations console present it as its own area without
a seventh deployable.

Same saga shape, but in the other direction — Orders calls out to Shipping:

```
PrepareFulfillmentOrderHandler     order.Prepare()                        save
CreateShipmentHandler              guard status is Preparing
                                   POST to Shipping to create the shipment
                                   order.RecordShipment(id), order.MarkShipped()   save
MarkShipmentInTransitHandler       order.MarkInTransit()                  save
MarkShipmentDeliveredHandler       order.MarkDelivered()                  save
```

`EnsureShipmentCreationAllowed()` means an order that is not `Preparing` cannot get a shipment. The
operations console reads through `IOrderReadRepository` / `FulfillmentQueries`.

### 6.5 Errors

Handlers throw domain-flavoured exceptions; `ExceptionHandlingMiddleware` translates them to HTTP. This is
the only place that knows about status codes:

| Exception | Status | Code |
| --- | --- | --- |
| `CheckoutRejectedException` | 422 | the specific code, e.g. `PAYMENT_DECLINED` |
| `UnauthenticatedCustomerException` | 401 | `UNAUTHENTICATED` |
| `ArgumentException` | 400 | `INVALID_REQUEST` |
| `KeyNotFoundException` | 404 | `CART_NOT_FOUND` |
| `DbUpdateConcurrencyException` | 409 | `ORDER_CONCURRENCY_CONFLICT` |
| `InvalidOperationException` | 409 | `FULFILLMENT_CONFLICT` |
| anything else | 500 | `INTERNAL_ERROR` (message is not leaked) |

So if you add a new failure mode, add an exception and one arm here. Do not start returning status codes
from a handler.

---

## 7. Patterns actually in use

Named so you can grep for them later. Not all of them are textbook-perfect; where the code departs, it says
so.

| Pattern | Where to see it |
| --- | --- |
| **Aggregate / invariant protection** | `Order.EnsureStatus`, `ShippingAddress.Create`, `Cart` |
| **Repository** | `OrderRepository`, `CartRepository` — interfaces in Application, EF in Infrastructure |
| **CQRS** | `IOrderReadRepository` vs `IOrderWriteRepository`; read side uses `AsNoTracking` + projection |
| **Ports and adapters (hexagonal)** | `CheckoutPorts.cs` declares four interfaces; `CheckoutHttpClients.cs` implements them |
| **Dependency injection** | `Program.cs`; handlers registered one line each with `AddScoped` |
| **Middleware pipeline** | `CorrelationIdMiddleware` then `ExceptionHandlingMiddleware` |
| **Saga with compensation** | `CheckoutHandler`, `HandlePaymentFailureAsync` |
| **Idempotency** | `Idempotency-Key` header, `CheckoutHandler.NormalizeIdempotencyKey`, frontend `lib/idempotency.ts` |
| **Optimistic concurrency** | `Order.RowVersion`, mapped to EF's concurrency token; `409` on conflict |
| **Token-bucket rate limiting** | `IdentityRateLimitingExtensions` — token endpoint only |
| **Structured logging** | Serilog, with `Enrich.WithProperty("Service", ...)`, no interpolated messages |
| **Architecture testing** | `LayeringArchitectureTests` — 5 rules, fails the build if a layer is violated |
| **Testcontainers** | one MSSQL container per service under test |
| **Central package versions are *not* used** | every `.csproj` pins its own version explicitly |

That last row is worth knowing: there is no `Directory.Packages.props`. Searching for a package version
means searching all the `.csproj` files, which is why Dependabot generates many pull requests.

---

## 8. Where this code will surprise you

Practical things that cost someone an hour. All of them are real, not stylistic complaints.

**Identity is one project, not five.** `src/Services/Identity/Identity.csproj` contains `Application/`,
`Controllers/`, `Domain/`, `Infrastructure/`, `Migrations/` as *folders*. This is deliberate and documented
in `AGENTS.md` — the OpenIddict host predates the template. Do not "fix" it.

**Every database write is its own transaction.** There is no unit-of-work spanning a handler. `SaveChangesAsync`
is called explicitly after each step. This is why the checkout flow can be interrupted between steps, and it
is deliberate.

**`Program.cs` ends with `public partial class Program { }`.** That looks like noise. It exists so
`WebApplicationFactory<T>` can find the entry point in tests. Do not remove it.

**Tests use `extern alias`.** Fourteen lines across eight files, e.g. `extern alias PaymentsApi;`, then
`PaymentsApi::Program`. This is project aliasing: several services each have a type called `Program`, and
without aliases the test project cannot reference more than one. The aliases are declared in the test
`.csproj` as `<ProjectReference ... Aliases="PaymentsApi" />`.

**The read repository computes `Total` in SQL, in two places.** `OrderReadRepository` sums line totals plus
shipping cost in both the queue query and the detail query. If you change the pricing rule, change the
domain and both queries, or they will drift.

**Swagger is only served in Development.** `Program.cs` guards `UseSwagger()` with
`IsDevelopment()`, and `ENABLE_API_EXPLORER` gates the API explorer. If you cannot find Swagger locally,
that is why.

**`swagger-ui-react` sits in `dependencies`, not `devDependencies`.** It looks wrong. It is not — the route
is statically analysed during `next build`, and moving it breaks the build.

**Searching this repo from PowerShell.** `Select-String -Path src\**\*.cs` silently matches almost nothing,
because `**` is not recursive in PowerShell. It will report `0` matches for a symbol that is definitely
there. Use `Get-ChildItem -Recurse` and pass `.FullName`. This one has already caused a wrong conclusion.

---

## 9. What is planned but not built

**There is no message broker.** Every cross-service interaction is a synchronous HTTP call that waits. This
is why checkout has to compensate by hand, and it is the root of the recovery gap in section 6.2.

Phase 2 designs the asynchronous replacement in detail, and the design is already written:

- [`dev_phases/phase-2/spec.md`](dev_phases/phase-2/spec.md) — scope, an event catalogue, requirements,
  a **failure model**, and explicitly out-of-scope items
- [`dev_phases/phase-2/architecture.md`](dev_phases/phase-2/architecture.md) — infrastructure, the messaging
  model, and a decision record for choosing **Floci (EventBridge + SQS + SES) instead of RabbitMQ**

`architecture.md` section 8 is the short version and notes that the emulator is a local development tool
only, never deployed.

So the messaging question has already been thought about and decided at the infrastructure level. If you
are weighing "reconciler versus messaging", read the phase 2 failure model first — it should tell you
whether the planned design already answers that, or whether the two are complementary.

`architecture.md` section 10 lists the other known gaps, including the missing reconciliation job.

---

## 10. A reading order

If you are changing something, read these files first. In order:

1. `AGENTS.md` — the structural rules you must not break
2. This document, section 4 and 6
3. `Orders.Domain/Order.cs` — the invariants you cannot violate
4. `Orders.Application/Checkout/CheckoutCommands.cs` — the saga
5. `Orders.Api/Program.cs` — how it is wired
6. `Orders.Api/Middleware/ExceptionHandlingMiddleware.cs` — the error contract
7. The matching tests: `tests/Unit/Orders/` for rules, `tests/Integration/Orders/` for behaviour

Two habits that will save you time:

**Run the tests before you change anything.** `dotnet test tests\Unit` is four seconds and catches most
regressions. The full integration suite takes about seven minutes and needs Docker.

**If a test fails and it disagrees with the code you are reading, rebuild with `--no-incremental`.** MSBuild
has silently reused a stale test assembly here before, producing a failure that did not exist in the source.