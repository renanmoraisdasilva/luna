# Luna

### Distributed Commerce & Logistics Simulation

Luna is a production-inspired commerce and logistics platform built to explore the engineering challenges behind distributed systems.

It simulates the complete lifecycle of an order, from checkout and payment through inventory, fulfillment, shipping, and delivery, while intentionally introducing asynchronous processing, failures, delays, and service outages.

The goal is to build a system that behaves like a real distributed platform.

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