using Microsoft.EntityFrameworkCore;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization();

        group.MapGet("/daily-summary", async (DateOnly date, PointOfSaleDbContext db) =>
        {
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);

            var sales = await db.Sales
                .Where(s => s.CreatedAtUtc >= dayStart && s.CreatedAtUtc < dayEnd && s.Status == SaleStatus.Completed)
                .ToListAsync();

            var cash = sales.Where(s => s.TenderType == TenderType.Cash).Sum(s => s.Total);
            var card = sales.Where(s => s.TenderType == TenderType.Card).Sum(s => s.Total);

            var summary = new DailySummaryDto(
                date,
                sales.Count,
                sales.Sum(s => s.Subtotal),
                sales.Sum(s => s.TaxTotal),
                sales.Sum(s => s.DiscountTotal),
                sales.Sum(s => s.Total),
                new TenderTypeTotals(cash, card));

            return Results.Ok(summary);
        });

        group.MapGet("/top-products", async (PointOfSaleDbContext db, DateTime? from, DateTime? to, int take = 10) =>
        {
            var effectiveTake = take <= 0 ? 10 : take;

            var query = db.SaleLineItems
                .Include(l => l.Sale)
                .Where(l => l.Sale!.Status == SaleStatus.Completed);

            if (from is not null)
                query = query.Where(l => l.Sale!.CreatedAtUtc >= from);
            if (to is not null)
                query = query.Where(l => l.Sale!.CreatedAtUtc <= to);

            var lineItems = await query.ToListAsync();

            var top = lineItems
                .GroupBy(l => new { l.ProductId, l.ProductNameSnapshot })
                .Select(g => new TopProductDto(g.Key.ProductId, g.Key.ProductNameSnapshot, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
                .OrderByDescending(p => p.QuantitySold)
                .Take(effectiveTake)
                .ToList();

            return Results.Ok(top);
        });
    }
}
