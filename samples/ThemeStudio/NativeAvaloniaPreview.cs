using Majorsilence.Forms;
using Av = Avalonia.Controls;

namespace ThemeStudio
{
    // The "one stylesheet, two hosts" half of the preview (#104): one native Avalonia control of each type
    // the Avalonia support matrix maps, hosted in the Studio's own window through NativeControlHost. The
    // Studio applies its sheet through AvaloniaCssTheme when an Avalonia application is running, so an
    // edit restyles these and the Majorsilence controls on the other tabs together.
    internal static class NativeAvaloniaPreview
    {
        // True when the Studio runs on the Avalonia backend, the only place native Avalonia controls exist.
        public static bool Available => Avalonia.Application.Current is not null;

        public static void Build (TabPage page)
        {
            if (!Available) {
                page.Controls.Add (new Label {
                    Dock = DockStyle.Fill,
                    Text = "Native Avalonia controls appear here when Theme Studio runs on the Avalonia backend. "
                         + "They are themed by the same stylesheet, through Majorsilence.Forms.Theming.Avalonia.",
                });
                return;
            }

            // Built the first time the tab is shown rather than at startup: adding the Fluent theme to the
            // running application is the one global effect here, so a session that never opens this tab
            // never pays it.
            var host = page.Controls.Add (new NativeControlHost { Dock = DockStyle.Fill });

            void Populate ()
            {
                if (host.NativeControl is not null || !page.Visible)
                    return;

                EnsureFluent ();
                host.NativeControl = BuildControls ();
            }

            page.VisibleChanged += (_, _) => Populate ();
            Populate ();
        }

        // The standalone backend's Avalonia application installs no control theme of its own -- it never
        // shows a native control -- and AvaloniaCssTheme writes Fluent's resources, so the Fluent theme is
        // added the first time this preview is shown.
        private static void EnsureFluent ()
        {
            var styles = Avalonia.Application.Current!.Styles;

            foreach (var style in styles)
                if (style is Avalonia.Themes.Fluent.FluentTheme)
                    return;

            styles.Insert (0, new Avalonia.Themes.Fluent.FluentTheme ());
        }

        private static Av.ScrollViewer BuildControls ()
        {
            var column = new Av.StackPanel { Spacing = 10, Margin = new Avalonia.Thickness (12) };

            column.Children.Add (new Av.TextBlock { Text = "Native Avalonia controls, themed by the same sheet" });

            var buttons = new Av.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add (new Av.Button { Content = "Button" });
            buttons.Children.Add (new Av.Button { Content = "Disabled", IsEnabled = false });
            buttons.Children.Add (new Av.CheckBox { Content = "CheckBox", IsChecked = true });
            buttons.Children.Add (new Av.RadioButton { Content = "RadioButton", IsChecked = true });
            buttons.Children.Add (new Av.HyperlinkButton { Content = "HyperlinkButton (LinkLabel)" });
            column.Children.Add (buttons);

            var inputs = new Av.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            inputs.Children.Add (new Av.TextBox { Text = "TextBox", Width = 180 });
            inputs.Children.Add (new Av.ComboBox { ItemsSource = new[] { "ComboBox", "Second", "Third" }, SelectedIndex = 0, Width = 160 });
            inputs.Children.Add (new Av.NumericUpDown { Value = 42, Width = 140 });
            column.Children.Add (inputs);

            column.Children.Add (new Av.Slider { Minimum = 0, Maximum = 100, Value = 40, Width = 300, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
            column.Children.Add (new Av.ProgressBar { Minimum = 0, Maximum = 100, Value = 60, Width = 300, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });

            var lists = new Av.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            lists.Children.Add (new Av.ListBox { ItemsSource = new[] { "ListBox item", "Selected", "Third" }, SelectedIndex = 1, Width = 180, Height = 110 });

            var tabs = new Av.TabControl { Width = 260, Height = 110 };
            tabs.Items.Add (new Av.TabItem { Header = "TabItem", Content = new Av.TextBlock { Text = "A tab page" } });
            tabs.Items.Add (new Av.TabItem { Header = "Second" });
            lists.Children.Add (tabs);
            column.Children.Add (lists);

            return new Av.ScrollViewer { Content = column };
        }
    }
}
