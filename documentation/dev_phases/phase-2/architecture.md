# Phase 2 - Messaging and Async Workflows Architecture

> Companion document: [phase 2 specification](spec.md) records **what** Phase 2
> must do (behavior, failure model, acceptance criteria). This document records
> **how** it is built: the broker decision, the AWS mapping contract, messaging
> mechanics, infrastructure, and service structure — the split defined in
> `AGENTS.md` and matching the Phase 0 design / Phase 1 architecture precedent.

## Contents

- [Purpose and relationship to the specification](#purpose-and-relationship-to-the-specification)
- [Decision Record: LocalStack (EventBridge + SQS + SES) instead of RabbitMQ](#decision-record-localstack-eventbridge--sqs--ses-instead-of-rabbitmq)
- [The AWS Mapping Contract](#the-aws-mapping-contract)
- [1. Messaging Principles](#1-messaging-principles)
- [2. Infrastructure](#2-infrastructure)
- [3. Messaging Model](#3-messaging-model)
- [4. Notification Service Structure](#4-notification-service-structure)
- [5. Architectural Decisions Summary](#5-architectural-decisions-summary)

## Purpose and relationship to the specification

This document owns every mechanism-level requirement in Phase 2:
`SPEC-INFRA-001`-`003`, `SPEC-MSG-001`-`009`, and
`SPEC-NTF-001/003/004`. Behavior-level requirements (`SPEC-MSG-010`-`012`,
`SPEC-NTF-002/005/006`, `SPEC-FE-P2-*`) live in
[phase 2 specification](spec.md).

Acceptance of everything in this document is exercised through the
specification's [acceptance criteria](spec.md#8-acceptance-criteria),
most directly criterion 12 (every component appears in the AWS mapping
contract below).

Phase 2 is **not local-only**. It ships to both current deployment targets —
the local Compose stack and the self-hosted server that Dokploy deploys from
`infrastructure/docker-compose.prod.yml` — because that server is a personal
learning environment, not a public production system. The full asynchronous
pipeline (event bus, queues, competing consumers, email delivery) runs in the
deployed stack exactly as it does locally; AWS remains the third target in
Phase 15.

## Decision Record: LocalStack (EventBridge + SQS + SES) instead of RabbitMQ

**Status:** Accepted (supersedes the RabbitMQ direction recorded in `roadmap.md`
and the Phase 0/1 architecture documents).

**Decision:** Phase 2 runs on **LocalStack** emulating three AWS services locally:

| Role | Local (LocalStack) | AWS |
| --- | --- | --- |
| Event bus / routing | EventBridge | EventBridge |
| Durable work queues | SQS (standard + DLQ) | SQS |
| Email delivery | SES (v1 API) | SES |

**Rationale:**

- The project's primary learning goal is AWS. A RabbitMQ architecture
  (exchanges, bindings, routing keys) must later be re-learned as
  EventBridge rules and SNS/SQS fan-out. LocalStack removes that translation.
- The AWS SDK for .NET is itself a skill worth learning; Luna services talk to
  LocalStack with `AWSSDK.EventBridge`, `AWSSDK.SQS`, and `AWSSDK.SES` exactly as
  they would in AWS.
- Phase 15 becomes "same code, real endpoints, add Terraform" instead of a
  messaging rewrite.
- LocalStack is usable in CI and under the free **Hobby** plan for every service
  this phase needs (verified against the LocalStack licensing table:
  SQS, SNS, EventBridge, SES v1, Step Functions, S3, Lambda, CloudWatch, Secrets
  Manager are all included in Hobby).

**Consequences and caveats (accepted deliberately):**

- Luna loses RabbitMQ-specific literacy (exchanges, consumer acknowledgements).
  The transferable concepts (durable queue, competing consumers, at-least-once
  delivery, visibility timeout vs. acknowledgement, DLQ) are all still learned.
- LocalStack requires a free Hobby auth token (`LOCALSTACK_AUTH_TOKEN`) supplied
  through environment configuration — `.env` locally, the Dokploy application
  environment on the server. Tokens are never committed.
- LocalStack is a **Luna-owned deployment component**, not a `server-infra`
  platform service. It ships inside Luna's own Compose files (local and
  production) and is deployed by Dokploy like every other Luna container, so
  the `server-infra` repository needs no changes for Phase 2.
- Hobby does **not** emulate ECS, RDS, or ELB. This does not affect Phase 2:
  Luna itself still runs in Docker Compose; only messaging and email are
  emulated. Those services become real (not emulated) in Phases 14-15.
- SES on the Hobby plan covers the **v1** API (`SendEmail`). LocalStack SMTP
  forwarding (to a Mailpit-style inbox) is a paid-tier feature; local email
  inspection therefore uses the `/_aws/ses` endpoint and Luna's own sent-email
  records, not an SMTP preview tool.

**Rejected alternative:** RabbitMQ as a "messaging laboratory" with translation
to AWS later. Rejected because it optimizes for generic messaging literacy at
the cost of the stated primary goal, and because the roadmap's own Phase 15
mapping (`RabbitMQ -> SQS`) already understates what AWS actually requires
(`EventBridge + SQS`, not a bare queue).

## The AWS Mapping Contract

Every Phase 2 element has exactly one AWS destination. This table is the
contract; new Phase 2 components must be added to it before acceptance
(specification [criterion 12](spec.md#8-acceptance-criteria)).

**Deployment environments:** Phase 2 runs in two environments today — local
Compose, and the self-hosted server stack (Dokploy reads
`infrastructure/docker-compose.prod.yml`, pulls GHCR images by commit SHA, and
injects the application environment). The component set is identical in both,
so one mapping covers them; AWS is the third environment in Phase 15.

| Luna (local) | AWS | Notes |
| --- | --- | --- |
| LocalStack EventBridge bus (`luna-bus`) | EventBridge custom event bus | Same `PutEvents` API |
| EventBridge rules | EventBridge rules | Same rule pattern syntax |
| LocalStack SQS queues + DLQs | SQS queues + DLQs | Same queue attributes |
| Notification worker container | ECS Fargate task (or Lambda) | Long-polling loop is portable |
| LocalStack SES `SendEmail` | SES `SendEmail` | Same API, same request shape |
| Sent-email inspection `GET /_aws/ses` | SES console / `ListReceiptFilters`-era tooling | Local-only inspection aid |
| SQL Server container (`NotificationDb`) | RDS (SQL Server) | Same EF Core migrations |
| Docker Compose service definition | ECS task definition | Phase 14/15 concern |
| `.env` / compose environment | Secrets Manager | Phase 14/15 concern |
| OpenTelemetry -> SigNoz (existing) | OTel -> CloudWatch (ADOT) + X-Ray | Existing Luna telemetry |
| Seed script (`scripts/`, AWS CLI) | Terraform | Phase 14 replaces script with IaC |

**Messaging pattern mapping (conceptual):**

| Pattern in Luna | AWS pattern |
| --- | --- |
| "Something happened, tell interested services" | EventBridge rule -> N SQS queues (fan-out) |
| "Someone needs to do this work" | Direct SQS queue (command) |
| "Scale out the workers" | Competing consumers on one SQS queue |
| "Don't lose the event if the DB commit fails" | Transactional outbox -> `PutEvents` (Phase 5) |
| "Retry a failing consumer" | Visibility timeout + redrive to DLQ (Phase 4) |

## 1. Messaging Principles

1. **Messaging complements HTTP; it does not replace it.** Checkout, cart,
   catalog reads, and anything needing an immediate response stay synchronous.
2. **Queue where AWS uses a queue, publish to the bus where AWS uses the bus.**
   Domain events go to EventBridge; work items go directly to SQS.
3. **One queue per consumer.** Each consumer owns its queue; no shared queues
   across services. Fan-out is the bus's job, not the consumers'.
4. **At-least-once delivery.** Every consumer knows duplicates are possible
   from day one and makes them observable (receive counts, duplicate rows).
   Suppressing them is Phase 4 work and must not be pre-empted.
5. **Events are self-contained enough for notification.** Consumers must not
   have to call back into the producer to render a message. PII minimization
   trade-offs are documented at the event, not ignored.
6. **No silent dual-write claims.** Phase 2 publishes directly after the
   database commit. The known risk (commit succeeds, publish fails, event lost)
   is documented and deliberately deferred to the Phase 5 outbox.

## 2. Infrastructure

### SPEC-INFRA-001 - LocalStack service

The `localstack` service is added to **both** deployment definitions:
`infrastructure/docker-compose.yml` (base local stack, with
`docker-compose.dev.yml`) and `infrastructure/docker-compose.prod.yml` (the
production definition Dokploy reads directly from this repository):

- Image pinned to a specific LocalStack version tag.
- Services enabled: `sqs,sns,events,ses`.
- Port `4566` exposed on the host locally for inspection and tooling; in the
  production definition it stays internal to the Compose network and is not
  published on the server.
- `LOCALSTACK_AUTH_TOKEN` supplied via the existing environment-file pattern
  locally and through the Dokploy application environment on the server;
  never committed to source control.
- Health check wired into Compose health status so dependent services start
  after LocalStack is ready, in both environments.

### SPEC-INFRA-002 - Idempotent infrastructure seed

A script under `scripts/` runs as a **one-shot init service** in both Compose
stacks (starting after LocalStack is healthy), and is also invoked manually
and by CI. It uses the AWS CLI
(`--endpoint-url localhost:4566`) to create, and re-create without error:

- The custom event bus `luna-bus`.
- The queues defined in [Section 3](#3-messaging-model), each with a matching
  DLQ and redrive policy.
- The EventBridge rules routing bus events to queues.
- The verified SES identity used as the sender (LocalStack verifies
  identities immediately).

Idempotency is required: running the seed twice must not fail or duplicate
resources. Because the script runs as the startup/deploy step in both
environments, every fresh deployment arrives with the bus, queues, rules, and
sender identity already provisioned, and a wiped LocalStack state repairs
itself on the next start.

### SPEC-INFRA-003 - Service configuration

Services configured for AWS SDK access use the standard AWS configuration
surface so the same code works against LocalStack and AWS:

- `ServiceURL` (e.g. `http://localstack:4566` locally, unset in AWS),
  `Region`, and dummy static credentials locally.
- On the server these values come from the Dokploy application environment;
  container DNS, region, and credentials are identical in both environments,
  so there is no configuration fork between local and deployed.
- No LocalStack-specific branching in application code outside the
  Infrastructure layer's external-client wiring.

## 3. Messaging Model

### SPEC-MSG-001 - Events use the bus, commands use queues

- **Events** (`OrderConfirmed`, `Shipment*`) are published to the EventBridge
  custom bus `luna-bus` with `PutEvents`.
- **Commands** (explicit work requests, e.g. create a shipment) are sent
  directly to the SQS queue owned by the service that performs the work.
- Producers never send events directly to another service's queue unless that
  queue is the target of a rule; routing decisions live with the bus.

### SPEC-MSG-002 - Queue inventory

| Queue | Owner/consumer | Fed by |
| --- | --- | --- |
| `luna-notification-orders` | Notification (order emails) | Rule on `OrderConfirmed` |
| `luna-notification-shipments` | Notification (shipment emails) | Rules on `ShipmentCreated`, `ShipmentInTransit`, `ShipmentDelivered` |
| `luna-orders-shipment-commands` (Phase 3, not provisioned here) | Orders/Shipping (shipment creation) | Direct `SendMessage` from Luna Ops |
| `*.DLQ` (one per Phase 2 queue) | Operators (Phase 4 handles) | Redrive policy |

The Phase 2 seed provisions the two Notification queues and their DLQs only.
The command queue is created in Phase 3; see
[SPEC-MSG-009](#spec-msg-009---command-queue-queue-without-a-bus-deferred-to-phase-3).

All queues are **standard** (non-FIFO). FIFO is explicitly rejected: AWS
guideline practice is to prefer standard queues plus consumer idempotency, and
Luna wants to learn that discipline rather than lean on broker-enforced
ordering. Ordering is not a business requirement for these workflows.

### SPEC-MSG-003 - Event envelope

Events use the EventBridge envelope so the local and AWS payloads are identical:

```json
{
  "version": "1.0",
  "id": "uuid",
  "source": "luna.orders",
  "detail-type": "OrderConfirmed",
  "account": "000000000000",
  "time": "2026-01-01T00:00:00Z",
  "region": "us-east-1",
  "resources": [],
  "detail": {
    "orderId": "uuid",
    "customerId": "uuid",
    "customerEmail": "customer@example.com",
    "orderTotal": 129.90,
    "currency": "USD",
    "correlationId": "uuid-or-trace-id"
  }
}
```

Rules:

- `source` follows `luna.{service}`; `detail-type` is the event name.
- `correlationId` carries the existing Luna correlation/trace identifier so
  SigNoz can stitch the async workflow to the originating request.
- `detail` is versioned by adding fields only; removing or renaming a field is
  a breaking change requiring a new `detail-type` suffix or documented schema
  version (full schema evolution is Phase 16).

The four events carried in this envelope in Phase 2 are catalogued in the
specification's [event catalog](spec.md#2-event-catalog-phase-2).

### SPEC-MSG-004 - Routing rules

EventBridge rules on `luna-bus`:

- `OrderConfirmed` -> `luna-notification-orders`
- `ShipmentCreated`, `ShipmentInTransit`, `ShipmentDelivered` -> `luna-notification-shipments`

Rules target SQS queues directly (EventBridge's supported target type for SQS).
Adding a new consumer later means adding a rule, not changing the producer.

### SPEC-MSG-005 - Consumer loop

Every consumer (this phase: Notification) implements the SQS worker contract:

1. Long-poll `ReceiveMessage` (explicit wait time, batch size 1..10).
2. Process the message.
3. On success, `DeleteMessage`.
4. On failure, do **not** delete; the message becomes visible again after the
   visibility timeout and is redelivered.
5. Log every delivery attempt count (`ApproximateReceiveCount`) so duplicate
   and retry behavior is visible in SigNoz from day one.

Graceful shutdown: stop receiving, finish in-flight messages, then exit.

### SPEC-MSG-006 - Competing consumers

The Notification worker must run as **at least two instances** in the
development stack (`docker-compose.dev.yml`) and as two replicas in the
deployed production stack, all polling `luna-notification-orders`. A message is
processed by exactly one instance per delivery, so competing consumers behave
identically on the server and locally. This is Phase 2's horizontal
scaling lesson; Phase 10 extends redundancy to every service.

### SPEC-MSG-007 - Dead-letter queues

Each queue has a DLQ and a redrive policy (`maxReceiveCount` small, e.g. 3-5,
so failures surface quickly during learning).

Phase 2 requires: DLQs exist, messages land in them when a consumer keeps
failing, and the DLQ depth is observable. **Handling** DLQ content (triage,
re-drive tooling, backoff schedules) is Phase 4 work.

### SPEC-MSG-008 - Direct publish (deliberate, documented)

`Orders` and `Shipping` publish events immediately after a successful database
commit, in application code. This creates the classic dual-write window (commit
succeeds, publish fails, or process dies in between).

This is intentional: Phase 2 must make the problem observable so the Phase 5
transactional outbox solves a problem the project has actually experienced.
The limitation is recorded in the specification's
[failure model](spec.md#6-failure-model) and must not be
described anywhere as "reliable publishing."

### SPEC-MSG-009 - Command queue (queue without a bus), deferred to Phase 3

**Status: deferred out of Phase 2.** The design is unchanged; only the timing
moved. Phase 2 does not provision this queue, does not seed its DLQ, and does
not require it for acceptance. It is implemented at the start of Phase 3, as
recorded in the roadmap's Phase 3 goal and milestone.

Luna Ops "create shipment" moves from a synchronous
Orders-coordinated command to `SendMessage` on `luna-orders-shipment-commands`:

- The Ops UI shows a requested state immediately after the command is enqueued.
- The consumer performs the shipment creation and advances the order state.
- The Ops UI reflects the result by refreshing order state, not by awaiting a
  synchronous response.

This exists to teach the SQS-as-work-queue pattern on its own, distinct from
bus fan-out.

## 4. Notification Service Structure

The Notification service is a new bounded context following the standard
five-project template (`Notification.Api`, `Notification.Application`,
`Notification.Contracts`, `Notification.Domain`, `Notification.Infrastructure`)
with its own `NotificationDb` and EF Core migrations. Services do not access
`NotificationDb` directly.

Behavior requirements for this service (templates, visibility endpoint, no
blocking calls, duplicate visibility) are in the specification's
[Notification Service](spec.md#4-notification-service) section.

### SPEC-NTF-001 - Queue consumer

Notification runs a hosted background consumer implementing
[SPEC-MSG-005](#spec-msg-005---consumer-loop) against its two queues, with
graceful shutdown and health reporting consistent with Luna's existing health
endpoints.

### SPEC-NTF-003 - SES sender

An `IEmailSender` abstraction in the Application layer with an Infrastructure
implementation using `AWSSDK.SES` `SendEmail`:

- Locally targets LocalStack SES; in AWS it targets SES unchanged.
- The sender identity is configured by environment (`From` address).
- SES rejections (throttling, invalid identity) surface as consumer failures so
  they follow the retry path rather than being swallowed.

### SPEC-NTF-004 - Sent-email record

Every send attempt (success or failure) is persisted in `NotificationDb`:

- Event id, message id (SES `MessageId`), recipient, subject, template type,
  correlation id, delivery attempt count (`ApproximateReceiveCount`), status,
  SES error if any, timestamp.

This gives Luna a provider-independent record of what was emailed and powers
the visibility endpoint.

**Deliberately no uniqueness constraint in Phase 2.** There is no unique index
on event id and no dedupe check before sending. When SQS redelivers a message,
Notification sends the email again and persists a second row. The duplicate is
the lesson: it must be visible in the Luna Ops Emails view and attributable via
the recorded receive count. Phase 4 introduces the unique index (and the
at-least-once consumer discipline around it) as the fix for a pitfall this
phase has actually demonstrated, not as a precaution that hides it.

## 5. Architectural Decisions Summary

| Decision | Where |
| --- | --- |
| LocalStack (EventBridge + SQS + SES) instead of RabbitMQ | [Decision Record](#decision-record-localstack-eventbridge--sqs--ses-instead-of-rabbitmq) |
| Standard queues only; FIFO rejected | [SPEC-MSG-002](#spec-msg-002---queue-inventory) |
| Events to the bus, commands to queues | [SPEC-MSG-001](#spec-msg-001---events-use-the-bus-commands-use-queues) |
| One queue per consumer; fan-out via rules | [SPEC-MSG-004](#spec-msg-004---routing-rules) |
| Direct publish after commit (dual-write, fixed in Phase 5) | [SPEC-MSG-008](#spec-msg-008---direct-publish-deliberate-documented) |
| No duplicate suppression in Phase 2 (fixed in Phase 4) | [SPEC-NTF-004](#spec-ntf-004---sent-email-record) |
| `customerEmail` carried in event details (PII trade-off) | Specification [event catalog](spec.md#2-event-catalog-phase-2) |
| Checkout remains synchronous | Specification [SPEC-MSG-012](spec.md#spec-msg-012---checkout-stays-synchronous) |
| Ops shipment command queue deferred to Phase 3 | [SPEC-MSG-009](#spec-msg-009---command-queue-queue-without-a-bus-deferred-to-phase-3) |
