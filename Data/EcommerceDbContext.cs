using Microsoft.EntityFrameworkCore;
using SerilogDemo.Models;

namespace SerilogDemo.Data;

public class EcommerceDbContext : DbContext
{
    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<DeliveryOption> DeliveryOptions => Set<DeliveryOption>();
    public DbSet<PaymentOption> PaymentOptions => Set<PaymentOption>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Item configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.HasMany(e => e.WarehouseInventories)
                  .WithOne(e => e.Item)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WarehouseInventory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WarehouseName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.WarehouseName, e.ItemId }).IsUnique();
            entity.ToTable(tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_WarehouseInventories_QuantityOnHand_NonNegative", "\"QuantityOnHand\" >= 0");
                tableBuilder.HasCheckConstraint("CK_WarehouseInventories_QuantityReserved_NonNegative", "\"QuantityReserved\" >= 0");
                tableBuilder.HasCheckConstraint("CK_WarehouseInventories_Reserved_NotGreaterThanOnHand", "\"QuantityReserved\" <= \"QuantityOnHand\"");
            });
        });

        // Cart configuration
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.Cart)
                  .HasForeignKey(e => e.CartId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CartItem configuration
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(200);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.HasOne(e => e.Item)
                  .WithMany()
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // DeliveryOption configuration
        modelBuilder.Entity<DeliveryOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CourierName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 2);
        });

        // PaymentOption configuration
        modelBuilder.Entity<PaymentOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ItemsTotal).HasPrecision(18, 2);
            entity.Property(e => e.DeliveryPrice).HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
            entity.Property(e => e.FulfillmentWarehouse).HasMaxLength(100);
            entity.Property(e => e.FulfillmentTrackingReference).HasMaxLength(100);
            entity.Property(e => e.FulfillmentLastMessage).HasMaxLength(500);
            entity.Property(e => e.PaymentProviderCode).HasMaxLength(100);
            entity.Property(e => e.PaymentFailureReason).HasMaxLength(500);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.Order)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(200);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
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

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.ProcessedAtUtc);
        });
    }
}
