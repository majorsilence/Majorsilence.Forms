using Microsoft.EntityFrameworkCore;
using PointOfSale.Api.Mapping;
using PointOfSale.Api.Security;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class SaleEndpoints
{
    public static void MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales").WithTags("Sales").RequireAuthorization();

        group.MapPost("/", CreateSaleAsync);

        group.MapPost("/{id:int}/void", async (int id, HttpContext http, PointOfSaleDbContext db) =>
        {
            var sale = await db.Sales
                .Include(s => s.LineItems)
                .Include(s => s.CashierUser)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale is null)
                return Results.NotFound();

            if (sale.Status == SaleStatus.Voided)
                return Results.Conflict("Sale is already voided.");

            sale.Status = SaleStatus.Voided;
            sale.VoidedByUserId = http.GetCurrentUserId();

            foreach (var line in sale.LineItems)
            {
                var product = await db.Products.FindAsync(line.ProductId);
                if (product is null)
                    continue;

                product.QuantityOnHand += line.Quantity;

                db.StockAdjustments.Add(new StockAdjustment
                {
                    ProductId = product.Id,
                    ChangeQuantity = line.Quantity,
                    Reason = StockAdjustmentReason.VoidReversal,
                    RelatedSaleId = sale.Id,
                    PerformedByUserId = http.GetCurrentUserId(),
                    CreatedAtUtc = DateTime.UtcNow,
                    ResultingQuantityOnHand = product.QuantityOnHand,
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(sale.ToReceiptDto());
        }).RequireAuthorization("Manager");

        group.MapGet("/", async (DateTime? from, DateTime? to, int? cashierUserId, PointOfSaleDbContext db) =>
        {
            var query = db.Sales.Include(s => s.CashierUser).AsQueryable();

            if (from is not null)
                query = query.Where(s => s.CreatedAtUtc >= from);
            if (to is not null)
                query = query.Where(s => s.CreatedAtUtc <= to);
            if (cashierUserId is not null)
                query = query.Where(s => s.CashierUserId == cashierUserId);

            var sales = await query.OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
            return Results.Ok(sales.Select(s => s.ToSummaryDto()));
        });

        group.MapGet("/{id:int}", async (int id, PointOfSaleDbContext db) =>
        {
            var sale = await db.Sales
                .Include(s => s.LineItems)
                .Include(s => s.CashierUser)
                .FirstOrDefaultAsync(s => s.Id == id);
            return sale is null ? Results.NotFound() : Results.Ok(sale.ToReceiptDto());
        });
    }

    private static async Task<IResult> CreateSaleAsync(
        SaleCreateDto request, HttpContext http, PointOfSaleDbContext db, IConfiguration config)
    {
        var userId = http.GetCurrentUserId();
        if (userId is null)
            return Results.Unauthorized();

        if (request.LineItems.Count == 0)
            return Results.BadRequest("A sale requires at least one line item.");

        var productIds = request.LineItems.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var lineErrors = new List<SaleLineItemErrorDto>();
        foreach (var line in request.LineItems)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
            {
                lineErrors.Add(new SaleLineItemErrorDto(line.ProductId, "(unknown product)", line.Quantity, 0));
                continue;
            }

            if (!SaleCalculator.HasSufficientStock(product.QuantityOnHand, line.Quantity))
                lineErrors.Add(new SaleLineItemErrorDto(product.Id, product.Name, line.Quantity, product.QuantityOnHand));
        }

        if (lineErrors.Count > 0)
            return Results.Conflict(new SaleCreateErrorDto("Insufficient stock for one or more items.", lineErrors));

        // Totals are always computed server-side from current product prices — never trust client-sent prices.
        var taxRate = config.GetValue("Tax:Rate", 0.08m);

        var lineItems = new List<SaleLineItem>();
        var lineTotals = new List<decimal>();

        foreach (var line in request.LineItems)
        {
            var product = products[line.ProductId];
            var lineTotal = SaleCalculator.ComputeLineTotal(product.Price, line.Quantity, line.DiscountAmount);
            lineTotals.Add(lineTotal);

            lineItems.Add(new SaleLineItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                UnitPriceSnapshot = product.Price,
                Quantity = line.Quantity,
                DiscountAmount = line.DiscountAmount,
                LineTotal = lineTotal,
            });
        }

        var discountTotal = request.DiscountTotal;
        var (subtotal, taxTotal, total) = SaleCalculator.ComputeTotals(lineTotals, discountTotal, taxRate);

        decimal? amountTendered = request.TenderType == TenderType.Cash ? request.AmountTendered : total;
        decimal? changeDue = request.TenderType == TenderType.Cash ? (amountTendered - total) : 0m;

        if (request.TenderType == TenderType.Cash && amountTendered < total)
            return Results.BadRequest("Amount tendered is less than the sale total.");

        var sale = new Sale
        {
            SaleNumber = $"S{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            CashierUserId = userId.Value,
            Channel = request.Channel,
            Status = SaleStatus.Completed,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            DiscountTotal = discountTotal,
            Total = total,
            TenderType = request.TenderType,
            AmountTendered = amountTendered,
            ChangeDue = changeDue,
            CreatedAtUtc = DateTime.UtcNow,
            LineItems = lineItems,
        };
        db.Sales.Add(sale);

        foreach (var line in request.LineItems)
        {
            var product = products[line.ProductId];
            product.QuantityOnHand -= line.Quantity;

            db.StockAdjustments.Add(new StockAdjustment
            {
                Product = product,
                ChangeQuantity = -line.Quantity,
                Reason = StockAdjustmentReason.Sale,
                RelatedSale = sale,
                PerformedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                ResultingQuantityOnHand = product.QuantityOnHand,
            });
        }

        await db.SaveChangesAsync();

        await db.Entry(sale).Reference(s => s.CashierUser).LoadAsync();
        return Results.Created($"/api/sales/{sale.Id}", sale.ToReceiptDto());
    }
}
