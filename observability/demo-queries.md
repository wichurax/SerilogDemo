# Demo Queries

Use these queries during the order-paid fan-out demo. Replace the sample order number with the value returned by the API.

## Tempo TraceQL

### Checkout request in the main API

```text
{ resource.service.name = "serilogdemo-api" && name = "checkout.place_order" }
```

### Payment authorization downstream hop

```text
{ resource.service.name = "payment-gateway" && name = "payment.authorize" }
```

### Notification consumer spans, including retry attempts

```text
{ resource.service.name = "notification-service" && name = "notification.consume_order_paid" }
```

### Fulfillment consumer spans

```text
{ resource.service.name = "fulfillment-service" && name = "fulfillment.consume_order_paid" }
```

### Filter by a specific order number attribute

```text
{ span.order.number = "ORD-REPLACE-ME" }
```

## Loki LogQL

### API logs for one order number

```text
{service_name="serilogdemo-api"} | json | OrderNumber="ORD-REPLACE-ME"
```

### Notification retry log lines

```text
{service_name="notification-service"} |~ "failed intentionally|Notification sent"
```

### Notification logs for one message id or order number

```text
{service_name="notification-service"} | json | OrderNumber="ORD-REPLACE-ME"
```

### Fulfillment logs for one order number

```text
{service_name="fulfillment-service"} | json | OrderNumber="ORD-REPLACE-ME"
```

### Outbox publisher logs in the main API

```text
{service_name="serilogdemo-api"} |= "Published outbox message"
```

## Grafana Drilldown Ideas

- Start in Tempo with `checkout.place_order`, open a trace, and confirm it contains the API checkout work, the Payment Gateway span, the producer span, and the async consumer spans.
- Pivot from a notification retry span to logs and confirm the warning log is followed by a later success log for the same message.
- Compare notification and fulfillment spans for the same order number to show RabbitMQ fan-out instead of single-consumer handoff.