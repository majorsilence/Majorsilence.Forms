using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Forms;
using PointOfSale.Client.Models;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class InventoryPanel : BasePanel
{
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private List<ProductDto> _products = [];

    public InventoryPanel(ApiClient api, SessionState session) : base(api, session)
    {
        AutoScroll = true;

        var heading = Controls.Add(new Label { Text = "Inventory", Left = 10, Top = 10, Width = 400 });
        PosStyle.Heading(heading);

        Controls.Add(new Label { Text = "Status flags items at or below their reorder threshold.", Left = 10, Top = 56, Width = 600 });

        var adjustButton = Controls.Add(new Button { Text = "Adjust Stock", Left = 10, Top = 90, Width = 180, Height = 48 });
        PosStyle.PrimaryButton(adjustButton);
        adjustButton.Click += async (_, _) => await AdjustAsync();

        _grid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 150,
            Width = 900,
            Height = 450,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        });
        GridColumns.AddBound(_grid, nameof(InventoryRow.Sku), "SKU", 110);
        GridColumns.AddBound(_grid, nameof(InventoryRow.Name), "Name", 280);
        GridColumns.AddBound(_grid, nameof(InventoryRow.QuantityOnHand), "On Hand", 110);
        GridColumns.AddBound(_grid, nameof(InventoryRow.ReorderThreshold), "Reorder At", 130);
        GridColumns.AddBound(_grid, nameof(InventoryRow.Status), "Status", 130);

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 610, Width = 900 });
    }

    public override void LoadPanel() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            _products = await Api.GetProductsAsync(activeOnly: true);
            var rows = _products
                .Select(p => new InventoryRow
                {
                    Sku = p.Sku,
                    Name = p.Name,
                    QuantityOnHand = p.QuantityOnHand,
                    ReorderThreshold = p.ReorderThreshold,
                    Status = p.QuantityOnHand <= p.ReorderThreshold ? "LOW STOCK" : "OK",
                })
                .ToList();
            GridColumns.Rebind(_grid, rows);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load inventory: {ex.Message}";
        }
    }

    private async Task AdjustAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _products.Count)
        {
            _statusLabel.Text = "Select a product first.";
            return;
        }

        var product = _products[index];
        using var form = new StockAdjustmentForm(product);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.AdjustStockAsync(product.Id, new StockAdjustmentCreateDto(form.ChangeQuantity, form.Reason, null));
            await RefreshAsync();
        }
        catch (ApiException ex)
        {
            _statusLabel.Text = $"Could not adjust stock: {ex.Body}";
        }
    }
}
