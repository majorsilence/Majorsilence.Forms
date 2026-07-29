namespace PointOfSale.Contracts;

public sealed record LoginResponse(
    string Token,
    int UserId,
    string DisplayName,
    UserRole Role,
    DateTime ExpiresAtUtc);
