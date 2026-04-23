using System.Diagnostics;

namespace NotificationsApi.Telemetry;

public static class NotificationDiagnostics
{
    public const string ActivitySourceName = "NotificationsApi.Notifications";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}