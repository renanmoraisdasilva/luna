# Luna

### Distributed Commerce & Logistics Simulation

Luna is a production-inspired commerce and logistics platform built to explore the engineering challenges behind distributed systems.

It simulates the complete lifecycle of an order, from checkout and payment through inventory, fulfillment, shipping, and delivery, while intentionally introducing asynchronous processing, failures, delays, and service outages.

The goal is to build a system that behaves like a real distributed platform.

## Run Phase 0

Prerequisites: Docker Desktop.

```bash
docker compose -f infrastructure/docker-compose.yml up --build
```

Open the storefront at `http://localhost:3000`. Unified Swagger UI is available at `http://localhost:3000/swagger`. The backend services remain private to the Compose network and are reached through the frontend gateway.
SQL Server is configured with one database per service. RabbitMQ is intentionally not included until Phase 2.

See [Phase 0 Design](documentation/phase-0-design.md) and [Roadmap](documentation/roadmap.md) for the architectural decisions and progression.

---

## What is Luna?

A customer can browse products, place orders, choose shipping options, and track deliveries.

Behind the scenes, the order moves through several independently running services:

```text
Customer
   │
   ▼
 Orders
   │
   ├──────────────► Payments
   │
   └──────────────► Inventory
                         │
                         ▼
                    Fulfillment
                         │
                         ▼
                     Shipping
                         │
                         ▼
                 Simulated Carrier