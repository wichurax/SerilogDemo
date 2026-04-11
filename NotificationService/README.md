# Notification Service

Async consumer that reacts to order-paid events published by the main API.

## Responsibility

The notification service demonstrates the broker-backed part of the architecture. It consumes `order.paid` messages from RabbitMQ, stores notification attempts in PostgreSQL, and emits its own logs and spans so the workshop can show asynchronous flow correlation.

## Features

- Consumes `order.paid` events from the `serilogdemo.notifications` queue
- Stores notification attempts in PostgreSQL under the `notification_service` schema
- Supports deterministic simulation modes: `Success` and `FailFirstAttempt`
- Continues the producer trace from RabbitMQ headers so retry attempts stay easy to inspect in Tempo
- Exposes traces, logs, and health checks for the observability stack
- Creates and applies its own EF Core migrations on startup

## Usage

### Docker Compose

```bash
docker compose up -d notification-service rabbitmq
```

### Local Run

```bash
dotnet run --project NotificationService/NotificationService.csproj
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
- `Notification__SimulationMode`: `Success` or `FailFirstAttempt`
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `notification-service`

Default local values are in [appsettings.json](appsettings.json).

## Retry Demo

Set `Notification__SimulationMode=FailFirstAttempt` to force the first delivery attempt for each message to fail, persist a `RetryScheduled` status, and requeue the message. The second delivery succeeds, which makes the retry path deterministic for workshops and for trace-to-log correlation.

## Notes

- This service expects the main API outbox publisher to bind the queue topology.
- Startup uses a dedicated EF migrations history table in the `notification_service` schema to avoid collisions with the other services.