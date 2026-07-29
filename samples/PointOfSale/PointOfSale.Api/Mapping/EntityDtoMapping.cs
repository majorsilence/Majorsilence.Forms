using PointOfSale.Contracts;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Mapping;

public static class EntityDtoMapping
{
    public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name, c.Description);

    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Price,
        p.CategoryId, p.Category?.Name ?? string.Empty,
        p.QuantityOnHand, p.ReorderThreshold, p.IsActive);

    public static UserDto ToDto(this User u) => new(u.Id, u.DisplayName, u.Role, u.IsActive);

    public static StockAdjustmentDto ToDto(this StockAdjustment a) => new(
        a.Id, a.ProductId, a.ChangeQuantity, a.Reason,
        a.RelatedSaleId, a.PerformedByUserId, a.CreatedAtUtc, a.ResultingQuantityOnHand);

    public static SaleLineItemDto ToDto(this SaleLineItem l) => new(
        l.Id, l.ProductId, l.ProductNameSnapshot, l.UnitPriceSnapshot, l.Quantity, l.DiscountAmount, l.LineTotal);

    public static SaleReceiptDto ToReceiptDto(this Sale s) => new(
        s.Id, s.SaleNumber, s.CashierUserId, s.CashierUser?.DisplayName ?? string.Empty,
        s.Channel, s.Status, s.Subtotal, s.TaxTotal, s.DiscountTotal, s.Total,
        s.TenderType, s.AmountTendered, s.ChangeDue, s.CreatedAtUtc,
        s.LineItems.Select(l => l.ToDto()).ToList());

    public static SaleSummaryDto ToSummaryDto(this Sale s) => new(
        s.Id, s.SaleNumber, s.CashierUser?.DisplayName ?? string.Empty,
        s.Channel, s.Status, s.Total, s.TenderType, s.CreatedAtUtc);
}
