using System.Diagnostics;

namespace NotificationService.Telemetry;

public static class NotificationDiagnostics
{
    public const string ActivitySourceName = "NotificationService.Notifications";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}