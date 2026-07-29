using Microsoft.EntityFrameworkCore;
using PointOfSale.Api.Mapping;
using PointOfSale.Api.Security;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products").RequireAuthorization();

        group.MapGet("/", async (PointOfSaleDbContext db, string? search, int? categoryId, bool activeOnly = false) =>
        {
            var query = db.Products.Include(p => p.Category).AsQueryable();

            if (activeOnly)
                query = query.Where(p => p.IsActive);

            if (categoryId is not null)
                query = query.Where(p => p.CategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Sku.Contains(term) || p.Name.Contains(term));
            }

            var products = await query.OrderBy(p => p.Name).ToListAsync();
            return Results.Ok(products.Select(p => p.ToDto()));
        });

        group.MapGet("/{id:int}", async (int id, PointOfSaleDbContext db) =>
        {
            var product = await db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            return product is null ? Results.NotFound() : Results.Ok(product.ToDto());
        });

        group.MapPost("/", async (ProductCreateDto request, PointOfSaleDbContext db) =>
        {
            var product = new Product
            {
                Sku = request.Sku,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                CategoryId = request.CategoryId,
                QuantityOnHand = request.QuantityOnHand,
                ReorderThreshold = request.ReorderThreshold,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            await db.Entry(product).Reference(p => p.Category).LoadAsync();
            return Results.Created($"/api/products/{product.Id}", product.ToDto());
        }).RequireAuthorization("Manager");

        group.MapPut("/{id:int}", async (int id, ProductUpdateDto request, PointOfSaleDbContext db) =>
        {
            var product = await db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
            if (product is null)
                return Results.NotFound();

            product.Sku = request.Sku;
            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.CategoryId = request.CategoryId;
            product.ReorderThreshold = request.ReorderThreshold;
            product.IsActive = request.IsActive;
            await db.SaveChangesAsync();
            await db.Entry(product).Reference(p => p.Category).LoadAsync();
            return Results.Ok(product.ToDto());
        }).RequireAuthorization("Manager");

        group.MapDelete("/{id:int}", async (int id, PointOfSaleDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
                return Results.NotFound();

            // Soft-delete only: Sale/SaleLineItem history references this product.
            product.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Manager");

        group.MapPost("/{id:int}/stock-adjustments", async (int id, StockAdjustmentCreateDto request, HttpContext http, PointOfSaleDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
                return Results.NotFound();

            var newQuantity = product.QuantityOnHand + request.ChangeQuantity;
            if (newQuantity < 0)
                return Results.BadRequest("Adjustment would result in negative stock.");

            product.QuantityOnHand = newQuantity;

            var adjustment = new StockAdjustment
            {
                ProductId = product.Id,
                ChangeQuantity = request.ChangeQuantity,
                Reason = request.Reason,
                PerformedByUserId = http.GetCurrentUserId(),
                CreatedAtUtc = DateTime.UtcNow,
                ResultingQuantityOnHand = newQuantity,
            };
            db.StockAdjustments.Add(adjustment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/products/{id}/stock-adjustments/{adjustment.Id}", adjustment.ToDto());
        }).RequireAuthorization("Manager");
    }
}
