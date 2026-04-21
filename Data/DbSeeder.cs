using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SerilogDemo.Models;
using SerilogDemo.Options;

namespace SerilogDemo.Data;

public static class DbSeeder
{
    private const int DbInitLockId = 123456; // Unique lock ID for database initialization
    private const int DefaultInventoryQuantity = 20;

    private static readonly Dictionary<string, int> DefaultInventoryByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accessories"] = 32,
        ["Audio"] = 14,
        ["Electronics"] = 18,
        ["Furniture"] = 8,
        ["Gaming"] = 16,
        ["Office"] = 24,
    };

    public static async Task SeedAsync(EcommerceDbContext context, ILogger<EcommerceDbContext> logger)
    {
        var lockAcquired = false;

        try
        {
            logger.LogInformation("Attempting to acquire database initialization lock...");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT pg_try_advisory_lock({DbInitLockId})";
            var result = await command.ExecuteScalarAsync();
            lockAcquired = result is bool b && b;

            if (!lockAcquired)
            {
                logger.LogInformation("Another instance is currently initializing the database. Waiting...");

                command.CommandText = $"SELECT pg_advisory_lock({DbInitLockId})";
                await command.ExecuteNonQueryAsync();
                lockAcquired = true;

                logger.LogInformation("Database initialization completed by another instance. Proceeding...");

                command.CommandText = $"SELECT pg_advisory_unlock({DbInitLockId})";
                await command.ExecuteNonQueryAsync();
                lockAcquired = false;

                return;
            }

            logger.LogInformation("Lock acquired. Starting database initialization...");

            await context.Database.MigrateAsync();
            await SeedCatalogItemsAsync(context, logger);

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

    public static async Task SeedAsync(EcommerceDbContext context, ILogger<EcommerceDbContext> logger, IOptions<WarehouseOptions> warehouseOptions)
    {
        await SeedAsync(context, logger);

        var warehouseName = warehouseOptions.Value.DefaultWarehouseName;
        logger.LogInformation("Ensuring warehouse inventory exists for warehouse {WarehouseName}...", warehouseName);

        var items = await context.Items
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync();

        if (items.Count == 0)
        {
            return;
        }

        var itemIds = items.Select(item => item.Id).ToArray();
        var existingInventoryItemIds = await context.WarehouseInventories
            .Where(inventory => inventory.WarehouseName == warehouseName && itemIds.Contains(inventory.ItemId))
            .Select(inventory => inventory.ItemId)
            .ToListAsync();

        var existingInventorySet = existingInventoryItemIds.ToHashSet();
        var inventories = items
            .Where(item => !existingInventorySet.Contains(item.Id))
            .Select(item => new WarehouseInventory
            {
                Id = Guid.NewGuid(),
                WarehouseName = warehouseName,
                ItemId = item.Id,
                QuantityOnHand = GetDefaultQuantityOnHand(item.Category),
                QuantityReserved = 0,
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        if (inventories.Count == 0)
        {
            logger.LogInformation("Warehouse inventory already covers all {Count} catalog items for warehouse {WarehouseName}", items.Count, warehouseName);
            return;
        }

        await context.WarehouseInventories.AddRangeAsync(inventories);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} warehouse inventory records for warehouse {WarehouseName}", inventories.Count, warehouseName);
    }

    private static async Task SeedCatalogItemsAsync(EcommerceDbContext context, ILogger<EcommerceDbContext> logger)
    {
        var seedItems = GetCatalogSeedItems();
        var seedItemIds = seedItems.Select(item => item.Id).ToArray();
        var existingSeedItems = await context.Items
            .Where(item => seedItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        var itemsAdded = 0;
        var itemsUpdated = 0;

        foreach (var seedItem in seedItems)
        {
            if (existingSeedItems.TryGetValue(seedItem.Id, out var existingItem))
            {
                if (ApplyCatalogSeed(existingItem, seedItem))
                {
                    itemsUpdated += 1;
                }

                continue;
            }

            await context.Items.AddAsync(seedItem.ToItem());
            itemsAdded += 1;
        }

        if (itemsAdded == 0 && itemsUpdated == 0)
        {
            logger.LogInformation(
                "Catalog already matches {ItemCount} seeded items across {CategoryCount} categories",
                seedItems.Count,
                seedItems.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            return;
        }

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Catalog seed synchronized. Added {ItemsAdded} items, updated {ItemsUpdated} items, total seed set {ItemCount} items across {CategoryCount} categories",
            itemsAdded,
            itemsUpdated,
            seedItems.Count,
            seedItems.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static bool ApplyCatalogSeed(Item target, CatalogSeedItem seed)
    {
        var changed = false;

        if (!string.Equals(target.Name, seed.Name, StringComparison.Ordinal))
        {
            target.Name = seed.Name;
            changed = true;
        }

        if (!string.Equals(target.Description, seed.Description, StringComparison.Ordinal))
        {
            target.Description = seed.Description;
            changed = true;
        }

        if (target.Price != seed.Price)
        {
            target.Price = seed.Price;
            changed = true;
        }

        if (!string.Equals(target.Category, seed.Category, StringComparison.Ordinal))
        {
            target.Category = seed.Category;
            changed = true;
        }

        if (!string.Equals(target.ImageUrl, seed.ImageUrl, StringComparison.Ordinal))
        {
            target.ImageUrl = seed.ImageUrl;
            changed = true;
        }

        return changed;
    }

    private static int GetDefaultQuantityOnHand(string category) =>
        DefaultInventoryByCategory.TryGetValue(category, out var quantity)
            ? quantity
            : DefaultInventoryQuantity;

    private static List<CatalogSeedItem> GetCatalogSeedItems() =>
    [
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Wireless Bluetooth Headphones", "Over-ear Bluetooth headphones with active noise cancellation and all-day battery life", 149.99m, "Electronics", "/images/headphones.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Mechanical Gaming Keyboard", "Hot-swappable mechanical keyboard with tactile switches and per-key backlighting", 129.99m, "Electronics", "/images/keyboard.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Ergonomic Office Chair", "Mesh-backed chair with lumbar support, adjustable arms, and tilt lock", 299.99m, "Furniture", "/images/chair.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000004"), "USB-C Hub 7-in-1", "Compact aluminum hub with HDMI, card reader, USB-A, and pass-through charging", 49.99m, "Electronics", "/images/usb-hub.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000005"), "Standing Desk Converter", "Dual-surface riser that turns a fixed desk into a sit-stand workstation", 199.99m, "Furniture", "/images/standing-desk.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000006"), "Wireless Mouse", "Silent-click wireless mouse with precision tracking and rechargeable battery", 39.99m, "Electronics", "/images/mouse.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000007"), "4K Monitor 27\"", "27-inch UHD monitor with USB-C power delivery and factory-calibrated color", 449.99m, "Electronics", "/images/monitor.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000008"), "Laptop Backpack", "Water-resistant commuter backpack with padded laptop sleeve and luggage strap", 59.99m, "Accessories", "/images/backpack.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000009"), "Wi-Fi 6 Router", "Dual-band home router with mesh-ready firmware and four gigabit LAN ports", 179.99m, "Electronics", "/images/router.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000010"), "Smart Home Hub", "Matter-compatible smart home hub for lights, plugs, and climate routines", 119.99m, "Electronics", "/images/smart-hub.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000011"), "Portable SSD 1TB", "Pocket-sized USB 3.2 solid-state drive rated for field backup and travel", 109.99m, "Electronics", "/images/portable-ssd.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000012"), "Solid Wood Bookshelf", "Five-shelf oak veneer bookcase sized for office storage and decor", 249.99m, "Furniture", "/images/bookshelf.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000013"), "Compact Filing Cabinet", "Lockable two-drawer cabinet with caster wheels for under-desk storage", 189.99m, "Furniture", "/images/filing-cabinet.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000014"), "Lounge Accent Chair", "Textured fabric accent chair with walnut legs and medium-firm cushioning", 379.99m, "Furniture", "/images/accent-chair.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000015"), "Stainless Steel Water Bottle", "Insulated 24-ounce bottle that keeps drinks cold through the workday", 24.99m, "Accessories", "/images/water-bottle.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000016"), "Leather Desk Mat", "Full-width desk pad that protects the surface and doubles as a mouse area", 34.99m, "Accessories", "/images/desk-mat.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000017"), "Magnetic Cable Organizer", "Modular cable clips and weighted base for keeping charging leads in place", 18.99m, "Accessories", "/images/cable-organizer.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000018"), "Travel Tech Pouch", "Structured pouch with elastic loops for chargers, adapters, and memory cards", 27.99m, "Accessories", "/images/tech-pouch.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000019"), "Adjustable Laptop Stand", "Fold-flat aluminum stand with six height positions for hybrid desks", 46.99m, "Accessories", "/images/laptop-stand.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000020"), "Monitor Light Bar", "USB-powered light bar that reduces desk glare without taking monitor space", 79.99m, "Accessories", "/images/monitor-light.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000021"), "Fountain Pen Set", "Brass fountain pen starter set with converter, ink cartridges, and case", 44.99m, "Office", "/images/fountain-pen.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000022"), "A4 Paper Pack", "500-sheet pack of 90 gsm office paper for printers, notes, and presentations", 8.99m, "Office", "/images/paper-pack.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000023"), "Weekly Desk Planner", "Undated weekly planner pad with tear-off sheets and goal tracking sidebar", 14.99m, "Office", "/images/desk-planner.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000024"), "Noise-Isolating Cubicle Panel", "Clamp-on felt privacy panel that softens echoes in open office setups", 89.99m, "Office", "/images/cubicle-panel.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000025"), "Thermal Label Printer", "Desktop label printer for shipping, inventory, and cable identification", 129.99m, "Office", "/images/label-printer.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000026"), "Rolling Whiteboard", "Double-sided magnetic whiteboard on locking casters for team planning", 159.99m, "Office", "/images/whiteboard.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000027"), "Stapler and Punch Kit", "Metal desk set with low-effort stapler, two-hole punch, and staple pack", 22.99m, "Office", "/images/stapler-kit.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000028"), "USB Podcast Microphone", "Cardioid USB microphone with desktop stand for streaming, calls, and voiceover", 139.99m, "Audio", "/images/podcast-mic.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000029"), "Bookshelf Speakers Pair", "Powered stereo speakers with optical input and warm near-field tuning", 229.99m, "Audio", "/images/bookshelf-speakers.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000030"), "Studio Headphone Amplifier", "Compact DAC and headphone amp with gain control for editing desks", 99.99m, "Audio", "/images/headphone-amp.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000031"), "Compact Soundbar", "Space-saving soundbar with HDMI ARC and clear dialogue tuning", 199.99m, "Audio", "/images/soundbar.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000032"), "Vinyl Record Player", "Belt-drive turntable with built-in phono preamp and walnut plinth", 259.99m, "Audio", "/images/record-player.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000033"), "Conference Speakerphone", "USB and Bluetooth speakerphone designed for hybrid meeting rooms", 169.99m, "Audio", "/images/speakerphone.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000034"), "Ergonomic Game Controller", "Hall-effect wireless controller with textured grips and charging dock", 69.99m, "Gaming", "/images/controller.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000035"), "32\" Curved Gaming Monitor", "High-refresh QHD gaming monitor with adaptive sync and curved VA panel", 399.99m, "Gaming", "/images/gaming-monitor.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000036"), "Streaming Ring Light", "Bi-color ring light kit for streaming desks and creator setups", 89.99m, "Gaming", "/images/ring-light.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000037"), "Racing Wheel Pedal Set", "Force-feedback wheel and pedal bundle for sim racing rigs", 329.99m, "Gaming", "/images/racing-wheel.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000038"), "RGB Desk Tower Stand", "Elevated desktop PC stand with lockable wheels and RGB edge lighting", 54.99m, "Gaming", "/images/tower-stand.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000039"), "Mechanical Keypad", "Programmable one-handed keypad for macros, shortcuts, and gaming overlays", 74.99m, "Gaming", "/images/mechanical-keypad.jpg"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000040"), "Gaming Mouse Pad XL", "Extended stitched mouse pad sized for keyboard-and-mouse setups", 29.99m, "Gaming", "/images/mouse-pad.jpg")
    ];

    private sealed record CatalogSeedItem(Guid Id, string Name, string Description, decimal Price, string Category, string ImageUrl)
    {
        public Item ToItem() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Price = Price,
            Category = Category,
            ImageUrl = ImageUrl
        };
    }
}
