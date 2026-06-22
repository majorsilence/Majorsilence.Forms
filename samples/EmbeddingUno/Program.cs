using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Majorsilence.Forms.Uno;
using Uno.UI.Hosting;

using CF = Majorsilence.Forms;
using MuxButton = Microsoft.UI.Xaml.Controls.Button;
using MuxTextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace EmbeddingUno;

// A host-owned Uno Platform (Skia desktop) head that embeds Majorsilence.Forms content via
// MajorsilenceFormsPresenter. Run on a desktop session: `dotnet run --project samples/EmbeddingUno`.
public static class Program
{
    [System.STAThread]
    public static void Main ()
    {
        var host = UnoPlatformHostBuilder.Create ()
            .App (() => new EmbeddingApp ())
            .UseX11 ()
            .UseWin32 ()
            .UseMacOS ()
            .Build ();

        host.Run ();
    }
}

public sealed class EmbeddingApp : Application
{
    private Window? _window;

    public EmbeddingApp ()
    {
        // Native WinUI controls (TextBox/Button/ComboBox) need their default styles; without this the
        // native TextBox/Button render unstyled (often invisible). Majorsilence.Forms draws its own controls,
        // so this is only required because the host app uses native controls alongside the presenter.
        Resources.MergedDictionaries.Add (new Microsoft.UI.Xaml.Controls.XamlControlsResources ());
    }

    protected override void OnLaunched (LaunchActivatedEventArgs args)
    {
        _window = new Window ();

        var root = new Grid ();
        root.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Star) });

        // ── Native Uno controls ──
        var nativeRow = new StackPanel {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
            Spacing = 8,
            Padding = new Thickness (12)
        };
        nativeRow.Children.Add (new MuxTextBox { PlaceholderText = "A native Uno TextBox", Width = 240 });

        var themeButton = new MuxButton { Content = "Toggle host theme" };
        themeButton.Click += (_, _) => {
            if (root.RequestedTheme == ElementTheme.Dark)
                root.RequestedTheme = ElementTheme.Light;
            else
                root.RequestedTheme = ElementTheme.Dark;
        };
        nativeRow.Children.Add (themeButton);
        Grid.SetRow (nativeRow, 0);
        root.Children.Add (nativeRow);

        var subheading = new TextBlock {
            Text = "Embedded Majorsilence.Forms (MajorsilenceFormsPresenter)",
            Margin = new Thickness (12, 0, 12, 8),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        Grid.SetRow (subheading, 1);
        root.Children.Add (subheading);

        // ── Embedded Majorsilence.Forms ──
        var presenter = new MajorsilenceFormsPresenter {
            Content = BuildMajorsilenceScene (),
            Margin = new Thickness (12, 0, 12, 12)
        };
        Grid.SetRow (presenter, 2);
        root.Children.Add (presenter);

        _window.Content = root;
        _window.Activate ();

        System.Console.WriteLine ($"[uno-embed] window shown; backend={CF.Backends.Platform.Backend.Name}");
    }

    private static CF.Control BuildMajorsilenceScene ()
    {
        var panel = new CF.Panel ();

        var label = new CF.Label {
            Text = "Majorsilence.Forms controls (theme follows the host):",
            Left = 12, Top = 12, Width = 420, Height = 24
        };
        var textbox = new CF.TextBox { Text = "Edit me", Left = 12, Top = 44, Width = 240, Height = 30 };
        var combo = new CF.ComboBox { Left = 12, Top = 84, Width = 240, Height = 30 };
        combo.Items.Add ("First");
        combo.Items.Add ("Second");
        combo.Items.Add ("Third");
        var button = new CF.Button { Text = "Majorsilence Button", Left = 12, Top = 124, Width = 160, Height = 34 };
        var status = new CF.Label { Text = "Clicks: 0", Left = 12, Top = 168, Width = 300, Height = 24 };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; status.Text = $"Clicks: {clicks} — text: \"{textbox.Text}\""; };

        // Phase 5: a real native Uno control hosted *inside* the Majorsilence scene (airspace overlay).
        var nativeButton = new MuxButton { Content = "Native Uno button" };
        nativeButton.Click += (_, _) => status.Text = "Native Uno button clicked!";
        var nativeHost = new CF.NativeControlHost {
            Left = 280, Top = 44, Width = 220, Height = 40,
            NativeControl = nativeButton
        };

        panel.Controls.Add (label);
        panel.Controls.Add (textbox);
        panel.Controls.Add (combo);
        panel.Controls.Add (button);
        panel.Controls.Add (status);
        panel.Controls.Add (nativeHost);

        return panel;
    }
}
