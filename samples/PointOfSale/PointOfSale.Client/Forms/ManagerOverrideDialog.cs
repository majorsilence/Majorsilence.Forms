using Majorsilence.Forms;
using PointOfSale.Client.Controls;
using PointOfSale.Client.Services;

namespace PointOfSale.Client.Forms;

/// <summary>
/// PIN-gated confirmation for actions a Cashier can't perform on their own (removing a cart
/// line, applying a discount). Validates the PIN against the server via /api/auth/manager-override
/// — a real check, not just a client-side prompt — but the elevated token it returns isn't needed
/// again here since nothing downstream calls a Manager-only endpoint on the caller's behalf.
/// </summary>
public class ManagerOverrideDialog : Form
{
    private readonly ApiClient _api;
    private readonly Label _statusLabel;
    private readonly Button _authorizeButton;
    private string _pin = string.Empty;
    private readonly Label _pinDisplay;

    public ManagerOverrideDialog(ApiClient api)
    {
        _api = api;

        Text = "Manager Approval Required";
        ClientSize = new System.Drawing.Size(340, 740);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // FormTitleBar is a fixed 34px; content added directly to a Form sits behind it unless
        // the first row starts at Top >= ~40 (see the note in TenderDialog). Every element below
        // is positioned from the actual bottom edge of the one above it (Top + Height + ~14px gap)
        // — NumericKeypad is 272x366 (3x4 84px tiles + 10px gaps), so eyeballing offsets here is
        // what caused the last two rounds of overlap bugs.
        var title = Controls.Add(new Label { Text = "Manager Approval", Left = 20, Top = 50, Width = 300 });
        PosStyle.Heading(title, fontSize: 24); // Height = 44, bottom = 94

        Controls.Add(new Label { Text = "Enter manager PIN:", Left = 20, Top = 108, Width = 280, Height = 36 }); // bottom = 144

        _pinDisplay = Controls.Add(new Label
        {
            Text = string.Empty,
            Left = 20,
            Top = 158,
            Width = 272,
            Height = 48,
        }); // bottom = 206
        PosStyle.Display(_pinDisplay);

        var keypad = Controls.Add(new NumericKeypad { Left = 20, Top = 220 }); // bottom = 220 + 366 = 586
        keypad.DigitPressed += digit => { if (_pin.Length < 8) _pin += digit; RefreshDisplay(); };
        keypad.BackspacePressed += () => { if (_pin.Length > 0) _pin = _pin[..^1]; RefreshDisplay(); };
        keypad.ClearPressed += () => { _pin = string.Empty; RefreshDisplay(); };

        _authorizeButton = Controls.Add(new Button { Text = "Authorize", Left = 20, Top = 600, Width = 150, Height = 54 }); // bottom = 654
        PosStyle.PrimaryButton(_authorizeButton);
        _authorizeButton.Click += async (_, _) => await AuthorizeAsync();

        var cancelButton = Controls.Add(new Button { Text = "Cancel", Left = 180, Top = 600, Width = 130, Height = 54 });
        PosStyle.SecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        _statusLabel = Controls.Add(new Label { Text = string.Empty, Left = 20, Top = 668, Width = 300, Height = 36 });
    }

    private void RefreshDisplay() => _pinDisplay.Text = new string('•', _pin.Length);

    private async Task AuthorizeAsync()
    {
        if (_pin.Length == 0)
            return;

        _authorizeButton.Enabled = false;
        _statusLabel.Text = "Checking...";

        try
        {
            await _api.ManagerOverrideAsync(_pin);
            DialogResult = DialogResult.OK;
        }
        catch (ApiException)
        {
            _statusLabel.Text = "Not a valid manager PIN.";
            _pin = string.Empty;
            RefreshDisplay();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _authorizeButton.Enabled = true;
        }
    }
}
