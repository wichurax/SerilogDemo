using Microsoft.EntityFrameworkCore;
using SerilogDemo.Models;

namespace SerilogDemo.Data;

public class EcommerceDbContext : DbContext
{
    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Basket> Baskets => Set<Basket>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();
    public DbSet<DeliveryOption> DeliveryOptions => Set<DeliveryOption>();
    public DbSet<PaymentOption> PaymentOptions => Set<PaymentOption>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

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
        });

        // Basket configuration
        modelBuilder.Entity<Basket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.Basket)
                  .HasForeignKey(e => e.BasketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // BasketItem configuration
        modelBuilder.Entity<BasketItem>(entity =>
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
    }
}
