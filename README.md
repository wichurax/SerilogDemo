# OpenTelemetry E-Commerce Demo

This repository is a local, Docker-first demo of a moderately distributed e-commerce system instrumented with OpenTelemetry. The goal is to keep the domain simple while still showing realistic trace, log, metric, and service-boundary examples.

Main branch is the starting point. Individual blog-post states live on separate branches.

## What This Repo Demonstrates

- A simple web app and main e-commerce API behind Nginx
- Finite warehouse inventory with reservation, shipping deduction, and restock flows
- A synchronous payment hop for clear end-to-end traces
- An asynchronous RabbitMQ fan-out for eventually consistent side effects
- Correlated traces, logs, and metrics across multiple .NET services
- A fully local observability stack with Grafana, Loki, Tempo, Prometheus, and PostgreSQL

## Architecture

```mermaid
flowchart TB
  User[App in Browser] --> LB[Nginx Load Balancer<br/>:8080]
  K6[k6 Automated Clients] --> LB
  LB --> API[Main API]
  API --> PGW[Payment Gateway]
  API --> DB[(PostgreSQL)]
  PGW --> DB
  API --> RMQ[(RabbitMQ)]
  RMQ --> NOTIFY[Notifications API]
  RMQ --> FULFILL[Fulfillment API]
  NOTIFY --> DB
  FULFILL --> DB

  API --> OTel[OpenTelemetry Collector<br/>:4318]
  PGW --> OTel
  NOTIFY --> OTel
  FULFILL --> OTel
  OTel --> Loki[Loki]
  OTel --> Tempo[Tempo]
  OTel --> Prom[Prometheus]
  Loki --> Grafana[Grafana]
  Tempo --> Grafana
  Prom --> Grafana

```

## Main Flow

1. A user opens the web app in a browser.
2. The web app calls the main API and Fulfillment API through the Nginx entrypoint.
3. The main API validates cart and order data.
4. The main API calls the Payment Gateway synchronously.
5. On success, the main API stores an outbox record.
6. The outbox publisher sends an `order.paid` event to RabbitMQ.
7. Notifications API consumes the same event on separate email and SMS queues, while Fulfillment API consumes it on its own queue.
8. Notifications API resolves fake user contact preferences, logs fake email or SMS payloads when the channel is enabled, and persists one delivery record per channel.
9. Fulfillment API publishes `fulfillment.progress`, and the main API projects those updates back onto order status, fulfillment details, and warehouse stock.

This gives one manual browser path, matching automated k6 client paths, and multiple asynchronous, broker-backed follow-up paths. Fulfillment progression stays manual by default, and the warehouse-worker k6 script can automate collect, pack, and ship during load runs.

## Runnable Components

- Main API: this README and [SerilogDemo.http](SerilogDemo.http)
- Payment Gateway: [PaymentGateway/README.md](PaymentGateway/README.md)
- Notifications API: [NotificationsApi/README.md](NotificationsApi/README.md)
- Fulfillment API: [FulfillmentApi/README.md](FulfillmentApi/README.md)
- Log Search Tools: [LogSearchTools/README.md](LogSearchTools/README.md)
- Observability Demo Queries: [observability/demo-queries.md](observability/demo-queries.md)
- Workshop Script: [observability/workshop-script.md](observability/workshop-script.md)

## Main API

The main API is the front door of the system. It owns the catalog, cart, delivery and payment-option lookup, warehouse inventory commands, order placement, payment orchestration, outbox publication, and fulfillment progress projection.

### Main API Features

- Catalog, cart, delivery, payment-option, and order endpoints
- Warehouse inventory read and adjustment endpoints for restock, write-off, and recount workflows
- Checkout orchestration with manual business spans
- Synchronous Payment Gateway integration
- Outbox persistence and RabbitMQ publishing for `order.paid`
- RabbitMQ consumption for `fulfillment.progress` to keep order and inventory projections current
- RabbitMQ fan-out to notification and fulfillment consumers with propagated trace context
- Structured logs and custom metrics for checkout flow

### Main API Required Configuration

- `ConnectionStrings__Postgres`: PostgreSQL connection string
- `PaymentGateway__BaseUrl`: Payment Gateway base URL
- `RabbitMq__HostName`: RabbitMQ host
- `RabbitMq__Port`: RabbitMQ port
- `RabbitMq__UserName`: RabbitMQ username
- `RabbitMq__Password`: RabbitMQ password
- `RabbitMq__VirtualHost`: RabbitMQ virtual host
- `RabbitMq__PublishEnabled`: enables or disables outbox publishing
- `RabbitMq__PublishIntervalSeconds`: outbox polling interval
- `Warehouse__DefaultWarehouseName`: logical warehouse used by inventory endpoints and projections
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `serilogdemo-api`

Default local values are in [appsettings.json](appsettings.json).

## Quick Start

### Prerequisites

- Docker with `docker compose`
- .NET 8 SDK for local development
- Bun is required for the default host-based UI development flow.

### Start Everything

```bash
# Start full app (frontend and backend).
ENABLE_DOCKER_UI=1 docker compose --profile ui-build up -d

# Start the backend stack. The UI is not built by compose in this mode.
docker compose up -d

# Scalable main API with 3 instances behind Nginx
docker compose up -d --scale api=3

# Load test profile with 5 API instances plus ecommerce, warehouse, and restock k6 workers
docker compose --profile loadtest up -d --scale api=5
```

### UI Modes

- Default development flow: start the backend stack with `docker compose up -d`, then run `bun run dev` from `UI/` and open `http://localhost:5173` for hot reload.
- Optional containerized UI flow: run `ENABLE_DOCKER_UI=1 docker compose --profile ui-build up -d` and open `http://localhost:8080`.
- If you start compose without the opt-in UI flag, `http://localhost:8080/` returns a short message telling you to use the host dev server instead.

### Access Points

- UI (for `bun run dev` setup): `http://localhost:5173`
- UI (for docker compose setup): `http://localhost:8080`
- API: `http://localhost:8080/api`
- Swagger UI: `http://localhost:8080/swagger`
- Grafana: `http://localhost:3001`
- RabbitMQ Management: `http://localhost:15672`

## Request Samples

- Main API requests: [SerilogDemo.http](SerilogDemo.http)
- Payment Gateway requests: [PaymentGateway/PaymentGateway.http](PaymentGateway/PaymentGateway.http)

The main API order endpoint accepts the `X-Payment-Scenario` header for deterministic payment demos. Supported explicit values are `Success`, `Decline`, `SlowSuccess`, and `Timeout`; when the header is omitted, the backend defaults to `Success`.

## Load Test Model

This repo now uses three separate k6 scripts:

- [k6/load-test.js](k6/load-test.js): ecommerce users hitting the main API through Nginx with browse, cart, checkout, and order-status polling behavior.
- [k6/load-test-warehouse.js](k6/load-test-warehouse.js): warehouse workers hitting the Fulfillment API directly to collect, pack, ship, and inspect fulfillment backlog.
- [k6/load-test-restock.js](k6/load-test-restock.js): a replenishment worker that watches warehouse availability and tops stock back up through the existing warehouse API when items fall below a configurable low-water mark.

Run all three with the `loadtest` profile when you want end-to-end automated load with continuing order creation. Run only the ecommerce script when you want backlog to accumulate for manual fulfillment demos.

The restock worker keeps the test aligned with the business process: shipped orders still deduct real stock, and incoming replenishment restores availability instead of relying on unrealistically large seed inventory or bypassing reservation rules.

Useful restock environment variables for the `loadtest` profile:

- `RESTOCK_LOW_WATER_MARK`: when available quantity at or below this threshold becomes a replenishment candidate.
- `RESTOCK_TARGET_AVAILABLE_QUANTITY`: target available quantity to restore after replenishment.
- `RESTOCK_MAX_ITEMS_PER_CYCLE`: maximum number of items replenished per worker loop.
- `RESTOCK_POLL_SECONDS`: pause between replenishment cycles.
- `RESTOCK_CATEGORY_FILTER`: optional comma-separated category list to constrain replenishment.

## Stopping Services

```bash
docker compose down
docker compose --profile loadtest down
docker compose --profile loadtest down -v
```

## Observability Notes

- All services export OTLP data through the OpenTelemetry Collector.
- The `order.paid` message carries W3C trace headers so the async consumers continue the producer trace.
- The demo is designed for trace-to-log correlation across service boundaries.
- The main API produces the most visible business spans around checkout.
- The Notifications API demonstrates independent fake email and fake SMS handlers after the HTTP request has already completed.
- The Fulfillment API demonstrates a second independent consumer on the same event and exposes manual workflow controls.
- The warehouse-worker k6 script can drain fulfillment backlog during load runs without becoming part of the runtime architecture.
- The restock-worker k6 script keeps downstream traffic steady by replenishing finite warehouse stock through the same business API operators would use.

## Notifications API Schema

Notifications API owns the `notification_service` schema in PostgreSQL.

- `NotificationUsers`: fake recipient contact data and channel preferences, seeded with 10 demo users.
- `EmailNotificationDeliveries`: one record per handled email notification event, including destination, status, failure reason, and processed timestamp.
- `SmsNotificationDeliveries`: one record per handled SMS notification event, including destination, status, failure reason, and processed timestamp.

If a `UserId` from `order.paid` does not exist in `NotificationUsers`, Notifications API synthesizes a fake email address and phone number for that event and treats both channels as enabled. The migration that introduces this schema replaces the earlier `NotificationAttempts` table.

## Project Structure

```
SerilogDemo/
├── Controllers/                # Main API controllers
├── Data/                       # Main API DbContext and seeding
├── DTOs/                       # Main API contracts
├── Models/                     # Main API domain entities
├── Migrations/                 # Main API EF Core migrations
├── PaymentGateway/             # Synchronous payment service
├── PaymentGateway.Contracts/   # Shared payment contracts
├── NotificationsApi/           # Notification API host and async consumer
├── FulfillmentApi/             # Fulfillment API host and async consumer
├── SerilogDemo.Messaging/      # Shared integration-event contracts
├── LogSearchTools/             # Optional log-search benchmark tool
├── UI/                         # React frontend for Bun/Vite dev or optional compose build
├── observability/              # Grafana, Tempo, Loki, Prometheus, Collector config
├── nginx/                      # Nginx load balancer config
├── k6/                         # Load test scripts
├── docker-compose.yml          # Full local topology
├── Dockerfile                  # Main API container image
├── SerilogDemo.http            # Main API request samples
└── appsettings.json            # Main API defaults
```

