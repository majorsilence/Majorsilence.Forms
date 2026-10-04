using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Terminal;

namespace Gallery.Terminal;

// Hosts a small Majorsilence.Forms form in the terminal. Mouse and keyboard work; Ctrl+C exits.
// Run in a truecolor terminal: `dotnet run --project samples/Gallery.Terminal`.
// The output mode is found by asking the terminal (Kitty graphics, Sixel, else half-blocks); MF_TERMINAL_GRAPHICS=halfblock|kitty|sixel forces one.
// MF_TERMINAL_SCALE=0.5 lays the form out on a canvas twice as large as the pixel grid: needed in half-block mode, where the grid is tiny.
public static class Program
{
    public static void Main ()
    {
        var options = new TerminalOptions ();
        if (double.TryParse (Environment.GetEnvironmentVariable ("MF_TERMINAL_SCALE"), out var scale) && scale > 0)
            options.Scaling = scale;

        TerminalApplication.Use (options);
        Application.Run (new DemoForm ());
    }
}

internal sealed class DemoForm : Form
{
    public DemoForm ()
    {
        Text = "Majorsilence.Forms in a terminal";

        var title = new Label { Text = "Hello from the terminal host", Location = new Point (8, 36), AutoSize = false, Width = 220 };
        var name = new TextBox { Text = "Type here", Location = new Point (8, 56), Width = 220 };
        var check = new CheckBox { Text = "A checkbox", Checked = true, Location = new Point (8, 82), AutoSize = true };
        var ok = new Button { Text = "OK", Location = new Point (8, 106), Width = 80 };
        var bar = new ProgressBar { Location = new Point (8, 136), Width = 220, Value = 65 };

        var clicks = 0;
        ok.Click += (_, _) => title.Text = $"Clicked {++clicks} time(s)";

        Controls.AddRange (new Control[] { title, name, check, ok, bar });
    }
}
