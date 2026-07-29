namespace PointOfSale.Data;

/// <summary>Pure checkout math, kept separate from PointOfSale.Api's HTTP/EF concerns so it's unit-testable.</summary>
public static class SaleCalculator
{
    public static decimal ComputeLineTotal(decimal unitPrice, int quantity, decimal discountAmount) =>
        (unitPrice * quantity) - discountAmount;

    public static (decimal Subtotal, decimal TaxTotal, decimal Total) ComputeTotals(
        IReadOnlyList<decimal> lineTotals, decimal discountTotal, decimal taxRate)
    {
        var subtotal = lineTotals.Sum();
        var taxableAmount = subtotal - discountTotal;
        var taxTotal = Math.Round(taxableAmount * taxRate, 2);
        var total = taxableAmount + taxTotal;
        return (subtotal, taxTotal, total);
    }

    public static bool HasSufficientStock(int quantityOnHand, int requestedQuantity) =>
        quantityOnHand >= requestedQuantity;
}
