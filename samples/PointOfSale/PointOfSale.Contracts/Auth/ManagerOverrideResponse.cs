namespace PointOfSale.Contracts;

public sealed record ManagerOverrideResponse(string Token, DateTime ExpiresAtUtc);
