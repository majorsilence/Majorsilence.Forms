using PointOfSale.Contracts;

namespace PointOfSale.Data.Entities;

public class Sale
{
    public int Id { get; set; }
    public required string SaleNumber { get; set; }

    public int CashierUserId { get; set; }
    public User? CashierUser { get; set; }

    public SaleChannel Channel { get; set; }
    public SaleStatus Status { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }

    public TenderType TenderType { get; set; }
    public decimal? AmountTendered { get; set; }
    public decimal? ChangeDue { get; set; }

    /// <summary>Manager who authorized voiding this sale, if any.</summary>
    public int? VoidedByUserId { get; set; }
    public User? VoidedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<SaleLineItem> LineItems { get; set; } = [];
}
