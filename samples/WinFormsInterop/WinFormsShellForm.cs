using System.Drawing;
using WF = System.Windows.Forms;
using Majorsilence.Forms.Interop;

namespace WinFormsInterop;

/// <summary>
/// Direction B sample: a real <see cref="System.Windows.Forms.Form"/> shell that opens
/// Majorsilence.Forms (Avalonia-backed) windows modeless and modally.
///
/// The MF windows it opens (<see cref="SpikeForm"/>) can themselves open legacy WinForms forms
/// via Direction A — demonstrating the full bi-directional round-trip.
/// </summary>
public sealed class WinFormsShellForm : WF.Form
{
    public WinFormsShellForm()
    {
        Text = "WinForms shell  —  Direction B: WF → Majorsilence.Forms";
        ClientSize = new Size(520, 250);
        StartPosition = WF.FormStartPosition.CenterScreen;

        var info = new WF.Label
        {
            Text = "This is a real System.Windows.Forms.Form.\n" +
                   "The buttons below open Majorsilence.Forms (Avalonia-backed) windows.\n" +
                   "Each MF window also has buttons to open WinForms forms (Direction A).",
            Location = new Point(16, 14),
            Size = new Size(488, 52),
            AutoSize = false,
        };

        var modeless = new WF.Button
        {
            Text = "Open Majorsilence.Forms window  (modeless)",
            Location = new Point(16, 80),
            Size = new Size(488, 40),
        };
        modeless.Click += (_, _) =>
            WindowsFormsInterop.ShowMajorsilenceForm(new SpikeForm(), owner: this);

        var modal = new WF.Button
        {
            Text = "Open Majorsilence.Forms window  (modal — blocks until closed)",
            Location = new Point(16, 136),
            Size = new Size(488, 40),
        };
        modal.Click += (_, _) =>
        {
            var result = WindowsFormsInterop.ShowMajorsilenceDialog(new SpikeForm(), owner: this);
            Text = $"WinForms shell  —  Majorsilence dialog returned: {result}";
        };

        var status = new WF.Label
        {
            Text = "Tip: close an MF window with OK or Cancel to see the DialogResult above.",
            Location = new Point(16, 196),
            Size = new Size(488, 20),
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = false,
        };

        Controls.AddRange(new WF.Control[] { info, modeless, modal, status });
    }
}
