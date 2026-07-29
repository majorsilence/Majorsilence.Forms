using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Forms;
using PointOfSale.Client.Services;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Panels;

public class CategoriesPanel : BasePanel
{
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private List<CategoryDto> _categories = [];

    public CategoriesPanel(ApiClient api, SessionState session) : base(api, session)
    {
        AutoScroll = true;

        var heading = Controls.Add(new Label { Text = "Categories", Left = 10, Top = 10, Width = 300 });
        PosStyle.Heading(heading);

        var addButton = Controls.Add(new Button { Text = "Add", Left = 10, Top = 60, Width = 120, Height = 48 });
        PosStyle.PrimaryButton(addButton);
        addButton.Click += async (_, _) => await AddAsync();

        var editButton = Controls.Add(new Button { Text = "Edit", Left = 140, Top = 60, Width = 120, Height = 48 });
        PosStyle.SecondaryButton(editButton);
        editButton.Click += async (_, _) => await EditAsync();

        var deleteButton = Controls.Add(new Button { Text = "Delete", Left = 270, Top = 60, Width = 120, Height = 48 });
        PosStyle.CautionButton(deleteButton);
        deleteButton.Click += async (_, _) => await DeleteAsync();

        _grid = Controls.Add(new DataGridView
        {
            Left = 10,
            Top = 120,
            Width = 800,
            Height = 460,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        });
        GridColumns.AddBound(_grid, nameof(CategoryDto.Name), "Name", 260);
        GridColumns.AddBound(_grid, nameof(CategoryDto.Description), "Description", 460);

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 10, Top = 590, Width = 800 });
    }

    public override void LoadPanel() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            _categories = await Api.GetCategoriesAsync();
            GridColumns.Rebind(_grid, _categories);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load categories: {ex.Message}";
        }
    }

    private async Task AddAsync()
    {
        using var form = new CategoryEditForm(null);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.CreateCategoryAsync(new CategoryCreateDto(form.CategoryName, form.CategoryDescription));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not create category: {ex.Message}";
        }
    }

    private async Task EditAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _categories.Count)
            return;

        var category = _categories[index];
        using var form = new CategoryEditForm(category);
        if (await form.ShowDialogAsync(FindForm()!) != DialogResult.OK)
            return;

        try
        {
            await Api.UpdateCategoryAsync(category.Id, new CategoryUpdateDto(form.CategoryName, form.CategoryDescription));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not update category: {ex.Message}";
        }
    }

    private async Task DeleteAsync()
    {
        var index = _grid.SelectedRowIndex;
        if (index < 0 || index >= _categories.Count)
            return;

        try
        {
            await Api.DeleteCategoryAsync(_categories[index].Id);
            await RefreshAsync();
        }
        catch (ApiException ex)
        {
            _statusLabel.Text = $"Could not delete category: {ex.Body}";
        }
    }
}
