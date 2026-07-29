namespace PointOfSale.Client.Models;

/// <summary>Local view model for the checkout cart grid — never the EF/DTO Product type directly.</summary>
public sealed class CartLine
{
    public required int ProductId { get; set; }
    public required string Name { get; set; }
    public required decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }

    public decimal LineTotal => (UnitPrice * Quantity) - DiscountAmount;
}
