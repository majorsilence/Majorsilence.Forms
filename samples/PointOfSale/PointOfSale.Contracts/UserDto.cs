namespace PointOfSale.Contracts;

public sealed record UserDto(int Id, string DisplayName, UserRole Role, bool IsActive);

/// <summary>Pin is write-only: it is hashed server-side and never returned in a UserDto.</summary>
public sealed record UserCreateDto(string DisplayName, string Pin, UserRole Role);

public sealed record UserUpdateDto(string DisplayName, UserRole Role, bool IsActive, string? NewPin);
