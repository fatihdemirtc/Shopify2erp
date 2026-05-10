using Microsoft.EntityFrameworkCore;
using ShopifyErpSync.Core.Models.Erp;

namespace ShopifyErpSync.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.Products.AnyAsync()) return;

        db.Products.AddRange(
            new Product { Sku = "TS-RM", Name = "Red T-Shirt M", StockQuantity = 50, Price = 29.99m },
            new Product { Sku = "TS-BL", Name = "Blue T-Shirt L", StockQuantity = 45, Price = 29.99m },
            new Product { Sku = "TS-GXL", Name = "Green T-Shirt XL", StockQuantity = 30, Price = 34.99m },
            new Product { Sku = "HO-NVY-M", Name = "Navy Hoodie M", StockQuantity = 25, Price = 59.99m },
            new Product { Sku = "HO-BLK-L", Name = "Black Hoodie L", StockQuantity = 20, Price = 59.99m },
            new Product { Sku = "CAP-WHT", Name = "White Cap", StockQuantity = 100, Price = 19.99m },
            new Product { Sku = "CAP-BLK", Name = "Black Cap", StockQuantity = 80, Price = 19.99m },
            new Product { Sku = "MUG-CLR", Name = "Ceramic Mug Clear", StockQuantity = 60, Price = 14.99m },
            new Product { Sku = "BAG-TOT", Name = "Canvas Tote Bag", StockQuantity = 40, Price = 24.99m },
            new Product { Sku = "STCK-PCK", Name = "Sticker Pack", StockQuantity = 200, Price = 9.99m }
        );

        await db.SaveChangesAsync();
    }
}
