using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class CategoryEditForm : Form
{
    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;

    public string CategoryName => _nameBox.Text;
    public string? CategoryDescription => string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text;

    public CategoryEditForm(CategoryDto? existing)
    {
        Text = existing is null ? "Add Category" : "Edit Category";
        ClientSize = new System.Drawing.Size(420, 370);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog).
        var title = Controls.Add(new Label { Text = existing is null ? "Add Category" : "Edit Category", Left = 20, Top = 46, Width = 360 });
        PosStyle.Heading(title, fontSize: 24);

        Controls.Add(new Label { Text = "Name:", Left = 20, Top = 110, Width = 120 });
        _nameBox = Controls.Add(new TextBox { Left = 20, Top = 138, Width = 360, Height = 42, Text = existing?.Name ?? string.Empty });

        Controls.Add(new Label { Text = "Description:", Left = 20, Top = 194, Width = 200 });
        _descriptionBox = Controls.Add(new TextBox { Left = 20, Top = 222, Width = 360, Height = 42, Text = existing?.Description ?? string.Empty });

        var saveButton = Controls.Add(new Button { Text = "Save", Left = 20, Top = 290, Width = 175, Height = 52 });
        PosStyle.PrimaryButton(saveButton);
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
                return;

            DialogResult = DialogResult.OK;
        };

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 205, Top = 290, Width = 175, Height = 52 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }
}
