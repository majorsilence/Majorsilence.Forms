using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Interop;

namespace WinFormsInterop;

/// <summary>
/// Direction A sample: a Majorsilence.Forms (Avalonia-backed) window that opens real
/// <see cref="System.Windows.Forms.Form"/> forms modeless and modally.
///
/// This form is also used as the MF window opened by <see cref="WinFormsShellForm"/>
/// (Direction B), demonstrating the full bi-directional round-trip.
/// </summary>
public sealed class SpikeForm : Form
{
    public SpikeForm()
    {
        Text = "Majorsilence.Forms window  —  Direction A: MF → WinForms";
        ClientSize = new Size(480, 290);

        var info = new Label
        {
            Text = "This is a Majorsilence.Forms (Avalonia-backed) window.\n" +
                   "The buttons below open legacy System.Windows.Forms forms.",
            Location = new Point(20, 16),
            Size = new Size(440, 38),
        };

        var modeless = new Button
        {
            Text = "Open legacy WinForms form  (modeless)",
            Location = new Point(20, 68),
            Size = new Size(440, 40),
        };
        modeless.Click += (_, _) =>
            WindowsFormsInterop.Show(new SampleLegacyForm("Modeless"), owner: this);

        var modal = new Button
        {
            Text = "Open legacy WinForms form  (modal — blocks until closed)",
            Location = new Point(20, 122),
            Size = new Size(440, 40),
        };
        modal.Click += (_, _) =>
        {
            var result = WindowsFormsInterop.ShowDialog(new SampleLegacyForm("Modal"), owner: this);
            Text = $"Majorsilence window  —  WinForms dialog returned: {result}";
        };

        var ok = new Button
        {
            Text = "Close (OK)",
            Location = new Point(20, 220),
            Size = new Size(208, 40),
        };
        ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };

        var cancel = new Button
        {
            Text = "Close (Cancel)",
            Location = new Point(252, 220),
            Size = new Size(208, 40),
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(info);
        Controls.Add(modeless);
        Controls.Add(modal);
        Controls.Add(ok);
        Controls.Add(cancel);
    }
}
