using PointOfSale.Data;
using Xunit;

namespace PointOfSale.Data.Tests;

public class SaleCalculatorTests
{
    [Fact]
    public void ComputeLineTotal_MultipliesPriceByQuantityThenSubtractsDiscount()
    {
        var total = SaleCalculator.ComputeLineTotal(unitPrice: 1.29m, quantity: 3, discountAmount: 0.50m);

        Assert.Equal(3.37m, total);
    }

    [Fact]
    public void ComputeTotals_AppliesSaleLevelDiscountBeforeTax()
    {
        var (subtotal, taxTotal, total) = SaleCalculator.ComputeTotals(
            lineTotals: [2.58m, 3.99m],
            discountTotal: 1.00m,
            taxRate: 0.08m);

        Assert.Equal(6.57m, subtotal);
        Assert.Equal(0.45m, taxTotal); // round(5.57 * 0.08, 2) = 0.45
        Assert.Equal(6.02m, total);
    }

    [Fact]
    public void ComputeTotals_WithNoLines_ReturnsZero()
    {
        var (subtotal, taxTotal, total) = SaleCalculator.ComputeTotals([], discountTotal: 0, taxRate: 0.08m);

        Assert.Equal(0m, subtotal);
        Assert.Equal(0m, taxTotal);
        Assert.Equal(0m, total);
    }

    [Theory]
    [InlineData(10, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(4, 5, false)]
    [InlineData(0, 1, false)]
    public void HasSufficientStock_ComparesOnHandToRequested(int onHand, int requested, bool expected)
    {
        Assert.Equal(expected, SaleCalculator.HasSufficientStock(onHand, requested));
    }
}
