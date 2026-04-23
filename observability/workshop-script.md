# Workshop Script

This script is intentionally short. It is designed for a live demo that starts with a happy path and then switches to channel-specific notification behavior.

## Setup

1. Start the stack with `docker compose up -d`.
2. Open Grafana at `http://localhost:3001`.
3. Keep [SerilogDemo.http](SerilogDemo.http) open for the API calls.

## Part 1: Happy Path

1. Run the basket setup requests and place an order with the `Async Fan-Out Happy Path` request in [SerilogDemo.http](SerilogDemo.http).
2. Show the API response and capture the returned order number.
3. In Tempo, run the `checkout.place_order` query from [observability/demo-queries.md](observability/demo-queries.md).
4. Open the trace and point out the synchronous payment span, the producer span, then the notification and fulfillment consumer spans.
5. In Loki, filter logs by the order number and show the API publish log, notification success log, and fulfillment success log.

## Part 2: Notification Channel Preferences

1. Use the seeded `demo-user-002` profile from Notifications API, which has email enabled and SMS disabled.
2. Place another order with the `Async Fan-Out Channel Preference Demo` request in [SerilogDemo.http](SerilogDemo.http).
3. In Loki, show the fake email payload log and the SMS skip log for the same order.
4. In Tempo, open the `notification.email.consume_order_paid` and `notification.sms.consume_order_paid` spans and show that they continue the same producer trace but produce different outcomes.
5. Contrast that with the fulfillment consumer, which still completes normally for the same order.

## Talking Points

- The main API stays the system entry point and only publishes one integration event.
- RabbitMQ fan-out lets multiple downstream services react independently to the same `order.paid` message.
- Trace headers on the RabbitMQ message keep the async follow-up work connected in Tempo.
- Notifications API uses a local fake-user table to decide whether email and SMS should be logged for a given user.