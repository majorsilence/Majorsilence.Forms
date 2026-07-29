using Microsoft.EntityFrameworkCore;
using PointOfSale.Api.Security;
using PointOfSale.Contracts;
using PointOfSale.Data;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, PointOfSaleDbContext db, JwtTokenService tokens) =>
        {
            var user = await FindUserByPinAsync(db, request.Pin);
            if (user is null)
                return Results.Unauthorized();

            var (token, expires) = tokens.CreateToken(user, tokens.DefaultLifetime);
            return Results.Ok(new LoginResponse(token, user.Id, user.DisplayName, user.Role, expires));
        });

        group.MapPost("/manager-override", async (ManagerOverrideRequest request, PointOfSaleDbContext db, JwtTokenService tokens) =>
        {
            var user = await FindUserByPinAsync(db, request.Pin);
            if (user is null || user.Role != UserRole.Manager)
                return Results.Unauthorized();

            var (token, expires) = tokens.CreateToken(user, tokens.OverrideLifetime);
            return Results.Ok(new ManagerOverrideResponse(token, expires));
        }).RequireAuthorization();
    }

    private static async Task<User?> FindUserByPinAsync(PointOfSaleDbContext db, string pin)
    {
        var activeUsers = await db.Users.Where(u => u.IsActive).ToListAsync();
        return activeUsers.FirstOrDefault(u => PinHasher.VerifyPin(pin, u.PinHash, u.PinSalt));
    }
}
