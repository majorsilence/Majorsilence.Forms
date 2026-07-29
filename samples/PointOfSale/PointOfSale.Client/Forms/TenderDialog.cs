using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Forms;

public class TenderDialog : Form
{
    private readonly decimal _total;
    private readonly RadioButton _cashOption;
    private readonly RadioButton _cardOption;
    private readonly Label _amountDisplay;
    private readonly Label _changeLabel;
    private readonly NumericKeypad _keypad;
    private string _amountText = string.Empty;

    public TenderType SelectedTenderType => _cashOption.Checked ? TenderType.Cash : TenderType.Card;
    public decimal AmountTendered { get; private set; }
    public decimal ChangeDue { get; private set; }

    public TenderDialog(decimal total)
    {
        _total = total;

        Text = "Tender Payment";
        ClientSize = new System.Drawing.Size(340, 780);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40. Every element below is positioned from the actual
        // bottom edge of the one above it (Top + Height + ~14px gap) — NumericKeypad is 272x366
        // (3x4 84px tiles + 10px gaps), so eyeballing offsets here is what caused earlier overlaps.
        var totalLabel = Controls.Add(new Label { Text = $"Total Due: {total:C}", Left = 20, Top = 50, Width = 300, Height = 44 });
        PosStyle.Heading(totalLabel, fontSize: 24); // bottom = 94

        _cashOption = Controls.Add(new RadioButton { Text = "Cash", Left = 20, Top = 110, Width = 130, Height = 36, Checked = true });
        _cardOption = Controls.Add(new RadioButton { Text = "Card", Left = 160, Top = 110, Width = 130, Height = 36 }); // bottom = 146
        _cashOption.CheckedChanged += (_, _) => UpdateAmountFieldState();
        _cardOption.CheckedChanged += (_, _) => UpdateAmountFieldState();

        Controls.Add(new Label { Text = "Amount Tendered:", Left = 20, Top = 156, Width = 280, Height = 36 }); // bottom = 192
        _amountDisplay = Controls.Add(new Label
        {
            Text = string.Empty,
            Left = 20,
            Top = 206,
            Width = 272,
            Height = 48,
        }); // bottom = 254
        PosStyle.Display(_amountDisplay);

        _keypad = Controls.Add(new NumericKeypad { Left = 20, Top = 268 }); // bottom = 268 + 366 = 634
        _keypad.DigitPressed += digit => { if (_amountText.Length < 9) _amountText += digit; RefreshAmountDisplay(); };
        _keypad.BackspacePressed += () => { if (_amountText.Length > 0) _amountText = _amountText[..^1]; RefreshAmountDisplay(); };
        _keypad.ClearPressed += () => { _amountText = string.Empty; RefreshAmountDisplay(); };

        _changeLabel = Controls.Add(new Label { Text = string.Empty, Left = 20, Top = 648, Width = 300, Height = 36 }); // bottom = 684

        var confirmButton = Controls.Add(new Button { Text = "Confirm", Left = 20, Top = 698, Width = 150, Height = 54 });
        PosStyle.PrimaryButton(confirmButton);
        confirmButton.Click += (_, _) => Confirm();

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 180, Top = 698, Width = 130, Height = 54 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }

    private void UpdateAmountFieldState()
    {
        if (_cardOption.Checked)
        {
            // Card is a mocked "approved" tender — no real payment integration in this sample.
            _amountText = _total.ToString("F2");
            RefreshAmountDisplay();
        }
    }

    private void RefreshAmountDisplay()
    {
        _amountDisplay.Text = _amountText;
        if (decimal.TryParse(_amountText, out var amount))
            _changeLabel.Text = amount >= _total ? $"Change Due: {(amount - _total):C}" : "Amount is less than total due.";
        else
            _changeLabel.Text = string.Empty;
    }

    private void Confirm()
    {
        if (!decimal.TryParse(_amountText, out var amount))
        {
            _changeLabel.Text = "Enter an amount.";
            return;
        }

        if (amount < _total)
        {
            _changeLabel.Text = "Amount is less than total due.";
            return;
        }

        AmountTendered = amount;
        ChangeDue = amount - _total;
        DialogResult = DialogResult.OK;
    }
}
