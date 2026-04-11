using FulfillmentService.Models;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentService.Data;

public class FulfillmentDbContext : DbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options)
    {
    }

    public DbSet<FulfillmentAttempt> FulfillmentAttempts => Set<FulfillmentAttempt>();
    public DbSet<FulfillmentAttemptItem> FulfillmentAttemptItems => Set<FulfillmentAttemptItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("fulfillment_service");

        modelBuilder.Entity<FulfillmentAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Warehouse).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeliveryCourier).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeliveryOptionName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(500);
            entity.Property(e => e.TrackingReference).HasMaxLength(100);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.OrderId);
            entity.HasMany(e => e.Items)
                .WithOne(e => e.FulfillmentAttempt)
                .HasForeignKey(e => e.FulfillmentAttemptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FulfillmentAttemptItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RoutingKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.TraceParent).HasMaxLength(200);
            entity.Property(e => e.TraceState).HasMaxLength(500);
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.HasIndex(e => e.PublishedAtUtc);
            entity.HasIndex(e => e.OccurredAtUtc);
        });
    }
}