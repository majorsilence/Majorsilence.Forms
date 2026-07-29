namespace PointOfSale.Contracts;

public sealed record StockAdjustmentCreateDto(int ChangeQuantity, StockAdjustmentReason Reason, string? Note);

public sealed record StockAdjustmentDto(
    int Id,
    int ProductId,
    int ChangeQuantity,
    StockAdjustmentReason Reason,
    int? RelatedSaleId,
    int? PerformedByUserId,
    DateTime CreatedAtUtc,
    int ResultingQuantityOnHand);
