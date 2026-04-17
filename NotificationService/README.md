# Notification Service

Async consumer that reacts to order-paid events published by the main API.

## Responsibility

The notification service demonstrates broker-backed fan-out inside a single downstream service. It consumes `order.paid` messages on separate email and SMS queues, resolves fake user contact preferences, logs fake outbound payloads instead of sending them, and emits spans so the workshop can show asynchronous flow correlation.

## Features

- Consumes `order.paid` events from `serilogdemo.notifications.email` and `serilogdemo.notifications.sms`
- Stores fake user profiles plus separate email and SMS delivery records in PostgreSQL under the `notification_service` schema
- Seeds 10 fake users into `NotificationUsers` during migration
- Falls back to synthesized fake email and phone data when a `UserId` does not exist in the local user table
- Continues the producer trace from RabbitMQ headers so email and SMS handling stay easy to inspect in Tempo
- Emits child spans for user-profile resolution, channel dispatch, and fake provider work so Tempo shows where notification handling time is spent
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

The service exposes `/health` plus read-only inspection endpoints for users and delivery history.

## Required Configuration

The service needs these settings:

- `ConnectionStrings__Postgres`: PostgreSQL connection string
- `RabbitMq__HostName`: RabbitMQ host
- `RabbitMq__Port`: RabbitMQ port
- `RabbitMq__UserName`: RabbitMQ username
- `RabbitMq__Password`: RabbitMQ password
- `RabbitMq__VirtualHost`: RabbitMQ virtual host
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `notification-service`

Default local values are in [appsettings.json](appsettings.json).

## Database Schema

- `NotificationUsers`: fake user email, phone number, and per-channel enablement flags
- `EmailNotificationDeliveries`: one row per handled email notification event
- `SmsNotificationDeliveries`: one row per handled SMS notification event

The migration seeds 10 fake users, including `demo-user-123`. Unknown users do not fail the flow: Notification Service synthesizes a fake email and phone number and assumes both channels are enabled for that event.

## Inspection Endpoints

- `GET /api/notifications/users`: list seeded fake users from `NotificationUsers`
- `GET /api/notifications/users/{userId}`: resolve the effective user profile, including synthesized fallback data for unknown users
- `GET /api/notifications/deliveries/email?take=20`: inspect recent fake email delivery records
- `GET /api/notifications/deliveries/sms?take=20`: inspect recent fake SMS delivery records

Both delivery endpoints also support optional `userId`, `orderNumber`, and `status` query parameters so you can isolate one user, one order, or only `Sent` / `Skipped` / `Failed` results.

## Delivery Behavior

- Email and SMS are handled by separate RabbitMQ consumers and separate fake transport services.
- Fake transports only log the prepared payloads; they do not send anything externally.
- A disabled channel is recorded as `Skipped` in the matching delivery table.
- A transport or processing error is recorded as `Failed`, acknowledged, and not retried by Notification Service.

## Notes

- This service expects the main API outbox publisher to bind the queue topology.
- Startup uses a dedicated EF migrations history table in the `notification_service` schema to avoid collisions with the other services.