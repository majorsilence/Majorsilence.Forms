using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class ProductEditForm : Form
{
    private readonly TextBox _skuBox;
    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;
    private readonly NumericUpDown _priceBox;
    private readonly ComboBox _categoryBox;
    private readonly NumericUpDown _quantityBox;
    private readonly NumericUpDown _reorderBox;
    private readonly CheckBox _activeBox;
    private readonly bool _isEdit;

    public string Sku => _skuBox.Text;
    // `new`: this is the product being edited, not the assembly metadata that Control/WindowBase's
    // ProductName reports. WinForms has the same collision -- Control.ProductName is inherited by every
    // Form there too -- so a migrated app hits this exact warning and answers it the same way.
    public new string ProductName => _nameBox.Text;
    public string? Description => string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text;
    public decimal Price => _priceBox.Value;
    public int CategoryId => ((CategoryDto)_categoryBox.SelectedItem!).Id;
    public int QuantityOnHand => (int)_quantityBox.Value;
    public int ReorderThreshold => (int)_reorderBox.Value;
    public bool IsActive => _activeBox.Checked;

    public ProductEditForm(ProductDto? existing, List<CategoryDto> categories)
    {
        _isEdit = existing is not null;

        Text = _isEdit ? "Edit Product" : "Add Product";
        ClientSize = new System.Drawing.Size(460, 690);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog).
        var title = Controls.Add(new Label { Text = _isEdit ? "Edit Product" : "Add Product", Left = 20, Top = 46, Width = 400 });
        PosStyle.Heading(title, fontSize: 24);

        var y = 106;

        Controls.Add(new Label { Text = "SKU:", Left = 20, Top = y + 8, Width = 130 });
        _skuBox = Controls.Add(new TextBox { Left = 160, Top = y, Width = 260, Height = 42, Text = existing?.Sku ?? string.Empty });
        y += 54;

        Controls.Add(new Label { Text = "Name:", Left = 20, Top = y + 8, Width = 130 });
        _nameBox = Controls.Add(new TextBox { Left = 160, Top = y, Width = 260, Height = 42, Text = existing?.Name ?? string.Empty });
        y += 54;

        Controls.Add(new Label { Text = "Description:", Left = 20, Top = y + 8, Width = 130 });
        _descriptionBox = Controls.Add(new TextBox { Left = 160, Top = y, Width = 260, Height = 42, Text = existing?.Description ?? string.Empty });
        y += 54;

        Controls.Add(new Label { Text = "Price:", Left = 20, Top = y + 8, Width = 130 });
        _priceBox = Controls.Add(new NumericUpDown { Left = 160, Top = y, Width = 260, Height = 42, Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Increment = 0.25m, Value = existing?.Price ?? 0m });
        y += 54;

        Controls.Add(new Label { Text = "Category:", Left = 20, Top = y + 8, Width = 130 });
        _categoryBox = Controls.Add(new ComboBox { Left = 160, Top = y, Width = 260, Height = 42, DropDownStyle = ComboBoxStyle.DropDownList });
        _categoryBox.DataSource = categories;
        _categoryBox.DisplayMember = nameof(CategoryDto.Name);
        if (existing is not null)
        {
            var match = categories.FirstOrDefault(c => c.Id == existing.CategoryId);
            if (match is not null)
                _categoryBox.SelectedItem = match;
        }
        y += 54;

        Controls.Add(new Label { Text = "Qty on Hand:", Left = 20, Top = y + 8, Width = 130 });
        _quantityBox = Controls.Add(new NumericUpDown { Left = 160, Top = y, Width = 260, Height = 42, Minimum = 0, Maximum = 1_000_000, Value = existing?.QuantityOnHand ?? 0, ReadOnly = _isEdit });
        y += 54;

        Controls.Add(new Label { Text = "Reorder Threshold:", Left = 20, Top = y + 8, Width = 130 });
        _reorderBox = Controls.Add(new NumericUpDown { Left = 160, Top = y, Width = 260, Height = 42, Minimum = 0, Maximum = 100_000, Value = existing?.ReorderThreshold ?? 5 });
        y += 54;

        _activeBox = Controls.Add(new CheckBox { Text = "Active", Left = 160, Top = y, Width = 200, Checked = existing?.IsActive ?? true });
        y += 66;

        var saveButton = Controls.Add(new Button { Text = "Save", Left = 20, Top = y, Width = 190, Height = 52 });
        PosStyle.PrimaryButton(saveButton);
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_skuBox.Text) || string.IsNullOrWhiteSpace(_nameBox.Text) || _categoryBox.SelectedItem is null)
                return;

            DialogResult = DialogResult.OK;
        };

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 230, Top = y, Width = 190, Height = 52 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }
}
