namespace PointOfSale.Contracts;

public sealed record CategoryDto(int Id, string Name, string? Description);

public sealed record CategoryCreateDto(string Name, string? Description);

public sealed record CategoryUpdateDto(string Name, string? Description);
