using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Interop;

namespace WinFormsInterop;

/// <summary>
/// A minimal Majorsilence.Forms (Avalonia-backed) window that opens a real System.Windows.Forms form
/// both modeless and modal, to validate that the legacy form paints and takes input under Majorsilence's pump.
/// </summary>
public sealed class SpikeForm : Form
{
    public SpikeForm()
    {
        Text = "Majorsilence shell";
        ClientSize = new Size(420, 200);

        var modeless = new Button
        {
            Text = "Open legacy WinForms (modeless)",
            Location = new Point(20, 30),
            Size = new Size(370, 40),
        };
        // Lambdas (not method groups / new EventHandler(...)) so this compiles whether or not the
        // Click-event-type fix (gaps doc #2) has been applied upstream yet.
        modeless.Click += (_, _) => WindowsFormsInterop.Show(new SampleLegacyForm("Modeless"), owner: this);

        var modal = new Button
        {
            Text = "Open legacy WinForms (modal)",
            Location = new Point(20, 90),
            Size = new Size(370, 40),
        };
        modal.Click += (_, _) =>
        {
            var result = WindowsFormsInterop.ShowDialog(new SampleLegacyForm("Modal"), owner: this);
            Text = $"Majorsilence shell — dialog returned: {result}";
        };

        Controls.Add(modeless);
        Controls.Add(modal);
    }
}
