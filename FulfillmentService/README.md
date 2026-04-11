# Fulfillment Service

Async consumer that reacts to order-paid events and simulates downstream fulfillment work.

## Responsibility

The fulfillment service demonstrates a second broker-backed side effect on the same `order.paid` event. It consumes the event from its own queue, stores a fulfillment attempt in PostgreSQL, and emits logs and spans that can be correlated with the API and notification services in Grafana, Loki, and Tempo.

## Features

- Consumes `order.paid` events from the `serilogdemo.fulfillment` queue
- Stores fulfillment attempts in PostgreSQL under the `fulfillment_service` schema
- Simulates warehouse reservation with a configurable processing delay
- Exposes traces, logs, and health checks for the observability stack
- Creates and applies its own EF Core migrations on startup

## Usage

### Docker Compose

```bash
docker compose up -d fulfillment-service rabbitmq
```

### Local Run

```bash
dotnet run --project FulfillmentService/FulfillmentService.csproj
```

The service does not provide business endpoints. The main useful endpoint is `/health`.

## Required Configuration

The service needs these settings:

- `ConnectionStrings__Postgres`: PostgreSQL connection string
- `RabbitMq__HostName`: RabbitMQ host
- `RabbitMq__Port`: RabbitMQ port
- `RabbitMq__UserName`: RabbitMQ username
- `RabbitMq__Password`: RabbitMQ password
- `RabbitMq__VirtualHost`: RabbitMQ virtual host
- `Fulfillment__WarehouseName`: logical warehouse identifier stored with each attempt
- `Fulfillment__ProcessingDelayMilliseconds`: simulated reservation delay per message
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `fulfillment-service`

Default local values are in [appsettings.json](appsettings.json).

## Notes

- This service expects the main API outbox publisher to bind the queue topology.
- Startup uses a dedicated EF migrations history table in the `fulfillment_service` schema to avoid collisions with the other services.