using Majorsilence.Forms;
using Majorsilence.Forms.Gtk4;

namespace Gallery.Gtk4;

// A GTK 4 (gir.core) head that hosts Majorsilence.Forms on the Gtk4 backend.
// Run on a desktop session (X11 or Wayland): `dotnet run --project samples/Gallery.Gtk4`.
// Set MF_GTK4_DEMO=1 for a tiny render+input smoke form instead of the full ControlGallery.
public static class Program
{
    [System.STAThread]
    public static void Main ()
    {
        Gtk4Application.Use ();
        System.Console.WriteLine ($"[gtk4-head] backend = {Majorsilence.Forms.Backends.Platform.Backend.Name}");

        Form form = System.Environment.GetEnvironmentVariable ("MF_GTK4_DEMO") == "1"
            ? new DemoForm ()
            : new ControlGallery.MainForm ();

        form.Show ();
        System.Console.WriteLine ("[gtk4-head] form.Show () returned — running the GLib main loop");

        Application.Run (form);
    }
}

// A small Majorsilence.Forms form proving render + input on the GTK 4 backend.
public sealed class DemoForm : Form
{
    public DemoForm ()
    {
        Text = "Majorsilence.Forms on GTK 4";
        ClientSize = new System.Drawing.Size (420, 200);

        var label = new Label { Text = "Hello from Majorsilence.Forms on the GTK 4 backend!", Left = 20, Top = 20, Width = 380, Height = 28 };
        var textbox = new TextBox { Left = 20, Top = 60, Width = 380, Height = 30 };
        var button = new Button { Text = "Click me", Left = 20, Top = 110, Width = 120, Height = 36 };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; label.Text = $"Clicked {clicks}x — text: \"{textbox.Text}\""; };

        Controls.Add (label);
        Controls.Add (textbox);
        Controls.Add (button);

        if (System.Environment.GetEnvironmentVariable ("MF_GTK4_SELFTEST") == "1") {
            var paints = 0;
            Paint += (_, _) => System.Console.Error.WriteLine ($"[selftest] paint #{++paints}");
            var t = new Majorsilence.Forms.Timer { Interval = 500 };
            var ticks = 0;
            t.Tick += (_, _) => {
                ticks++;
                System.Console.Error.WriteLine ($"[selftest] tick {ticks} clientSize={ClientSize} scaling={Scaling}");
                button.PerformClick ();
                if (ticks >= 4) { t.Stop (); Application.Exit (); }
            };
            t.Start ();
        }
    }
}
