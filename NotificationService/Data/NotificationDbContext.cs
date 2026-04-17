using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationUserProfile> NotificationUserProfiles => Set<NotificationUserProfile>();

    public DbSet<EmailNotificationDelivery> EmailNotificationDeliveries => Set<EmailNotificationDelivery>();

    public DbSet<SmsNotificationDelivery> SmsNotificationDeliveries => Set<SmsNotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("notification_service");

        modelBuilder.Entity<NotificationUserProfile>(entity =>
        {
            entity.ToTable("NotificationUsers");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.HasData(NotificationSeedData.Users);
        });

        modelBuilder.Entity<EmailNotificationDelivery>(entity =>
        {
            entity.ToTable("EmailNotificationDeliveries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RecipientEmail).HasMaxLength(200).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.OrderId);
        });

        modelBuilder.Entity<SmsNotificationDelivery>(entity =>
        {
            entity.ToTable("SmsNotificationDeliveries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RecipientPhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.OrderId);
        });
    }
}