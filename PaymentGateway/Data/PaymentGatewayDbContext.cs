using Microsoft.EntityFrameworkCore;
using PaymentGateway.Models;

namespace PaymentGateway.Data;

public class PaymentGatewayDbContext : DbContext
{
    public PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("payment_gateway");

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrderReference).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PaymentMethodCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ProviderCode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ProviderReference).HasMaxLength(100);
            entity.Property(e => e.Scenario).HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.OrderReference);
        });
    }
}