using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;
using Xunit;

namespace PointOfSale.Data.Tests;

/// <summary>
/// Exercises PointOfSaleDbContext against a real (in-memory) Sqlite connection rather than mocks,
/// so the FK/unique-constraint behavior configured in OnModelCreating is actually verified, not
/// just assumed from the fluent config.
/// </summary>
public class StockAdjustmentTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PointOfSaleDbContext _db;

    public StockAdjustmentTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PointOfSaleDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new PointOfSaleDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ApplyingAStockAdjustment_UpdatesProductAndRecordsAuditRow()
    {
        var category = new Category { Name = "Beverages" };
        var product = new Product { Sku = "BEV-001", Name = "Cola", Price = 1.29m, Category = category, QuantityOnHand = 100 };
        _db.Categories.Add(category);
        _db.Products.Add(product);
        _db.SaveChanges();

        product.QuantityOnHand -= 3;
        _db.StockAdjustments.Add(new StockAdjustment
        {
            Product = product,
            ChangeQuantity = -3,
            Reason = StockAdjustmentReason.Sale,
            CreatedAtUtc = DateTime.UtcNow,
            ResultingQuantityOnHand = product.QuantityOnHand,
        });
        _db.SaveChanges();

        using var freshDb = new PointOfSaleDbContext(new DbContextOptionsBuilder<PointOfSaleDbContext>().UseSqlite(_connection).Options);
        var reloaded = freshDb.Products.Single(p => p.Sku == "BEV-001");
        var adjustment = freshDb.StockAdjustments.Single(a => a.ProductId == reloaded.Id);

        Assert.Equal(97, reloaded.QuantityOnHand);
        Assert.Equal(-3, adjustment.ChangeQuantity);
        Assert.Equal(97, adjustment.ResultingQuantityOnHand);
        Assert.Equal(StockAdjustmentReason.Sale, adjustment.Reason);
    }

    [Fact]
    public void DuplicateSku_ViolatesUniqueIndex()
    {
        var category = new Category { Name = "Beverages" };
        _db.Categories.Add(category);
        _db.Products.Add(new Product { Sku = "BEV-001", Name = "Cola", Price = 1.29m, Category = category });
        _db.SaveChanges();

        _db.Products.Add(new Product { Sku = "BEV-001", Name = "Cola (duplicate SKU)", Price = 1.29m, Category = category });

        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void DeletingACategoryWithProducts_IsRejectedByTheForeignKeyConstraint()
    {
        var category = new Category { Name = "Beverages" };
        _db.Categories.Add(category);
        _db.Products.Add(new Product { Sku = "BEV-001", Name = "Cola", Price = 1.29m, Category = category });
        _db.SaveChanges();

        // Load the Category into a fresh, untracked context (without its Products) so EF's
        // in-memory change tracker doesn't short-circuit the delete on its own — this way the
        // DELETE actually reaches Sqlite, and it's the DeleteBehavior.Restrict-backed FK constraint
        // in the database itself that rejects it, independent of the API's own AnyAsync pre-check
        // in CategoryEndpoints.
        using var freshDb = new PointOfSaleDbContext(new DbContextOptionsBuilder<PointOfSaleDbContext>().UseSqlite(_connection).Options);
        var reloadedCategory = freshDb.Categories.Single(c => c.Id == category.Id);
        freshDb.Categories.Remove(reloadedCategory);

        Assert.Throws<DbUpdateException>(() => freshDb.SaveChanges());
    }
}
