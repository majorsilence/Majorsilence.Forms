using Microsoft.UI.Xaml;
using Continuum.Forms;
using Continuum.Forms.Uno;
using Uno.UI.Hosting;

namespace Gallery.Uno;

// A Uno Platform (Skia desktop) head that hosts Continuum.Forms on the Uno backend.
// Run on a desktop session: `dotnet run --project samples/Gallery.Uno`.
public static class Program
{
    [System.STAThread]
    public static void Main ()
    {
        var host = UnoPlatformHostBuilder.Create ()
            .App (() => new ContinuumFormsUnoApp ())
            .UseX11 ()
            .UseWin32 ()
            .UseMacOS ()
            .Build ();

        host.Run ();
    }
}

// The Uno application: once launched, install the Uno backend and show a Continuum.Forms window.
public sealed class ContinuumFormsUnoApp : Microsoft.UI.Xaml.Application
{
    protected override void OnLaunched (LaunchActivatedEventArgs args)
    {
        System.Console.WriteLine ("[uno-head] OnLaunched — installing UnoPlatformBackend");
        Continuum.Forms.Backends.Platform.Backend = new UnoPlatformBackend ();

        // Diagnostics: CF_OPEN_MENU=1 opens a context menu programmatically so the Uno popup
        // chrome/position can be traced (set CF_UNO_TRACE=1) without manual clicking.
        if (Environment.GetEnvironmentVariable ("CF_OPEN_MENU") is not null) {
            var f = new Form { Text = "menu-trace" };
            var label = new Label { Text = "host", Left = 40, Top = 40, Width = 200, Height = 30 };
            f.Controls.Add (label);
            var menu = new ContextMenu ();
            menu.Items.Add ("Copy");
            menu.Items.Add ("Cut");
            menu.Items.Add ("Paste");
            var timer = new Continuum.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) => {
                timer.Stop ();
                // Use the real coordinate path (Control.PointToScreen) like a right-click context menu.
                var screen = label.PointToScreen (new System.Drawing.Point (0, label.Height));
                System.Console.Error.WriteLine ($"[uno-head] opening context menu at screen=({screen.X},{screen.Y})");
                menu.Show (label, screen);
            };
            f.Show ();
            timer.Start ();
            return;
        }

        var form = new ControlGallery.MainForm ();
        System.Console.WriteLine ($"[uno-head] ControlGallery.MainForm created on backend={Continuum.Forms.Backends.Platform.Backend.Name}; showing");
        form.Show ();
        System.Console.WriteLine ("[uno-head] form.Show() returned — the ControlGallery is live on Uno");
    }
}

// A small Continuum.Forms form proving render + input on the Uno backend.
public sealed class DemoForm : Form
{
    public DemoForm ()
    {
        Text = "Continuum.Forms on Uno";

        var label = new Label { Text = "Hello from Continuum.Forms on the Uno backend!", Left = 20, Top = 20, Width = 360, Height = 28 };
        var textbox = new TextBox { Left = 20, Top = 60, Width = 360, Height = 30 };
        var button = new Button { Text = "Click me", Left = 20, Top = 100, Width = 120, Height = 36 };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; label.Text = $"Clicked {clicks}x — text: \"{textbox.Text}\""; };

        Controls.Add (label);
        Controls.Add (textbox);
        Controls.Add (button);
    }
}
