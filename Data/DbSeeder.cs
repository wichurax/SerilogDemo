using Microsoft.EntityFrameworkCore;
using Npgsql;
using SerilogDemo.Models;

namespace SerilogDemo.Data;

public static class DbSeeder
{
    private const int DbInitLockId = 123456; // Unique lock ID for database initialization
    
    public static async Task SeedAsync(EcommerceDbContext context, ILogger<EcommerceDbContext> logger)
    {
        // Try to acquire PostgreSQL advisory lock to ensure only one instance initializes DB
        var lockAcquired = false;
        
        try
        {
            logger.LogInformation("Attempting to acquire database initialization lock...");
            
            // Try to acquire lock (non-blocking)
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT pg_try_advisory_lock({DbInitLockId})";
            var result = await command.ExecuteScalarAsync();
            lockAcquired = result is bool b && b;
            
            if (!lockAcquired)
            {
                logger.LogInformation("Another instance is currently initializing the database. Waiting...");
                
                // Wait for the lock (blocking) - this ensures we don't proceed until initialization is done
                command.CommandText = $"SELECT pg_advisory_lock({DbInitLockId})";
                await command.ExecuteNonQueryAsync();
                lockAcquired = true;
                
                logger.LogInformation("Database initialization completed by another instance. Proceeding...");
                
                // Release the lock immediately since we didn't do the work
                command.CommandText = $"SELECT pg_advisory_unlock({DbInitLockId})";
                await command.ExecuteNonQueryAsync();
                lockAcquired = false;
                
                return; // Database already initialized
            }
            
            logger.LogInformation("Lock acquired. Starting database initialization...");
            
            // Apply pending migrations
            await context.Database.MigrateAsync();

        // Seed Items
        if (!await context.Items.AnyAsync())
        {
            logger.LogInformation("Seeding catalog items...");
            
            var items = new List<Item>
            {
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Name = "Wireless Bluetooth Headphones",
                    Description = "High-quality wireless headphones with noise cancellation",
                    Price = 149.99m,
                    Category = "Electronics",
                    ImageUrl = "/images/headphones.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Name = "Mechanical Gaming Keyboard",
                    Description = "RGB mechanical keyboard with Cherry MX switches",
                    Price = 129.99m,
                    Category = "Electronics",
                    ImageUrl = "/images/keyboard.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Name = "Ergonomic Office Chair",
                    Description = "Comfortable office chair with lumbar support",
                    Price = 299.99m,
                    Category = "Furniture",
                    ImageUrl = "/images/chair.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                    Name = "USB-C Hub 7-in-1",
                    Description = "Multi-port USB-C hub with HDMI, USB 3.0, SD card reader",
                    Price = 49.99m,
                    Category = "Electronics",
                    ImageUrl = "/images/usb-hub.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    Name = "Standing Desk Converter",
                    Description = "Adjustable standing desk converter for healthy working",
                    Price = 199.99m,
                    Category = "Furniture",
                    ImageUrl = "/images/standing-desk.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                    Name = "Wireless Mouse",
                    Description = "Ergonomic wireless mouse with precision tracking",
                    Price = 39.99m,
                    Category = "Electronics",
                    ImageUrl = "/images/mouse.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                    Name = "4K Monitor 27\"",
                    Description = "27-inch 4K UHD monitor with HDR support",
                    Price = 449.99m,
                    Category = "Electronics",
                    ImageUrl = "/images/monitor.jpg"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    Name = "Laptop Backpack",
                    Description = "Water-resistant laptop backpack with USB charging port",
                    Price = 59.99m,
                    Category = "Accessories",
                    ImageUrl = "/images/backpack.jpg"
                }
            };

            await context.Items.AddRangeAsync(items);
            logger.LogInformation("Seeded {Count} catalog items", items.Count);
        }

        // Seed Delivery Options
        if (!await context.DeliveryOptions.AnyAsync())
        {
            logger.LogInformation("Seeding delivery options...");
            
            var deliveryOptions = new List<DeliveryOption>
            {
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000001"),
                    CourierName = "DPD",
                    Name = "DPD Standard",
                    Description = "Standard delivery by DPD courier",
                    Price = 9.99m,
                    EstimatedDaysMin = 3,
                    EstimatedDaysMax = 5
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000002"),
                    CourierName = "DPD",
                    Name = "DPD Express",
                    Description = "Express delivery by DPD courier - next business day",
                    Price = 14.99m,
                    EstimatedDaysMin = 1,
                    EstimatedDaysMax = 2
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000003"),
                    CourierName = "DHL",
                    Name = "DHL Standard",
                    Description = "Standard delivery by DHL",
                    Price = 8.99m,
                    EstimatedDaysMin = 3,
                    EstimatedDaysMax = 5
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000004"),
                    CourierName = "DHL",
                    Name = "DHL Express",
                    Description = "Express international delivery by DHL",
                    Price = 19.99m,
                    EstimatedDaysMin = 1,
                    EstimatedDaysMax = 2
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000005"),
                    CourierName = "InPost",
                    Name = "InPost Parcel Locker",
                    Description = "Delivery to InPost parcel locker - 24/7 pickup",
                    Price = 6.99m,
                    EstimatedDaysMin = 2,
                    EstimatedDaysMax = 3
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000006"),
                    CourierName = "InPost",
                    Name = "InPost Courier",
                    Description = "Home delivery by InPost courier",
                    Price = 11.99m,
                    EstimatedDaysMin = 2,
                    EstimatedDaysMax = 4
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000007"),
                    CourierName = "GLS",
                    Name = "GLS Standard",
                    Description = "Standard delivery by GLS",
                    Price = 7.99m,
                    EstimatedDaysMin = 3,
                    EstimatedDaysMax = 5
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000008"),
                    CourierName = "GLS",
                    Name = "GLS Express",
                    Description = "Express delivery by GLS",
                    Price = 12.99m,
                    EstimatedDaysMin = 1,
                    EstimatedDaysMax = 2
                }
            };

            await context.DeliveryOptions.AddRangeAsync(deliveryOptions);
            logger.LogInformation("Seeded {Count} delivery options", deliveryOptions.Count);
        }

        // Seed Payment Options
        if (!await context.PaymentOptions.AnyAsync())
        {
            logger.LogInformation("Seeding payment options...");
            
            var paymentOptions = new List<PaymentOption>
            {
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000001"),
                    Name = "Credit Card",
                    Description = "Pay securely with Visa, Mastercard, or American Express",
                    Icon = "credit-card"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000002"),
                    Name = "PayPal",
                    Description = "Fast and secure payment with your PayPal account",
                    Icon = "paypal"
                },
                new()
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000003"),
                    Name = "Bank Transfer",
                    Description = "Direct bank transfer - order will be processed after payment confirmation",
                    Icon = "bank"
                }
            };

            await context.PaymentOptions.AddRangeAsync(paymentOptions);
            logger.LogInformation("Seeded {Count} payment options", paymentOptions.Count);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Database seeding completed successfully");
        }
        finally
        {
            // Always release the lock if we acquired it
            if (lockAcquired)
            {
                try
                {
                    var connection = context.Database.GetDbConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT pg_advisory_unlock({DbInitLockId})";
                    await command.ExecuteNonQueryAsync();
                    logger.LogInformation("Database initialization lock released");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to release database initialization lock");
                }
            }
        }
    }
}
