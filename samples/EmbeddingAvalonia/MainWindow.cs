using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

using CF = Majorsilence.Forms;

namespace EmbeddingAvalonia;

// A native Avalonia Window whose content is a mix of native Avalonia controls and an embedded
// Majorsilence.Forms scene (hosted by MajorsilenceFormsPresenter). A "Toggle theme" button flips the host's
// theme variant; the embedded Majorsilence content recolors automatically via the theme bridge.
public sealed class MainWindow : Window
{
    public MainWindow ()
    {
        Title = "Majorsilence.Forms embedded in Avalonia";
        Width = 820;
        Height = 560;

        var heading = new TextBlock {
            Text = "Native Avalonia controls",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness (12, 12, 12, 4)
        };

        var nativeRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness (12, 0, 12, 8)
        };
        var nativeBox = new TextBox { Watermark = "A native Avalonia TextBox", Width = 260 };
        var themeButton = new Button { Content = "Toggle host theme" };
        themeButton.Click += (_, _) => {
            Application.Current!.RequestedThemeVariant =
                Application.Current.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        };
        nativeRow.Children.Add (nativeBox);
        nativeRow.Children.Add (themeButton);

        var divider = new Border {
            Height = 1,
            Background = Brushes.Gray,
            Margin = new Thickness (12, 4)
        };

        var subheading = new TextBlock {
            Text = "Embedded Majorsilence.Forms (MajorsilenceFormsPresenter)",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness (12, 4)
        };

        var presenter = new CF.MajorsilenceFormsPresenter {
            Content = BuildMajorsilenceScene (),
            Margin = new Thickness (12, 4, 12, 12)
        };

        var grid = new Grid {
            RowDefinitions = new RowDefinitions ("Auto,Auto,Auto,Auto,*")
        };
        Grid.SetRow (heading, 0);
        Grid.SetRow (nativeRow, 1);
        Grid.SetRow (divider, 2);
        Grid.SetRow (subheading, 3);
        Grid.SetRow (presenter, 4);
        grid.Children.Add (heading);
        grid.Children.Add (nativeRow);
        grid.Children.Add (divider);
        grid.Children.Add (subheading);
        grid.Children.Add (presenter);

        Content = grid;
    }

    // Builds a small Majorsilence.Forms control tree exercising render + input + popups.
    private static CF.Control BuildMajorsilenceScene ()
    {
        var panel = new CF.Panel ();

        var label = new CF.Label {
            Text = "Majorsilence.Forms controls (theme follows the host):",
            Left = 12, Top = 12, Width = 420, Height = 24
        };

        var textbox = new CF.TextBox {
            Text = "Edit me",
            Left = 12, Top = 44, Width = 240, Height = 30
        };

        var combo = new CF.ComboBox {
            Left = 12, Top = 84, Width = 240, Height = 30
        };
        combo.Items.Add ("First");
        combo.Items.Add ("Second");
        combo.Items.Add ("Third");

        var button = new CF.Button {
            Text = "Majorsilence Button",
            Left = 12, Top = 124, Width = 160, Height = 34
        };

        var status = new CF.Label {
            Text = "Clicks: 0",
            Left = 12, Top = 168, Width = 300, Height = 24
        };

        var clicks = 0;
        button.Click += (_, _) => { clicks++; status.Text = $"Clicks: {clicks} — text: \"{textbox.Text}\""; };

        // Phase 5: a real native Avalonia control hosted *inside* the Majorsilence scene (airspace overlay).
        var nativeButton = new Button { Content = "Native Avalonia button" };
        var nativeHost = new CF.NativeControlHost {
            Left = 280, Top = 44, Width = 240, Height = 40,
            NativeControl = nativeButton
        };
        var nativeCheck = new CheckBox { Content = "Native Avalonia checkbox" };
        var nativeCheckHost = new CF.NativeControlHost {
            Left = 280, Top = 92, Width = 240, Height = 32,
            NativeControl = nativeCheck
        };
        nativeButton.Click += (_, _) => status.Text = "Native Avalonia button clicked!";

        panel.Controls.Add (label);
        panel.Controls.Add (textbox);
        panel.Controls.Add (combo);
        panel.Controls.Add (button);
        panel.Controls.Add (status);
        panel.Controls.Add (nativeHost);
        panel.Controls.Add (nativeCheckHost);

        return panel;
    }
}
