using PointOfSale.Contracts;

namespace PointOfSale.Data.Entities;

/// <summary>
/// Audit trail row. Every mutation of Product.QuantityOnHand inserts one of these in the
/// same transaction, so stock history can be reconstructed without recomputing a running sum.
/// </summary>
public class StockAdjustment
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Signed: negative for a sale decrement, positive for a restock/correction/void reversal.</summary>
    public int ChangeQuantity { get; set; }

    public StockAdjustmentReason Reason { get; set; }

    public int? RelatedSaleId { get; set; }
    public Sale? RelatedSale { get; set; }

    public int? PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Snapshot of Product.QuantityOnHand immediately after this adjustment was applied.</summary>
    public int ResultingQuantityOnHand { get; set; }
}
