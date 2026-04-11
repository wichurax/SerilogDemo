namespace SerilogDemo.Messaging;

public static class MessagingTopology
{
    public const string OrderEventsExchange = "serilogdemo.order-events";

    public const string OrderPaidRoutingKey = "order.paid";
    public const string FulfillmentProgressRoutingKey = "fulfillment.progress";

    public const string NotificationQueueName = "serilogdemo.notifications";

    public const string FulfillmentQueueName = "serilogdemo.fulfillment";

    public const string OrderProjectionQueueName = "serilogdemo.order-projection";
}