using Microsoft.UI.Xaml;
using Modern.Forms;
using Modern.Forms.Uno;
using Uno.UI.Hosting;

namespace Gallery.Uno;

// A Uno Platform (Skia desktop) head that hosts Modern.Forms on the Uno backend.
// Run on a desktop session: `dotnet run --project samples/Gallery.Uno`.
public static class Program
{
    [System.STAThread]
    public static void Main ()
    {
        var host = UnoPlatformHostBuilder.Create ()
            .App (() => new ModernFormsUnoApp ())
            .UseX11 ()
            .UseWin32 ()
            .UseMacOS ()
            .Build ();

        host.Run ();
    }
}

// The Uno application: once launched, install the Uno backend and show a Modern.Forms window.
public sealed class ModernFormsUnoApp : Microsoft.UI.Xaml.Application
{
    protected override void OnLaunched (LaunchActivatedEventArgs args)
    {
        System.Console.WriteLine ("[uno-head] OnLaunched — installing UnoPlatformBackend");
        Modern.Forms.Backends.Platform.Backend = new UnoPlatformBackend ();

        var form = new ControlGallery.MainForm ();
        System.Console.WriteLine ($"[uno-head] ControlGallery.MainForm created on backend={Modern.Forms.Backends.Platform.Backend.Name}; showing");
        form.Show ();
        System.Console.WriteLine ("[uno-head] form.Show() returned — the ControlGallery is live on Uno");
    }
}

// A small Modern.Forms form proving render + input on the Uno backend.
public sealed class DemoForm : Form
{
    public DemoForm ()
    {
        Text = "Modern.Forms on Uno";

        var label = new Label { Text = "Hello from Modern.Forms on the Uno backend!", Left = 20, Top = 20, Width = 360, Height = 28 };
        var textbox = new TextBox { Left = 20, Top = 60, Width = 360, Height = 30 };
        var button = new Button { Text = "Click me", Left = 20, Top = 100, Width = 120, Height = 36 };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; label.Text = $"Clicked {clicks}x — text: \"{textbox.Text}\""; };

        Controls.Add (label);
        Controls.Add (textbox);
        Controls.Add (button);
    }
}
