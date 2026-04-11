using SerilogDemo.Models;

namespace SerilogDemo.Services.Checkout;

public sealed record CheckoutResult(CheckoutOutcome Outcome, Order? Order, string Message)
{
    public static CheckoutResult Authorized(Order order) => new(CheckoutOutcome.Authorized, order, "Order placed successfully.");

    public static CheckoutResult ValidationFailed(string message) => new(CheckoutOutcome.ValidationFailed, null, message);

    public static CheckoutResult Declined(Order order, string message) => new(CheckoutOutcome.Declined, order, message);

    public static CheckoutResult TimedOut(Order order, string message) => new(CheckoutOutcome.TimedOut, order, message);
}

public enum CheckoutOutcome
{
    Authorized,
    ValidationFailed,
    Declined,
    TimedOut
}