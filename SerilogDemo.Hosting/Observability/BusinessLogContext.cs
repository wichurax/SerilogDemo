using Serilog.Context;

namespace SerilogDemo.Hosting.Observability;

/// <summary>
/// Pushes shared business identifiers into Serilog log context for cross-service correlation.
/// </summary>
public static class BusinessLogContext
{
    /// <summary>
    /// Adds order-related identifiers to the ambient log context.
    /// </summary>
    public static IDisposable PushOrder(Guid? orderId = null, string? orderNumber = null, string? userId = null)
    {
        var disposables = new List<IDisposable>(capacity: 3);

        if (orderId.HasValue)
        {
            disposables.Add(LogContext.PushProperty("OrderId", orderId.Value));
        }

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            disposables.Add(LogContext.PushProperty("OrderNumber", orderNumber));
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            disposables.Add(LogContext.PushProperty("UserId", userId));
        }

        return disposables.Count == 0 ? EmptyScope.Instance : new CompositeScope(disposables);
    }

    private sealed class CompositeScope : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _disposables;

        public CompositeScope(IReadOnlyList<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            for (var index = _disposables.Count - 1; index >= 0; index--)
            {
                _disposables[index].Dispose();
            }
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}