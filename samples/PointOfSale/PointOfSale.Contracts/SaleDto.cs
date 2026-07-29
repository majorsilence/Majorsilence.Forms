namespace PointOfSale.Contracts;

public sealed record SaleLineItemRequest(int ProductId, int Quantity, decimal DiscountAmount);

public sealed record SaleCreateDto(
    SaleChannel Channel,
    TenderType TenderType,
    decimal AmountTendered,
    decimal DiscountTotal,
    IReadOnlyList<SaleLineItemRequest> LineItems);

public sealed record SaleLineItemDto(
    int Id,
    int ProductId,
    string ProductNameSnapshot,
    decimal UnitPriceSnapshot,
    int Quantity,
    decimal DiscountAmount,
    decimal LineTotal);

public sealed record SaleReceiptDto(
    int Id,
    string SaleNumber,
    int CashierUserId,
    string CashierDisplayName,
    SaleChannel Channel,
    SaleStatus Status,
    decimal Subtotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal Total,
    TenderType TenderType,
    decimal? AmountTendered,
    decimal? ChangeDue,
    DateTime CreatedAtUtc,
    IReadOnlyList<SaleLineItemDto> LineItems);

public sealed record SaleSummaryDto(
    int Id,
    string SaleNumber,
    string CashierDisplayName,
    SaleChannel Channel,
    SaleStatus Status,
    decimal Total,
    TenderType TenderType,
    DateTime CreatedAtUtc);

/// <summary>Per-line stock validation failure returned as the body of a 409 from POST /api/sales.</summary>
public sealed record SaleLineItemErrorDto(int ProductId, string ProductName, int RequestedQuantity, int AvailableQuantity);

public sealed record SaleCreateErrorDto(string Message, IReadOnlyList<SaleLineItemErrorDto> LineErrors);
