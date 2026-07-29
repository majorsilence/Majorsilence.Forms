namespace PointOfSale.Data.Entities;

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int QuantityOnHand { get; set; }
    public int ReorderThreshold { get; set; } = 5;

    /// <summary>Soft-delete flag — products are never hard-deleted since Sales reference them.</summary>
    public bool IsActive { get; set; } = true;

    public List<SaleLineItem> SaleLineItems { get; set; } = [];
    public List<StockAdjustment> StockAdjustments { get; set; } = [];
}
