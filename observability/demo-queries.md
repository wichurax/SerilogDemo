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

### Email notification consumer spans

```text
{ resource.service.name = "notifications-api" && name = "notification.email.consume_order_paid" }
```

### SMS notification consumer spans

```text
{ resource.service.name = "notifications-api" && name = "notification.sms.consume_order_paid" }
```

### Fake email provider spans

```text
{ resource.service.name = "notifications-api" && name = "notification.email.fake_send" }
```

### Fake SMS provider spans

```text
{ resource.service.name = "notifications-api" && name = "notification.sms.fake_send" }
```

### Fulfillment consumer spans

```text
{ resource.service.name = "fulfillment-api" && name = "fulfillment.consume_order_paid" }
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

### Notification channel logs

```text
{service_name="notifications-api"} |~ "Fake email notification prepared|Fake SMS notification prepared|Skipped fake"
```

### Notification logs for one message id or order number

```text
{service_name="notifications-api"} | json | OrderNumber="ORD-REPLACE-ME"
```

### Fulfillment logs for one order number

```text
{service_name="fulfillment-api"} | json | OrderNumber="ORD-REPLACE-ME"
```

### Outbox publisher logs in the main API

```text
{service_name="serilogdemo-api"} |= "Published outbox message"
```

## Grafana Drilldown Ideas

- Start in Tempo with `checkout.place_order`, open a trace, and confirm it contains the API checkout work, the Payment Gateway span, the producer span, and the async consumer spans.
- Pivot from the email and SMS consumer spans to logs and compare the sent and skipped outcomes for the same order.
- Compare notification and fulfillment spans for the same order number to show RabbitMQ fan-out instead of single-consumer handoff.