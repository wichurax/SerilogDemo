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

    public async Task<IReadOnlyCollection<FulfillmentAttempt>> GetAttemptsAsync(FulfillmentAttemptQuery query, CancellationToken cancellationToken)
    {
        var attemptsQuery = _dbContext.FulfillmentAttempts
            .AsNoTracking()
            .Include(attempt => attempt.Items)
            .AsQueryable();

        if (query.Status.HasValue)
        {
            attemptsQuery = attemptsQuery.Where(attempt => attempt.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            attemptsQuery = attemptsQuery.Where(attempt => attempt.UserId == query.UserId);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            attemptsQuery = attemptsQuery.Where(attempt => attempt.OrderNumber == query.OrderNumber);
        }

        if (!string.IsNullOrWhiteSpace(query.Warehouse))
        {
            attemptsQuery = attemptsQuery.Where(attempt => attempt.Warehouse == query.Warehouse);
        }

        return await attemptsQuery
            .OrderByDescending(attempt => attempt.LastProcessedAtUtc)
            .ThenBy(attempt => attempt.OrderNumber)
            .Take(Math.Clamp(query.Take, 1, 200))
            .ToListAsync(cancellationToken);
    }

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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LoadAttemptForUpdateAsync(orderId, cancellationToken);

        if (attempt is null)
        {
            return FulfillmentTransitionResult.Missing($"Fulfillment attempt for order {orderId} was not found.");
        }

        activity?.SetTag("fulfillment.current_status", attempt.Status.ToString());

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
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Fulfillment for order {OrderNumber} moved to {Status}",
            attempt.OrderNumber,
            attempt.Status);

        return FulfillmentTransitionResult.Success(attempt, $"Fulfillment moved to {targetStatus}.");
    }

    public async Task<FulfillmentTransitionResult> FailAsync(Guid orderId, string? message, CancellationToken cancellationToken)
    {
        using var activity = FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.mark_failed", ActivityKind.Internal);
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("fulfillment.target_status", FulfillmentStatus.Failed.ToString());

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LoadAttemptForUpdateAsync(orderId, cancellationToken);

        if (attempt is null)
        {
            return FulfillmentTransitionResult.Missing($"Fulfillment attempt for order {orderId} was not found.");
        }

        activity?.SetTag("fulfillment.current_status", attempt.Status.ToString());

        if (attempt.Status == FulfillmentStatus.Failed)
        {
            return FulfillmentTransitionResult.Success(attempt, "Fulfillment already marked as failed.");
        }

        if (attempt.Status == FulfillmentStatus.Shipped)
        {
            return FulfillmentTransitionResult.Conflict("Cannot mark shipped fulfillment as failed.");
        }

        attempt.Status = FulfillmentStatus.Failed;
        attempt.LastProcessedAtUtc = DateTime.UtcNow;
        attempt.LastError = string.IsNullOrWhiteSpace(message)
            ? "Fulfillment marked as failed by operator."
            : message.Trim();

        _dbContext.OutboxMessages.Add(FulfillmentProgressOutboxFactory.Create(attempt, attempt.LastError, activity));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogWarning(
            "Fulfillment for order {OrderNumber} marked as failed. Reason: {Reason}",
            attempt.OrderNumber,
            attempt.LastError);

        return FulfillmentTransitionResult.Success(attempt, attempt.LastError);
    }

    private Task<FulfillmentAttempt?> LoadAttemptForUpdateAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return _dbContext.FulfillmentAttempts
            .FromSqlInterpolated($@"
                SELECT *
                FROM fulfillment_service.""FulfillmentAttempts""
                WHERE ""OrderId"" = {orderId}
                FOR UPDATE")
            .Include(attempt => attempt.Items)
            .FirstOrDefaultAsync(attempt => attempt.OrderId == orderId, cancellationToken);
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