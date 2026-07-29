using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PointOfSale.Data.Entities;

namespace PointOfSale.Api.Security;

public sealed class JwtTokenService(IConfiguration config)
{
    private readonly string _signingKey = config["Jwt:SigningKey"]
        ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
    private readonly string _issuer = config["Jwt:Issuer"] ?? "PointOfSale.Api";
    private readonly string _audience = config["Jwt:Audience"] ?? "PointOfSale.Client";

    public TimeSpan DefaultLifetime { get; } = TimeSpan.FromMinutes(config.GetValue("Jwt:TokenLifetimeMinutes", 480));
    public TimeSpan OverrideLifetime { get; } = TimeSpan.FromMinutes(config.GetValue("Jwt:OverrideTokenLifetimeMinutes", 2));

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user, TimeSpan lifetime)
    {
        var expires = DateTime.UtcNow.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
