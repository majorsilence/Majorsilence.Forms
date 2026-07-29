using Microsoft.EntityFrameworkCore;
using PointOfSale.Api.Mapping;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Categories").RequireAuthorization();

        group.MapGet("/", async (PointOfSaleDbContext db) =>
            Results.Ok((await db.Categories.OrderBy(c => c.Name).ToListAsync()).Select(c => c.ToDto())));

        group.MapPost("/", async (CategoryCreateDto request, PointOfSaleDbContext db) =>
        {
            var category = new Category { Name = request.Name, Description = request.Description };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{category.Id}", category.ToDto());
        }).RequireAuthorization("Manager");

        group.MapPut("/{id:int}", async (int id, CategoryUpdateDto request, PointOfSaleDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return Results.NotFound();

            category.Name = request.Name;
            category.Description = request.Description;
            await db.SaveChangesAsync();
            return Results.Ok(category.ToDto());
        }).RequireAuthorization("Manager");

        group.MapDelete("/{id:int}", async (int id, PointOfSaleDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return Results.NotFound();

            var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
                return Results.Conflict("Category still has products assigned to it.");

            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Manager");
    }
}
