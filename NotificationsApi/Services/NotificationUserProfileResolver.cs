using Microsoft.EntityFrameworkCore;
using NotificationsApi.Data;

namespace NotificationsApi.Services;

public interface INotificationUserProfileResolver
{
    Task<ResolvedNotificationUserProfile> ResolveAsync(string userId, CancellationToken cancellationToken);
}

public sealed record ResolvedNotificationUserProfile(
    string UserId,
    string Email,
    string PhoneNumber,
    bool EmailEnabled,
    bool SmsEnabled,
    bool ExistsInDatabase);

public sealed class NotificationUserProfileResolver : INotificationUserProfileResolver
{
    private readonly NotificationDbContext _dbContext;

    public NotificationUserProfileResolver(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResolvedNotificationUserProfile> ResolveAsync(string userId, CancellationToken cancellationToken)
    {
        var existingProfile = await _dbContext.NotificationUserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (existingProfile is not null)
        {
            return new ResolvedNotificationUserProfile(
                existingProfile.UserId,
                existingProfile.Email,
                existingProfile.PhoneNumber,
                existingProfile.EmailEnabled,
                existingProfile.SmsEnabled,
                ExistsInDatabase: true);
        }

        return new ResolvedNotificationUserProfile(
            userId,
            CreateDefaultEmail(userId),
            CreateDefaultPhoneNumber(userId),
            EmailEnabled: true,
            SmsEnabled: true,
            ExistsInDatabase: false);
    }

    private static string CreateDefaultEmail(string userId)
    {
        var normalized = new string(userId
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "user";
        }

        return $"{normalized}@notifications.demo";
    }

    private static string CreateDefaultPhoneNumber(string userId)
    {
        var digits = string.Concat(userId.Select(ch => char.IsDigit(ch) ? ch.ToString() : (ch % 10).ToString()));

        if (digits.Length < 7)
        {
            digits = digits.PadRight(7, '0');
        }

        return $"+1555{digits[..7]}";
    }
}