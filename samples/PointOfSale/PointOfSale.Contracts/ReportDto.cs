namespace PointOfSale.Contracts;

public sealed record TenderTypeTotals(decimal Cash, decimal Card);

public sealed record DailySummaryDto(
    DateOnly Date,
    int SaleCount,
    decimal GrossTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    TenderTypeTotals ByTenderType);

public sealed record TopProductDto(int ProductId, string Name, int QuantitySold, decimal Revenue);
