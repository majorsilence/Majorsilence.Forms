using PointOfSale.Contracts;
using PointOfSale.Data.Entities;

namespace PointOfSale.Data;

/// <summary>
/// Seeds demo categories/products/users on first run. All PINs below are demo-only
/// credentials for exercising this sample — never seed known credentials in production.
/// </summary>
public static class DbSeeder
{
    public static void Seed(PointOfSaleDbContext db)
    {
        if (db.Users.Any())
            return;

        var (managerHash, managerSalt) = PinHasher.HashPin("9999");
        var (cashier1Hash, cashier1Salt) = PinHasher.HashPin("1111");
        var (cashier2Hash, cashier2Salt) = PinHasher.HashPin("2222");
        var (kioskHash, kioskSalt) = PinHasher.HashPin("0000");

        db.Users.AddRange(
            new User { DisplayName = "Morgan (Manager)", PinHash = managerHash, PinSalt = managerSalt, Role = UserRole.Manager },
            new User { DisplayName = "Alex (Cashier)", PinHash = cashier1Hash, PinSalt = cashier1Salt, Role = UserRole.Cashier },
            new User { DisplayName = "Sam (Cashier)", PinHash = cashier2Hash, PinSalt = cashier2Salt, Role = UserRole.Cashier },
            new User { DisplayName = "Self-Checkout Kiosk", PinHash = kioskHash, PinSalt = kioskSalt, Role = UserRole.Cashier });

        var beverages = new Category { Name = "Beverages", Description = "Drinks, hot and cold" };
        var snacks = new Category { Name = "Snacks", Description = "Chips, candy, and other snack foods" };
        var produce = new Category { Name = "Produce", Description = "Fresh fruits and vegetables" };
        var household = new Category { Name = "Household", Description = "Cleaning and household supplies" };
        db.Categories.AddRange(beverages, snacks, produce, household);

        db.Products.AddRange(
            new Product { Sku = "BEV-001", Name = "Cola 12oz Can", Price = 1.29m, Category = beverages, QuantityOnHand = 120, ReorderThreshold = 24 },
            new Product { Sku = "BEV-002", Name = "Spring Water 16oz", Price = 0.99m, Category = beverages, QuantityOnHand = 200, ReorderThreshold = 40 },
            new Product { Sku = "BEV-003", Name = "Orange Juice 32oz", Price = 3.49m, Category = beverages, QuantityOnHand = 45, ReorderThreshold = 12 },
            new Product { Sku = "BEV-004", Name = "Coffee, Ground 12oz", Price = 7.99m, Category = beverages, QuantityOnHand = 30, ReorderThreshold = 8 },
            new Product { Sku = "SNK-001", Name = "Potato Chips 8oz", Price = 3.99m, Category = snacks, QuantityOnHand = 60, ReorderThreshold = 12 },
            new Product { Sku = "SNK-002", Name = "Chocolate Bar", Price = 1.79m, Category = snacks, QuantityOnHand = 150, ReorderThreshold = 30 },
            new Product { Sku = "SNK-003", Name = "Trail Mix 6oz", Price = 4.49m, Category = snacks, QuantityOnHand = 40, ReorderThreshold = 10 },
            new Product { Sku = "SNK-004", Name = "Pretzels 10oz", Price = 3.29m, Category = snacks, QuantityOnHand = 55, ReorderThreshold = 12 },
            new Product { Sku = "PRD-001", Name = "Bananas (per lb)", Price = 0.59m, Category = produce, QuantityOnHand = 300, ReorderThreshold = 50 },
            new Product { Sku = "PRD-002", Name = "Apples (per lb)", Price = 1.49m, Category = produce, QuantityOnHand = 180, ReorderThreshold = 40 },
            new Product { Sku = "PRD-003", Name = "Avocado", Price = 1.99m, Category = produce, QuantityOnHand = 75, ReorderThreshold = 15 },
            new Product { Sku = "PRD-004", Name = "Carrots 1lb Bag", Price = 1.29m, Category = produce, QuantityOnHand = 90, ReorderThreshold = 20 },
            new Product { Sku = "HHD-001", Name = "Paper Towels, 2-pack", Price = 5.99m, Category = household, QuantityOnHand = 35, ReorderThreshold = 8 },
            new Product { Sku = "HHD-002", Name = "Dish Soap 24oz", Price = 3.79m, Category = household, QuantityOnHand = 50, ReorderThreshold = 10 },
            new Product { Sku = "HHD-003", Name = "Trash Bags, 30ct", Price = 8.49m, Category = household, QuantityOnHand = 25, ReorderThreshold = 6 });

        db.SaveChanges();
    }
}
