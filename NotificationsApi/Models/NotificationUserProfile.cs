namespace NotificationsApi.Models;

public sealed class NotificationUserProfile
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public bool EmailEnabled { get; set; } = true;

    public bool SmsEnabled { get; set; } = true;
}