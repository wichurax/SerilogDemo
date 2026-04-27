# Fulfillment API

Async consumer that reacts to order-paid events and owns fulfillment workflow state.

## Responsibility

The Fulfillment API demonstrates a second broker-backed side effect on the same `order.paid` event. It consumes the event from its own queue, stores fulfillment workflow state in PostgreSQL, exposes operator endpoints for inspecting and advancing that workflow, and emits logs and spans that can be correlated with the main API and Notifications API in Grafana, Loki, and Tempo.

## Features

- Consumes `order.paid` events from the `serilogdemo.fulfillment` queue
- Stores fulfillment attempts in PostgreSQL under the `fulfillment_service` schema
- Reserves fulfillment immediately and publishes `fulfillment.progress` events through a local outbox
- Supports filtered inspection plus manual `collect`, `pack`, `ship`, and `fail` actions over HTTP
- Uses configurable consumer prefetch and scale-safe outbox locking so multiple instances can share load safely
- Exposes traces, logs, and health checks for the observability stack
- Creates and applies its own EF Core migrations on startup

## Usage

### Docker Compose

```bash
docker compose up -d fulfillment-api rabbitmq
```

In the full compose stack, the service is exposed to the host through Nginx at `http://localhost:8080/fulfillment-api/api/fulfillment/orders`.

### Local Run

```bash
dotnet run --project FulfillmentApi/FulfillmentApi.csproj
```

When run directly, the service exposes `/health` plus workflow endpoints under `/api/fulfillment/orders`.

## Required Configuration

The service needs these settings:

- `ConnectionStrings__Postgres`: PostgreSQL connection string
- `RabbitMq__HostName`: RabbitMQ host
- `RabbitMq__Port`: RabbitMQ port
- `RabbitMq__UserName`: RabbitMQ username
- `RabbitMq__Password`: RabbitMQ password
- `RabbitMq__VirtualHost`: RabbitMQ virtual host
- `Fulfillment__WarehouseName`: logical warehouse identifier stored with each attempt
- `Fulfillment__ConsumerPrefetchCount`: RabbitMQ prefetch for `order.paid` consumption
- `Fulfillment__OutboxBatchSize`: max fulfillment progress events published per polling iteration
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `fulfillment-api`

Default local values are in [appsettings.json](appsettings.json).

## Notes

- This service expects the main API outbox publisher to bind the `serilogdemo.fulfillment` queue topology.
- Default demos can use the HTTP workflow endpoints manually. Load-test demos can pair the service with `k6/load-test-warehouse.js` to automate collect, pack, and ship transitions.
- `GET /api/fulfillment/orders` supports `status`, `userId`, `orderNumber`, `warehouse`, and `take` query parameters.
- Startup uses a dedicated EF migrations history table in the `fulfillment_service` schema to avoid collisions with the other services.