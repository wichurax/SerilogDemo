using System.Diagnostics;
using System.Text;

namespace SerilogDemo.Messaging;

public static class TraceContextPropagation
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static IDictionary<string, object?> CreateHeaders(Activity? activity)
    {
        var headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (activity?.Id is not { } activityId)
        {
            return headers;
        }

        headers[TraceParentHeader] = Encoding.UTF8.GetBytes(activityId);

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            headers[TraceStateHeader] = Encoding.UTF8.GetBytes(activity.TraceStateString);
        }

        return headers;
    }

    public static ActivityContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null)
        {
            return default;
        }

        var traceParent = TryGetHeaderValue(headers, TraceParentHeader);
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        var traceState = TryGetHeaderValue(headers, TraceStateHeader);
        return ActivityContext.TryParse(traceParent, traceState, out var parentContext)
            ? parentContext
            : default;
    }

    public static ActivityContext Extract(string? traceParent, string? traceState)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(traceParent, traceState, out var parentContext)
            ? parentContext
            : default;
    }

    private static string? TryGetHeaderValue(IDictionary<string, object?> headers, string key)
    {
        if (!headers.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => value.ToString()
        };
    }
}