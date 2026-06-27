using System.Drawing;
using WF = System.Windows.Forms;

namespace WinFormsInterop;

/// <summary>A real <see cref="System.Windows.Forms.Form"/> — the "legacy" form being hosted.</summary>
public sealed class SampleLegacyForm : WF.Form
{
    public SampleLegacyForm(string mode)
    {
        Text = $"Legacy System.Windows.Forms ({mode})";
        ClientSize = new Size(380, 160);
        StartPosition = WF.FormStartPosition.CenterParent;

        var label = new WF.Label
        {
            Text = "This is a real System.Windows.Forms.Form.",
            AutoSize = true,
            Location = new Point(16, 16),
        };
        var input = new WF.TextBox
        {
            Location = new Point(16, 48),
            Width = 340,
            PlaceholderText = "Type here — proves input is being pumped",
        };
        var ok = new WF.Button { Text = "OK", DialogResult = WF.DialogResult.OK, Location = new Point(16, 96) };
        var cancel = new WF.Button { Text = "Cancel", DialogResult = WF.DialogResult.Cancel, Location = new Point(104, 96) };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new WF.Control[] { label, input, ok, cancel });
    }
}
