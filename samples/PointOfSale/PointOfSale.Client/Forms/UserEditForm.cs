using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class UserEditForm : Form
{
    private static readonly UserRole[] Roles = [UserRole.Cashier, UserRole.Manager];

    private readonly TextBox _nameBox;
    private readonly ComboBox _roleBox;
    private readonly CheckBox _activeBox;
    private readonly TextBox _pinBox;
    private readonly bool _isEdit;

    public string DisplayName => _nameBox.Text;
    public UserRole Role => Roles[_roleBox.SelectedIndex];
    public bool IsActive => _activeBox.Checked;
    public string Pin => _pinBox.Text;
    public string? NewPin => string.IsNullOrWhiteSpace(_pinBox.Text) ? null : _pinBox.Text;

    public UserEditForm(UserDto? existing)
    {
        _isEdit = existing is not null;

        Text = _isEdit ? "Edit User" : "Add User";
        ClientSize = new System.Drawing.Size(420, 490);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog).
        var title = Controls.Add(new Label { Text = _isEdit ? "Edit User" : "Add User", Left = 20, Top = 46, Width = 360 });
        PosStyle.Heading(title, fontSize: 24);

        Controls.Add(new Label { Text = "Display Name:", Left = 20, Top = 108, Width = 360 });
        _nameBox = Controls.Add(new TextBox { Left = 20, Top = 136, Width = 360, Height = 42, Text = existing?.DisplayName ?? string.Empty });

        Controls.Add(new Label { Text = "Role:", Left = 20, Top = 192, Width = 150 });
        _roleBox = Controls.Add(new ComboBox { Left = 20, Top = 220, Width = 360, Height = 42, DropDownStyle = ComboBoxStyle.DropDownList });
        _roleBox.Items.Add("Cashier");
        _roleBox.Items.Add("Manager");
        _roleBox.SelectedIndex = existing is not null && existing.Role == UserRole.Manager ? 1 : 0;

        _activeBox = Controls.Add(new CheckBox { Text = "Active", Left = 20, Top = 276, Width = 260, Checked = existing?.IsActive ?? true });

        Controls.Add(new Label
        {
            Text = _isEdit ? "New PIN (leave blank to keep current):" : "PIN:",
            Left = 20,
            Top = 320,
            Width = 380,
        });
        _pinBox = Controls.Add(new TextBox { Left = 20, Top = 348, Width = 360, Height = 42, UseSystemPasswordChar = true });

        var saveButton = Controls.Add(new Button { Text = "Save", Left = 20, Top = 410, Width = 175, Height = 52 });
        PosStyle.PrimaryButton(saveButton);
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
                return;
            if (!_isEdit && string.IsNullOrWhiteSpace(_pinBox.Text))
                return;

            DialogResult = DialogResult.OK;
        };

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 205, Top = 410, Width = 175, Height = 52 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }
}
