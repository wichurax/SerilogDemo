# Payment Gateway

Internal payment service used by the demo checkout flow.

## Responsibility

The payment gateway handles synchronous payment authorization requests from the main API. It exists to make the trace more realistic by introducing a downstream HTTP hop with its own database state, logs, and spans.

## Features

- Authorizes payments through `POST /api/payments/authorize`
- Stores payment attempts in PostgreSQL under the `payment_gateway` schema
- Enforces idempotency by `IdempotencyKey`
- Supports deterministic demo scenarios: `Success`, `Decline`, `SlowSuccess`, `Timeout`
- Exposes traces, logs, and health checks for Grafana/Tempo/Loki

## Usage

### Docker Compose

The service is started by the root compose file and is intended to be called only by the main API.

```bash
docker compose up -d payment-gateway
```

### Local Run

```bash
dotnet run --project PaymentGateway/PaymentGateway.csproj
```

Swagger is available in Development when the service is run directly.

## Required Configuration

The service needs these settings:

- `ConnectionStrings__Postgres`: PostgreSQL connection string
- `PaymentGateway__SlowSuccessDelayMilliseconds`: delay used by the `SlowSuccess` scenario
- `PaymentGateway__TimeoutDelayMilliseconds`: delay used by the `Timeout` scenario
- `OTEL_EXPORTER_OTLP_ENDPOINT`: OTLP base endpoint
- `OTEL_EXPORTER_OTLP_PROTOCOL`: exporter protocol, expected `http/protobuf`
- `OTEL_SERVICE_NAME`: logical service name, usually `payment-gateway`

Default local values are in [appsettings.json](appsettings.json).

## Request Samples

Example requests live in [PaymentGateway.http](PaymentGateway.http).