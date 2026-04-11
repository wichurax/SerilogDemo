using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("notification_service");

        modelBuilder.Entity<NotificationAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(500);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.OrderId);
        });
    }
}