using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Forms;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class ProductsPanel : BasePanel
{
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private List<ProductDto> _products = [];
    private List<CategoryDto> _categories = [];

    public ProductsPanel(ApiClient api, SessionState session) : base(api, session)
    {
        AutoScroll = true;

        var heading = Controls.Add(new Label { Text = "Products", Left = 10, Top = 10, Width = 300 });
        PosStyle.Heading(heading);

        var addButton = Controls.Add(new Button { Text = "Add", Left = 10, Top = 60, Width = 120, Height = 48 });
        PosStyle.PrimaryButton(addButton);
        addButton.Click += async (_, _) => await AddAsync();

        var editButton = Controls.Add(new Button { Text = "Edit", Left = 140, Top = 60, Width = 120, Height = 48 });
        PosStyle.SecondaryButton(editButton);
        editButton.Click += async (_, _) => await EditAsync();

        var deactivateButton = Controls.Add(new Button { Text = "Deactivate", Left = 270, Top = 60, Width = 150, Height = 48 });
        PosStyle.CautionButton(deactivateButton);
        deactivateButton.Click += async (_, _) => await DeactivateAsync();

        _grid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 120,
            Width = 1000,
            Height = 480,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        });
        GridColumns.AddBound(_grid, nameof(ProductDto.Sku), "SKU", 110);
        GridColumns.AddBound(_grid, nameof(ProductDto.Name), "Name", 260);
        GridColumns.AddBound(_grid, nameof(ProductDto.CategoryName), "Category", 160);
        GridColumns.AddBound(_grid, nameof(ProductDto.Price), "Price", 110);
        GridColumns.AddBound(_grid, nameof(ProductDto.QuantityOnHand), "On Hand", 110);
        GridColumns.AddBound(_grid, nameof(ProductDto.IsActive), "Active", 90);

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 610, Width = 1000 });
    }

    public override void LoadPanel() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            _categories = await Api.GetCategoriesAsync();
            _products = await Api.GetProductsAsync();
            GridColumns.Rebind(_grid, _products);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load products: {ex.Message}";
        }
    }

    private async Task AddAsync()
    {
        if (_categories.Count == 0)
        {
            _statusLabel.Text = "Create a category first.";
            return;
        }

        using var form = new ProductEditForm(null, _categories);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.CreateProductAsync(new ProductCreateDto(
                form.Sku, form.ProductName, form.Description, form.Price, form.CategoryId, form.QuantityOnHand, form.ReorderThreshold));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not create product: {ex.Message}";
        }
    }

    private async Task EditAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _products.Count)
            return;

        var product = _products[index];
        using var form = new ProductEditForm(product, _categories);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.UpdateProductAsync(product.Id, new ProductUpdateDto(
                form.Sku, form.ProductName, form.Description, form.Price, form.CategoryId, form.ReorderThreshold, form.IsActive));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not update product: {ex.Message}";
        }
    }

    private async Task DeactivateAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _products.Count)
            return;

        try
        {
            await Api.DeleteProductAsync(_products[index].Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not deactivate product: {ex.Message}";
        }
    }
}
