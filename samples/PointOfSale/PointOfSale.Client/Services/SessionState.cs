using PointOfSale.Contracts;

namespace PointOfSale.Client.Services;

public sealed class SessionState
{
    public required int UserId { get; init; }
    public required string DisplayName { get; init; }
    public required UserRole Role { get; init; }
    public required string Token { get; init; }
    public required SaleChannel Channel { get; init; }

    public bool IsManager => Role == UserRole.Manager;
}
