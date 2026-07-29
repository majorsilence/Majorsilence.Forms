namespace PointOfSale.Contracts;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int CategoryId,
    string CategoryName,
    int QuantityOnHand,
    int ReorderThreshold,
    bool IsActive);

public sealed record ProductCreateDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int CategoryId,
    int QuantityOnHand,
    int ReorderThreshold);

public sealed record ProductUpdateDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int CategoryId,
    int ReorderThreshold,
    bool IsActive);
