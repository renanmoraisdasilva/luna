# Phase 2 - Messaging and Async Workflows Specification

> Companion document: [phase 2 architecture](architecture.md) records
> **how** this phase is built: the Floci decision, the AWS mapping
> contract, messaging mechanics, infrastructure, and service structure. This
> specification records **what** the system must do and how that behavior is
> accepted, following the spec/architecture split defined in `AGENTS.md`.

## Contents

- [Purpose](#purpose)
- [1. Scope](#1-scope)
- [2. Event Catalog](#2-event-catalog-phase-2)
- [3. Messaging Requirements](#3-messaging-requirements)
- [4. Notification Service](#4-notification-service)
- [5. Frontend Behavior](#5-frontend-behavior)
- [6. Failure Model](#6-failure-model)
- [7. Explicitly Out of Scope](#7-explicitly-out-of-scope)
- [8. Acceptance Criteria](#8-acceptance-criteria)
- [9. Testing](#9-testing)
- [10. Documentation Updates Required](#10-documentation-updates-required)
- [11. Implementation Checklist](#11-implementation-checklist)

## Purpose

Phase 2 introduces asynchronous messaging to Luna's completed synchronous commerce
path. It is the first phase where services communicate through events and queues
instead of only HTTP.

The phase has two goals:

1. **Business:** An order progresses asynchronously after checkout, and the
   customer receives email notifications (order confirmation, shipment, delivery)
   without blocking any user-facing request.
2. **Learning:** Build direct fluency in the AWS asynchronous primitives Luna will
   use in Phase 15, so that moving from local to AWS is a change of deployment
   target, not a change of architecture.

**Milestone:** A customer can place an order and watch it progress while emails
are produced by an independent service consuming its own queue, using the same
APIs that will run unchanged on AWS.

**Deployment:** Phase 2 is deployed, not local-only. The identical stack runs
on the self-hosted server through Dokploy (`infrastructure/docker-compose.prod.yml`
plus the Dokploy application environment), which is a personal learning
environment rather than a public production system; the full asynchronous
pipeline runs there exactly as it does locally.

Requirement IDs are split across the two documents by intent: `SPEC-INFRA-*`,
`SPEC-MSG-001`-`009`, and `SPEC-NTF-001/003/004` define mechanism and structure
and live in [phase 2 architecture](architecture.md);
`SPEC-MSG-010`-`012`, `SPEC-NTF-002/005/006`, and `SPEC-FE-P2-*` define
behavior and live here.

## 1. Scope

In scope for Phase 2:

- The Floci emulator added to the local stack, CI, and the deployed production
  stack (`docker-compose.prod.yml`, which Dokploy reads directly from this
  repository). No account, auth token, or paid tier is involved.
- An event bus, queues, DLQs, and routing rules declared in Terraform and
  applied at every environment startup and deployment.
- Emulator configuration (hostname, region, dummy credentials) delivered
  through `.env` locally and the Dokploy application environment on the server.
- `Orders` publishes `OrderConfirmed` after a successful checkout.
- `Shipping` publishes `ShipmentCreated`, `ShipmentInTransit`, and
  `ShipmentDelivered` on lifecycle transitions.
- A new **Notification** bounded context (service + database) that consumes its
  own queue and sends email through SES.
- Competing consumers: at least two Notification worker instances process the
  same queue.
- Customer-visible eventual consistency: order status updates without a page
  reload blocking on downstream work.
- Observability for queue depth and consumer failures using the existing
  OpenTelemetry/SigNoz foundation.

## 2. Event Catalog (Phase 2)

| Event | Producer | Detail highlights | Consumers |
| --- | --- | --- | --- |
| `OrderConfirmed` | Orders | orderId, customerId, customerEmail, orderTotal, currency, correlationId | Notification (confirmation email) |
| `ShipmentCreated` | Shipping | orderId, shipmentId, carrier, customerEmail, correlationId | Notification (shipping email) |
| `ShipmentInTransit` | Shipping | orderId, shipmentId, trackingNumber, customerEmail, correlationId | Notification (in-transit email) |
| `ShipmentDelivered` | Shipping | orderId, shipmentId, trackingNumber, customerEmail, correlationId | Notification (delivery email) |

Only these four events exist in Phase 2. The broader roadmap event set
(`PaymentAuthorized`, `InventoryReserved`, `FulfillmentStarted`, ...) arrives
with Phases 3-7 as those workflows go async.

Event envelope format and routing rules are defined in
[SPEC-MSG-003](architecture.md#spec-msg-003---event-envelope) and
[SPEC-MSG-004](architecture.md#spec-msg-004---routing-rules) in the
architecture document.

**PII note:** `customerEmail` in the event detail is a deliberate trade-off so
the Notification service can render email without calling back into Orders or
Identity. This is documented here as a known compromise; a production design
would minimize PII on the bus. Do not add names, addresses, or payment data to
event details.

## 3. Messaging Requirements

These requirements define producer behavior. The mechanism (bus vs. queue
routing, consumer loop, DLQ configuration) is defined in
[phase 2 architecture](architecture.md).

### SPEC-MSG-010 - Orders publishes order confirmation

After `ConfirmOrderAsync` commits successfully, Orders publishes
`OrderConfirmed` via `PutEvents`. The HTTP checkout response is returned
regardless of publish outcome in this phase (the failure mode is documented,
not yet handled).

### SPEC-MSG-011 - Shipping publishes lifecycle events

Each successful shipment state transition (`Created`, `InTransit`,
`Delivered`) publishes the corresponding event after the owning transaction
commits. Failed transitions publish nothing.

### SPEC-MSG-012 - Checkout stays synchronous

Checkout behavior, response contracts, and idempotency semantics from Phase 1
are unchanged. Phase 2 moves the *downstream* pipeline async; it does not make
checkout return early with a pending token. (Reducing checkout latency through
parallelized dependency calls is a Phase 1 closure concern, not a Phase 2
substitution.)

## 4. Notification Service

Notification is a new bounded context. Its structure, database ownership, and
layering are defined in
[phase 2 architecture](architecture.md#4-notification-service-structure).
The behavior requirements are:

### SPEC-NTF-002 - Email templates

Three template types, rendered from event detail only:

- **Order confirmation** (`OrderConfirmed`): order id, total, currency.
- **Shipment status** (`ShipmentCreated` / `ShipmentInTransit`):
  order id, carrier, tracking number when present.
- **Delivery confirmation** (`ShipmentDelivered`).

Templates are plain text and simple HTML owned by Notification (not shared).

### SPEC-NTF-005 - Email visibility endpoint

Notification exposes an internal (private-network only, authenticated service
or operator call) read API listing sent emails for the Luna Ops surface. Luna
Ops gains an **Emails** view showing recent emails with status. Local content
verification during development may additionally use the emulated SES mailbox at
`GET /_aws/ses`, and the emulator's web UI on port `4500` lists captured mail
alongside queues and rules.

### SPEC-NTF-006 - No user-facing blocking calls

No endpoint a customer or operator waits on performs an SES call. Email is
always triggered by queue consumption.

### Duplicate delivery behavior

Sent-email rows are **recorded, never suppressed**. There is no uniqueness
constraint on event id in Phase 2, so a redelivered message produces a second
email and a second row. See
[SPEC-NTF-004](architecture.md#spec-ntf-004---sent-email-record) for
the persistence design and [criterion 9](#8-acceptance-criteria) for the
required demonstration.

## 5. Frontend Behavior

### SPEC-FE-P2-001 - Order confirmation is immediate

The checkout response and order confirmation page behave exactly as in Phase 1.
No new pending UX is introduced at checkout.

### SPEC-FE-P2-002 - Eventual order timeline updates

The customer order details page keeps polling (TanStack Query refetch/polling
with sensible intervals) so shipment status updates appear without a manual
reload once the async pipeline advances the order. Loading, empty, and
error/polling states are covered by tests per the frontend standards.

### SPEC-FE-P2-003 - Luna Ops email visibility

The Luna Ops Emails view ([SPEC-NTF-005](#spec-ntf-005---email-visibility-endpoint))
lists recent emails with recipient, subject, type, status, timestamp, **event
id, and delivery attempt count**. Two rows sharing an event id with different
attempt counts are how an operator sees a duplicate delivery happened; the view
must not group, hide, or deduplicate them. It is
read-only in Phase 2 (no re-send; re-send is Phase 4 tooling).

## 6. Failure Model

Phase 2 deliberately ships with these **known** failure modes, each documented
and observable:

| Failure | Behavior in Phase 2 | Fixed in |
| --- | --- | --- |
| Publish fails after DB commit | Event lost silently; log error | Phase 5 (outbox) |
| Duplicate delivery (redelivery after visibility timeout) | Possible duplicate email; `ApproximateReceiveCount` logged | Phase 4 (idempotency) |
| Consumer keeps failing | Message moves to DLQ after `maxReceiveCount`; DLQ depth visible | Phase 4 (triage/repair) |
| Emulator down at startup | Service health reflects broker dependency; graceful reconnect | Phase 10 (connection recovery hardening) |
| SES rejects a send | Record marked failed; message redelivered per visibility timeout | Phase 4 (backoff classification) |
| Process killed mid-message | Message redelivered after visibility timeout | Handled from Phase 2 by design |

No Phase 2 acceptance criterion depends on fixing these; all of them must be
**demonstrable**, because Phases 4, 5, and 10 exist to solve them.

## 7. Explicitly Out of Scope

- Retries with exponential backoff, failure classification, and DLQ
  re-drive tooling (Phase 4).
- Transactional outbox (Phase 5).
- Consumer-side idempotency guarantees (Phase 4). This includes **any**
  duplicate suppression: no unique index on event id, no dedupe check before
  sending, no conditional writes. Phase 2 must let duplicates through and make
  them visible.
- MFA-by-email, email verification, password reset emails (Phase 12 Security).
- SNS. EventBridge fan-out covers Phase 2 needs; SNS as an additional pattern
  (e.g. `SQS fan-out via SNS`) may be explored in Phase 14/15 notes but is not
  required here.
- Lambda-hosted consumers; all Luna workers remain containers.
- FIFO queues.
- Asynchronous checkout or moving payment authorization off the request path.
- RabbitMQ in any form.
- The emulator web UI in any deployed environment. It is a local development
  tool only.

## 8. Acceptance Criteria

1. `docker compose up` (dev profile) starts the emulator healthy;
   `terraform plan` reports no changes on a second run; and `terraform apply`
   after recreating the emulator container restores every declared resource.
2. Placing an order returns synchronously with the same contract as Phase 1,
   and `OrderConfirmed` appears on `luna-bus` (verifiable through the AWS CLI or
   the emulator UI).
3. The Notification service receives the order confirmation message from its
   queue and persists a sent-email record; the email content is verifiable via
   `GET /_aws/ses`, the emulator UI mailbox, and the Luna Ops Emails view.
4. Advancing a shipment through `Created` -> `InTransit` -> `Delivered`
   produces the corresponding emails without any additional manual trigger.
5. Two Notification instances run concurrently; a burst of messages is split
   across them (observable in logs/traces), with no message lost.
6. Stopping the Notification service causes messages to accumulate in the
   queue; restarting it drains the backlog without operator intervention.
7. Forcing a poison message (consumer that always throws) results in the
   message landing in the DLQ after `maxReceiveCount`, and DLQ depth is
   visible in the existing service/observability surfaces.
8. Killing a Notification instance mid-processing results in the in-flight
   message being redelivered to the surviving instance.
9. Duplicate delivery demonstrates the pitfall end to end: a redelivered message
   (receive count > 1) produces a **second email and a second sent-email row**
   for the same event id, both visible in the Luna Ops Emails view with their
   differing receive counts. Nothing suppresses the duplicate in Phase 2; the
   fix belongs to Phase 4 and must not pre-empt it.
10. Checkout, cart, catalog, and all Phase 1 integration tests still pass
    unchanged.
11. The customer order page updates shipment status via polling without a
    manual reload.
12. The [AWS mapping contract](architecture.md#the-aws-mapping-contract)
    covers every new component introduced by this phase.
13. The deployed server stack runs the same pipeline end to end: Dokploy
    deploys the emulator as part of Luna's production compose, Terraform is
    applied during deployment, and an order placed against the deployed
    application produces a confirmation email through the deployed Notification
    workers. The emulator UI is **not** deployed; only the emulator is.

## 9. Testing

- **Unit:** template rendering per event type; consumer handler success/failure
  paths; event envelope construction; sent-email record creation.
- **Integration (Testcontainers, Floci image):** publish an event to the bus and
  assert the consumer produced a sent-email record; assert rule routing
  delivers only matching events to each queue; assert DLQ behavior for a
  failing consumer; assert that redelivering the same message produces two
  sent-email rows for one event id (the documented Phase 2 pitfall). A
  `testcontainers-floci-dotnet` module exists but is lightly used; a plain
  `GenericContainer` on the pinned `floci/floci:<version>-compat` image waiting
  for `/_floci/health` is the lower-risk option and needs no extra dependency.
  Re-evaluate the module once it matures.
- **Integration (existing):** full Phase 1 suites remain green.
- **Compose smoke:** emulator healthy, `terraform plan` clean on a second run,
  one end-to-end order -> confirmation email pass — verified against the local stack and,
  for [criterion 13](#8-acceptance-criteria), against the deployed production
  compose.
- **Frontend:** order page polling behavior; Luna Ops Emails view loading,
  empty, and error states.

## 10. Documentation Updates Required

Per `AGENTS.md`, documentation and implementation must not diverge. This phase
requires updating the existing RabbitMQ direction recorded in:

- `documentation/roadmap.md`: "Why Build Luna?" bullet list; the End Goal
  diagram note ("RabbitMQ connects..."); Operations Console RabbitMQ lines;
  Communication and Reliability Patterns diagrams; Technology table
  (`Messaging | RabbitMQ`); Phase 2 goal/milestone; Phase 13 (Testcontainers
  RabbitMQ); Phase 14 infrastructure list; Phase 15 mapping table
  (`RabbitMQ -> SQS` becomes `Floci EventBridge/SQS -> EventBridge/SQS`);
  Phase 16 outage scenarios; Project Status next steps.
- `documentation/dev_phases/phase-0/design.md`: lines referring to RabbitMQ
  arriving in Phase 2 (including the numbered decision list).
- `documentation/dev_phases/phase-1/architecture.md`: the three RabbitMQ
  deferral statements.
- `documentation/dev_phases/phase-1/spec.md` and
  `documentation/dev_phases/phase-1/ops-architecture.md`: out-of-scope lists
  mentioning RabbitMQ.
- `README.md`: the "RabbitMQ is intentionally not part of Phase 0" note.
- `documentation/observability.md`: if it references RabbitMQ metrics, add
  queue-depth/DLQ metrics for SQS instead.
- `infrastructure/README.md` and `.env.example`: document the `floci`
  service, the local-only `floci-ui` service, the Terraform configuration and
  how it is applied, and the emulator settings the Dokploy application
  environment must carry. There is no auth token to document.

## 11. Implementation Checklist

### Infrastructure

- [ ] `floci` service in `docker-compose.yml` **and**
      `docker-compose.prod.yml`, pinned `-compat` tag, `FLOCI_HOSTNAME`,
      dummy AWS credentials, and a `/_floci/health` health check (internal
      network only in production)
- [ ] `floci-ui` in `docker-compose.dev.yml` only, on port `4500`; not in the
      production compose
- [x] Emulator service, health check, and dev UI added to the local stack and
      verified by hand on 2026-09-28 (bus, rule, SQS fan-out, receive counts,
      DLQ redrive, SES v1 send, `/_aws/ses` mailbox)
- [x] Terraform configuration for the bus, queues, DLQs, rules, and SES identity
      applied and verified: a second `terraform plan` reports no changes, and
      publishing `OrderConfirmed` and `ShipmentInTransit` fans out to the correct
      separate queues
- [ ] Terraform configuration for the bus, queues, DLQs, rules, and SES
      identity, applied at every environment startup and deployment
- [ ] Terraform state and plan files excluded from version control; the provider
      lock file committed
- [ ] Dev stack runs two Notification instances; production compose runs two
      Notification replicas

### Messaging plumbing

- [ ] `AWSSDK.EventBridge` / `AWSSDK.SQS` / `AWSSDK.SES` package references
- [ ] Infrastructure-layer publisher abstraction for `PutEvents`
- [ ] Infrastructure-layer SQS receiver implementing
      [SPEC-MSG-005](architecture.md#spec-msg-005---consumer-loop)
- [ ] Correlation id propagated into event detail

### Orders and Shipping

- [ ] `OrderConfirmed` published after commit
- [ ] Shipment lifecycle events published after commit


### Notification service

- [ ] Five-project service skeleton + `NotificationDb` migrations + health
- [ ] Consumer hosted service for both queues
- [ ] Three email templates + rendering
- [ ] `IEmailSender` with SES implementation
- [ ] Sent-email persistence (event id recorded, deliberately **no** unique
      index in Phase 2)
- [ ] Internal read API + Luna Ops Emails view

### Frontend

- [ ] Order details polling for async status updates
- [ ] Luna Ops Emails view (loading/empty/error states)

### Tests and quality gate

- [ ] Unit tests (templates, handlers, envelope)
- [ ] Testcontainers integration tests against the pinned Floci image
- [ ] Compose smoke including a clean `terraform plan` on a second run
- [ ] Full recorded quality gate run

### Documentation

- [ ] Apply every update in [Section 10](#10-documentation-updates-required)
- [ ] Add this phase's decisions (bus vs. queue, no FIFO, PII trade-off,
      deliberate dual-write) to the roadmap's decision notes
