using System.Diagnostics;
using FulfillmentService.Data;
using FulfillmentService.Models;
using FulfillmentService.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentService.Services;

public sealed class FulfillmentWorkflowService
{
    private readonly FulfillmentDbContext _dbContext;
    private readonly ILogger<FulfillmentWorkflowService> _logger;

    public FulfillmentWorkflowService(FulfillmentDbContext dbContext, ILogger<FulfillmentWorkflowService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<FulfillmentAttempt>> GetAttemptsAsync(CancellationToken cancellationToken) => 
        await _dbContext.FulfillmentAttempts
            .AsNoTracking()
            .Include(attempt => attempt.Items)
            .OrderBy(attempt => attempt.Status)
            .ThenBy(attempt => attempt.OrderNumber)
            .ToListAsync(cancellationToken);

    public async Task<FulfillmentTransitionResult> AdvanceAsync(
        Guid orderId,
        FulfillmentStatus targetStatus,
        string? message,
        string? trackingReference,
        CancellationToken cancellationToken)
    {
        using var activity = FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.advance_status", ActivityKind.Internal);
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("fulfillment.target_status", targetStatus.ToString());

        var attempt = await _dbContext.FulfillmentAttempts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

        if (attempt is null)
        {
            return FulfillmentTransitionResult.Missing($"Fulfillment attempt for order {orderId} was not found.");
        }

        if (attempt.Status == targetStatus)
        {
            return FulfillmentTransitionResult.Success(attempt, "Fulfillment already in the requested state.");
        }

        if (!IsAllowedTransition(attempt.Status, targetStatus))
        {
            return FulfillmentTransitionResult.Conflict($"Cannot move fulfillment from {attempt.Status} to {targetStatus}.");
        }

        attempt.Status = targetStatus;
        attempt.LastProcessedAtUtc = DateTime.UtcNow;
        attempt.LastError = null;
        attempt.TrackingReference = targetStatus == FulfillmentStatus.Shipped
            ? string.IsNullOrWhiteSpace(trackingReference)
                ? $"SHIP-{attempt.OrderNumber}"
                : trackingReference.Trim()
            : attempt.TrackingReference;

        switch (targetStatus)
        {
            case FulfillmentStatus.Collected:
                attempt.CollectedAtUtc = DateTime.UtcNow;
                break;
            case FulfillmentStatus.Packed:
                attempt.PackedAtUtc = DateTime.UtcNow;
                break;
            case FulfillmentStatus.Shipped:
                attempt.ShippedAtUtc = DateTime.UtcNow;
                break;
        }

        _dbContext.OutboxMessages.Add(FulfillmentProgressOutboxFactory.Create(attempt, message ?? $"Fulfillment moved to {targetStatus}.", activity));
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Fulfillment for order {OrderNumber} moved to {Status}",
            attempt.OrderNumber,
            attempt.Status);

        return FulfillmentTransitionResult.Success(attempt, $"Fulfillment moved to {targetStatus}.");
    }

    private static bool IsAllowedTransition(FulfillmentStatus currentStatus, FulfillmentStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (FulfillmentStatus.Reserved, FulfillmentStatus.Collected) => true,
            (FulfillmentStatus.Collected, FulfillmentStatus.Packed) => true,
            (FulfillmentStatus.Packed, FulfillmentStatus.Shipped) => true,
            _ => false
        };
    }
}

public sealed record FulfillmentTransitionResult(bool Succeeded, bool NotFound, string Message, FulfillmentAttempt? Attempt)
{
    public static FulfillmentTransitionResult Success(FulfillmentAttempt attempt, string message) => new(true, false, message, attempt);

    public static FulfillmentTransitionResult Missing(string message) => new(false, true, message, null);

    public static FulfillmentTransitionResult Conflict(string message) => new(false, false, message, null);
}