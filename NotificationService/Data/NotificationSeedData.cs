using NotificationService.Models;

namespace NotificationService.Data;

public static class NotificationSeedData
{
    public static readonly NotificationUserProfile[] Users =
    [
        new()
        {
            UserId = "demo-user-001",
            Email = "demo.user.001@notifications.demo",
            PhoneNumber = "+15551000001",
            EmailEnabled = true,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-002",
            Email = "demo.user.002@notifications.demo",
            PhoneNumber = "+15551000002",
            EmailEnabled = true,
            SmsEnabled = false
        },
        new()
        {
            UserId = "demo-user-003",
            Email = "demo.user.003@notifications.demo",
            PhoneNumber = "+15551000003",
            EmailEnabled = false,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-004",
            Email = "demo.user.004@notifications.demo",
            PhoneNumber = "+15551000004",
            EmailEnabled = true,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-005",
            Email = "demo.user.005@notifications.demo",
            PhoneNumber = "+15551000005",
            EmailEnabled = false,
            SmsEnabled = false
        },
        new()
        {
            UserId = "demo-user-006",
            Email = "demo.user.006@notifications.demo",
            PhoneNumber = "+15551000006",
            EmailEnabled = true,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-007",
            Email = "demo.user.007@notifications.demo",
            PhoneNumber = "+15551000007",
            EmailEnabled = true,
            SmsEnabled = false
        },
        new()
        {
            UserId = "demo-user-008",
            Email = "demo.user.008@notifications.demo",
            PhoneNumber = "+15551000008",
            EmailEnabled = false,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-009",
            Email = "demo.user.009@notifications.demo",
            PhoneNumber = "+15551000009",
            EmailEnabled = true,
            SmsEnabled = true
        },
        new()
        {
            UserId = "demo-user-123",
            Email = "demo.user.123@notifications.demo",
            PhoneNumber = "+15551000123",
            EmailEnabled = true,
            SmsEnabled = true
        }
    ];
}