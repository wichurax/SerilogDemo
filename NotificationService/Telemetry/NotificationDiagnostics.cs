using System.Diagnostics;

namespace NotificationService.Telemetry;

public static class NotificationDiagnostics
{
    public const string ActivitySourceName = "NotificationService.Consumer";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}