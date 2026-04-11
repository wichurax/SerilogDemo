# Workshop Script

This script is intentionally short. It is designed for a live demo that starts with a happy path and then switches to the deterministic notification retry path.

## Setup

1. Start the stack with `docker compose up -d`.
2. Open Grafana at `http://localhost:3000`.
3. Keep [SerilogDemo.http](SerilogDemo.http) open for the API calls.

## Part 1: Happy Path

1. Run the basket setup requests and place an order with the `Async Fan-Out Happy Path` request in [SerilogDemo.http](SerilogDemo.http).
2. Show the API response and capture the returned order number.
3. In Tempo, run the `checkout.place_order` query from [observability/demo-queries.md](observability/demo-queries.md).
4. Open the trace and point out the synchronous payment span, the producer span, then the notification and fulfillment consumer spans.
5. In Loki, filter logs by the order number and show the API publish log, notification success log, and fulfillment success log.

## Part 2: Deterministic Notification Retry

1. Restart only the notification service with PowerShell: `$env:NOTIFICATION_SIMULATION_MODE='FailFirstAttempt'; docker compose up -d notification-service`.
2. If you are using bash instead, run `NOTIFICATION_SIMULATION_MODE=FailFirstAttempt docker compose up -d notification-service`.
3. Place another order with the `Async Fan-Out Retry Demo` request in [SerilogDemo.http](SerilogDemo.http).
4. In Loki, show the warning log that says the notification failed intentionally on attempt 1 and the later success log for attempt 2.
5. In Tempo, open the `notification.consume_order_paid` spans and show that the first attempt is marked as an error and the retried attempt succeeds.
6. Contrast that with the fulfillment consumer, which still completes normally for the same order.

## Talking Points

- The main API stays the system entry point and only publishes one integration event.
- RabbitMQ fan-out lets multiple downstream services react independently to the same `order.paid` message.
- Trace headers on the RabbitMQ message keep the async follow-up work connected in Tempo.
- The retry demo is deterministic, so the first notification attempt always fails and the second always succeeds.