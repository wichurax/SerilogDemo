namespace NotificationService.Options;

public sealed class NotificationServiceOptions
{
    public const string SectionName = "Notification";

    public NotificationSimulationMode SimulationMode { get; set; } = NotificationSimulationMode.Success;
}

public enum NotificationSimulationMode
{
    Success,
    FailFirstAttempt
}