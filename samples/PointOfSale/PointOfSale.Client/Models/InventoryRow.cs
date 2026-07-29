namespace PointOfSale.Client.Models;

/// <summary>
/// Grid view model for InventoryPanel. Low-stock highlighting is done via this computed Status
/// column rather than DataGridView.CellFormatting/per-row DefaultCellStyle: neither hook is wired
/// through to the base DataGridView's renderer in this framework version (RaiseCellFormatting/
/// RaiseRowFormatting are both empty stubs, and RenderRow never reads a row's DefaultCellStyle) —
/// so any per-row background color set that way is silently ignored. A plain text column always renders.
/// </summary>
public sealed class InventoryRow
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required int QuantityOnHand { get; init; }
    public required int ReorderThreshold { get; init; }
    public required string Status { get; init; }
}
