using PointOfSale.Contracts;

namespace PointOfSale.Data.Entities;

public class User
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>PBKDF2 hash of the PIN. See Security/PinHasher in PointOfSale.Api.</summary>
    public required string PinHash { get; set; }
    public required string PinSalt { get; set; }

    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
