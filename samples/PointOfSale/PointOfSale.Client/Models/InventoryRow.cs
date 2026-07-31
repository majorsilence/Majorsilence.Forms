namespace PointOfSale.Client.Models;

/// <summary>
/// Grid view model for InventoryPanel. Low-stock highlighting is done via this computed Status
/// column, which keeps the panel readable on every backend. Colouring by value would now work too:
/// DataGridView.CellFormatting is a real event whose e.CellStyle colors are honored per paint, and the
/// renderer reads a row's DefaultCellStyle back color.
/// </summary>
public sealed class InventoryRow
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required int QuantityOnHand { get; init; }
    public required int ReorderThreshold { get; init; }
    public required string Status { get; init; }
}
