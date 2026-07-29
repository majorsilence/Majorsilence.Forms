using Microsoft.EntityFrameworkCore;
using PointOfSale.Api.Mapping;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("Manager");

        group.MapGet("/", async (PointOfSaleDbContext db) =>
            Results.Ok((await db.Users.OrderBy(u => u.DisplayName).ToListAsync()).Select(u => u.ToDto())));

        group.MapPost("/", async (UserCreateDto request, PointOfSaleDbContext db) =>
        {
            var (hash, salt) = PinHasher.HashPin(request.Pin);
            var user = new User { DisplayName = request.DisplayName, PinHash = hash, PinSalt = salt, Role = request.Role };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/api/users/{user.Id}", user.ToDto());
        });

        group.MapPut("/{id:int}", async (int id, UserUpdateDto request, PointOfSaleDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null)
                return Results.NotFound();

            user.DisplayName = request.DisplayName;
            user.Role = request.Role;
            user.IsActive = request.IsActive;

            if (!string.IsNullOrEmpty(request.NewPin))
            {
                var (hash, salt) = PinHasher.HashPin(request.NewPin);
                user.PinHash = hash;
                user.PinSalt = salt;
            }

            await db.SaveChangesAsync();
            return Results.Ok(user.ToDto());
        });
    }
}
