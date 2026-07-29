namespace PointOfSale.Data.Entities;

public class SaleLineItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Denormalized so historical receipts/reports survive later product renames.</summary>
    public required string ProductNameSnapshot { get; set; }
    public decimal UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}
