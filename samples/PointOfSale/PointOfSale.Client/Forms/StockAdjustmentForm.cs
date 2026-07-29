using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class StockAdjustmentForm : Form
{
    private static readonly StockAdjustmentReason[] SelectableReasons = [StockAdjustmentReason.Restock, StockAdjustmentReason.ManualCorrection];

    private readonly NumericUpDown _changeBox;
    private readonly ComboBox _reasonBox;

    public int ChangeQuantity => (int)_changeBox.Value;
    public StockAdjustmentReason Reason => SelectableReasons[_reasonBox.SelectedIndex];

    public StockAdjustmentForm(ProductDto product)
    {
        Text = $"Adjust Stock — {product.Name}";
        ClientSize = new System.Drawing.Size(420, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog).
        var title = Controls.Add(new Label { Text = product.Name, Left = 20, Top = 46, Width = 380 });
        PosStyle.Heading(title, fontSize: 22);

        Controls.Add(new Label { Text = $"Current on hand: {product.QuantityOnHand}", Left = 20, Top = 98, Width = 380 });

        Controls.Add(new Label { Text = "Change (+/-):", Left = 20, Top = 146, Width = 150 });
        _changeBox = Controls.Add(new NumericUpDown { Left = 180, Top = 140, Width = 200, Height = 42, Minimum = -100000, Maximum = 100000, Value = 0 });

        Controls.Add(new Label { Text = "Reason:", Left = 20, Top = 202, Width = 150 });
        _reasonBox = Controls.Add(new ComboBox { Left = 180, Top = 196, Width = 200, Height = 42, DropDownStyle = ComboBoxStyle.DropDownList });
        _reasonBox.Items.Add("Restock");
        _reasonBox.Items.Add("Manual Correction");
        _reasonBox.SelectedIndex = 0;

        var saveButton = Controls.Add(new Button { Text = "Apply", Left = 20, Top = 300, Width = 175, Height = 52 });
        PosStyle.PrimaryButton(saveButton);
        saveButton.Click += (_, _) => DialogResult = DialogResult.OK;

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 205, Top = 300, Width = 175, Height = 52 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }
}
