using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Contracts;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Models;
using SerilogDemo.Messaging;
using SerilogDemo.Services.Inventory;
using SerilogDemo.Services.Payments;
using SerilogDemo.Telemetry;

namespace SerilogDemo.Services.Checkout;

/// <inheritdoc cref="ICheckoutService" />
public sealed class CheckoutService : ICheckoutService
{
    private const string Currency = "USD";

    private readonly EcommerceDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentGatewayClient _paymentGatewayClient;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(EcommerceDbContext context, IInventoryService inventoryService, IPaymentGatewayClient paymentGatewayClient, ILogger<CheckoutService> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _paymentGatewayClient = paymentGatewayClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CheckoutResult> PlaceOrderAsync(string userId, PlaceOrderRequest request, PaymentScenario? paymentScenario, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var activity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.place_order", ActivityKind.Internal);
        activity?.SetTag("app.user_id", userId);
        activity?.SetTag("checkout.payment_scenario", paymentScenario?.ToString() ?? "default");

        using var basketActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.load_basket", ActivityKind.Internal);
        var basket = await _context.Baskets
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (basket is null || basket.Items.Count == 0)
        {
            basketActivity?.SetTag("checkout.valid", false);
            RecordCheckoutMetrics("validation_failed", paymentMethodCode: "unknown", stopwatch.Elapsed.TotalMilliseconds);
            return CheckoutResult.ValidationFailed("Cannot place order with an empty basket");
        }

        basketActivity?.SetTag("basket.id", basket.Id);
        basketActivity?.SetTag("basket.item_count", basket.Items.Count);

        using var validationActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.validate_options", ActivityKind.Internal);
        var deliveryOption = await _context.DeliveryOptions.FindAsync([request.DeliveryOptionId], cancellationToken);
        if (deliveryOption is null || !deliveryOption.IsActive)
        {
            validationActivity?.SetTag("checkout.valid", false);
            RecordCheckoutMetrics("validation_failed", paymentMethodCode: "unknown", stopwatch.Elapsed.TotalMilliseconds);
            return CheckoutResult.ValidationFailed("Invalid delivery option");
        }

        var paymentOption = await _context.PaymentOptions.FindAsync([request.PaymentOptionId], cancellationToken);
        if (paymentOption is null || !paymentOption.IsActive)
        {
            validationActivity?.SetTag("checkout.valid", false);
            RecordCheckoutMetrics("validation_failed", paymentMethodCode: "unknown", stopwatch.Elapsed.TotalMilliseconds);
            return CheckoutResult.ValidationFailed("Invalid payment option");
        }

        validationActivity?.SetTag("delivery.option", deliveryOption.Name);
        validationActivity?.SetTag("payment.option", paymentOption.Name);

        var orderLines = basket.Items
            .Select(item => new InventoryQuantityChange(item.ItemId, item.ItemName, item.Quantity))
            .ToArray();

        using (var reservationActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.reserve_inventory", ActivityKind.Internal))
        {
            reservationActivity?.SetTag("warehouse.name", _inventoryService.WarehouseName);

            await using var reservationTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var reservationResult = await _inventoryService.TryReserveAsync(orderLines, cancellationToken);
            if (!reservationResult.Succeeded)
            {
                await reservationTransaction.RollbackAsync(cancellationToken);
                reservationActivity?.SetTag("inventory.outcome", "validation_failed");
                RecordCheckoutMetrics("validation_failed", paymentOption.Icon, stopwatch.Elapsed.TotalMilliseconds);
                return CheckoutResult.ValidationFailed(reservationResult.Message);
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = GenerateOrderNumber(),
                UserId = userId,
                DeliveryOptionId = deliveryOption.Id,
                DeliveryPrice = deliveryOption.Price,
                PaymentOptionId = paymentOption.Id,
                ItemsTotal = basket.TotalPrice,
                TotalPrice = basket.TotalPrice + deliveryOption.Price,
                Status = OrderStatus.Pending,
                PaymentStatus = Models.PaymentStatus.Pending,
                FulfillmentStatus = OrderFulfillmentStatus.Pending,
                FulfillmentWarehouse = _inventoryService.WarehouseName,
                FulfillmentLastMessage = "Awaiting fulfillment reservation acknowledgement.",
                Items = basket.Items
                    .Select(bi => new OrderItem 
                        {
                            Id = Guid.NewGuid(),
                            ItemId = bi.ItemId,
                            ItemName = bi.ItemName,
                            UnitPrice = bi.UnitPrice,
                            Quantity = bi.Quantity
                        })
                    .ToList()
            };

            using (var persistenceActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.persist_order", ActivityKind.Internal))
            {
                persistenceActivity?.SetTag("order.number", order.OrderNumber);
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await reservationTransaction.CommitAsync(cancellationToken);

            AuthorizePaymentResponse paymentResponse;
            using (var paymentActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.authorize_payment", ActivityKind.Client))
            {
                paymentActivity?.SetTag("order.number", order.OrderNumber);
                paymentActivity?.SetTag("payment.method", paymentOption.Icon);

                var paymentRequest = new AuthorizePaymentRequest(
                    IdempotencyKey: $"order:{order.Id}:payment:authorize",
                    OrderReference: order.OrderNumber,
                    UserId: userId,
                    PaymentMethodCode: paymentOption.Icon,
                    Amount: order.TotalPrice,
                    Currency: Currency,
                    Scenario: paymentScenario);

                paymentResponse = await _paymentGatewayClient.AuthorizeAsync(paymentRequest, cancellationToken);

                paymentActivity?.SetTag("payment.status", paymentResponse.Status.ToString());
                paymentActivity?.SetTag("payment.provider_code", paymentResponse.ProviderCode);
            }

            order.PaymentAttemptId = paymentResponse.PaymentAttemptId;
            order.PaymentProviderCode = paymentResponse.ProviderCode;
            order.PaymentFailureReason = paymentResponse.Status == PaymentAuthorizationStatus.Authorized
                ? null
                : paymentResponse.Message;

            CheckoutResult result;
            switch (paymentResponse.Status)
            {
                case PaymentAuthorizationStatus.Authorized:
                    order.PaymentStatus = Models.PaymentStatus.Authorized;
                    order.Status = OrderStatus.Confirmed;

                    using (var finalizeActivity = EcommerceDiagnostics.ActivitySource.StartActivity("checkout.finalize_order", ActivityKind.Internal))
                    {
                        finalizeActivity?.SetTag("order.id", order.Id);

                        var orderPaidEvent = new OrderPaidIntegrationEvent(
                            EventId: Guid.NewGuid(),
                            OrderId: order.Id,
                            OrderNumber: order.OrderNumber,
                            UserId: order.UserId,
                            PaymentAttemptId: order.PaymentAttemptId,
                            PaymentMethodCode: paymentOption.Icon,
                            Delivery: new DeliverySelectionSnapshot(
                                deliveryOption.Id,
                                deliveryOption.CourierName,
                                deliveryOption.Name,
                                deliveryOption.Price,
                                deliveryOption.EstimatedDaysMin,
                                deliveryOption.EstimatedDaysMax),
                            Items: order.Items.Select(item => new OrderPaidLineItem(item.ItemId, item.ItemName, item.Quantity, item.UnitPrice)).ToArray(),
                            TotalPrice: order.TotalPrice,
                            OccurredAtUtc: DateTimeOffset.UtcNow);

                        _context.OutboxMessages.Add(new OutboxMessage
                        {
                            Id = orderPaidEvent.EventId,
                            Type = nameof(OrderPaidIntegrationEvent),
                            RoutingKey = MessagingTopology.OrderPaidRoutingKey,
                            Payload = JsonSerializer.Serialize(orderPaidEvent),
                            TraceParent = activity?.Id ?? Activity.Current?.Id,
                            TraceState = activity?.TraceStateString ?? Activity.Current?.TraceStateString,
                            OccurredAtUtc = orderPaidEvent.OccurredAtUtc.UtcDateTime
                        });

                        _context.BasketItems.RemoveRange(basket.Items);
                        _context.Baskets.Remove(basket);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    EcommerceMetrics.OrdersPlaced.Add(1, new TagList
                    {
                        { "delivery_courier", deliveryOption.CourierName }
                    });
                    EcommerceMetrics.OrderTotals.Record((double)order.TotalPrice, new TagList
                    {
                        { "delivery_courier", deliveryOption.CourierName }
                    });

                    result = CheckoutResult.Authorized(order);
                    break;

                case PaymentAuthorizationStatus.Declined:
                    order.PaymentStatus = Models.PaymentStatus.Declined;
                    order.Status = OrderStatus.Cancelled;
                    order.FulfillmentLastMessage = "Payment declined. Inventory reservation released.";
                    await ReleaseReservedInventoryAsync(orderLines, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    result = CheckoutResult.Declined(order, paymentResponse.Message);
                    break;

                default:
                    order.PaymentStatus = Models.PaymentStatus.TimedOut;
                    order.Status = OrderStatus.Cancelled;
                    order.FulfillmentLastMessage = "Payment timed out. Inventory reservation released.";
                    await ReleaseReservedInventoryAsync(orderLines, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    result = CheckoutResult.TimedOut(order, paymentResponse.Message);
                    break;
            }

            EcommerceMetrics.PaymentAuthorizations.Add(1, new TagList
            {
                { "payment_method", paymentOption.Icon },
                { "outcome", result.Outcome.ToString().ToLowerInvariant() }
            });

            RecordCheckoutMetrics(result.Outcome.ToString().ToLowerInvariant(), paymentOption.Icon, stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Checkout completed for order {OrderNumber} with outcome {Outcome}, payment status {PaymentStatus}, provider code {ProviderCode}",
                order.OrderNumber,
                result.Outcome,
                order.PaymentStatus,
                order.PaymentProviderCode);

            order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.DeliveryOption)
                .Include(o => o.PaymentOption)
                .FirstAsync(o => o.Id == order.Id, cancellationToken);

            return result with { Order = order };
        }
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{timestamp}-{uniqueSuffix}";
    }

    private static void RecordCheckoutMetrics(string outcome, string paymentMethodCode, double elapsedMilliseconds)
    {
        EcommerceMetrics.CheckoutAttempts.Add(1, new TagList
        {
            { "outcome", outcome },
            { "payment_method", paymentMethodCode }
        });

        EcommerceMetrics.CheckoutDuration.Record(elapsedMilliseconds, new TagList
        {
            { "outcome", outcome },
            { "payment_method", paymentMethodCode }
        });
    }

    private async Task ReleaseReservedInventoryAsync(IReadOnlyCollection<InventoryQuantityChange> orderLines, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var releaseResult = await _inventoryService.ReleaseReservationAsync(orderLines, cancellationToken);
        if (!releaseResult.Succeeded)
        {
            throw new InvalidOperationException(releaseResult.Message);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}