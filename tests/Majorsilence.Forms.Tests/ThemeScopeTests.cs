using System;
using System.Runtime.CompilerServices;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ThemeScope: a theme for one window or control subtree while the rest of the application keeps
    // Theme. Proved by paint where it matters; magenta appears in no built-in theme, so any is the scope's.
    [Collection ("Headless")]
    public class ThemeScopeTests : IDisposable
    {
        private static readonly SKColor Magenta = new SKColor (255, 0, 255);

        public ThemeScopeTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        [Fact]
        public void A_scoped_form_paints_with_its_scope_and_another_form_does_not ()
        {
            HeadlessRenderer.Use ();
            var scope = new ThemeScope ();
            scope.LoadFromCss ("Button { background-color: #ff00ff; }");

            using var scoped = ButtonForm (out var scopedButton);
            scoped.ThemeScope = scope;
            using var plain = ButtonForm (out var plainButton);

            Assert.True (Coverage (RenderBackBuffer (scopedButton, scoped), Magenta) > 0.25);
            Assert.Equal (0, Coverage (RenderBackBuffer (plainButton, plain), Magenta));
        }

        [Fact]
        public void A_scoped_panel_themes_only_its_subtree ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new System.Drawing.Size (400, 300) };
            var panel = new Panel { Dock = DockStyle.Left, Width = 200 };
            var inside = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add (inside);
            var outside = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (outside);
            form.Controls.Add (panel);

            panel.ThemeScope = new ThemeScope { BackgroundColor = Magenta };

            Assert.True (Coverage (RenderBackBuffer (inside, form), Magenta) > 0.9, "the scoped panel's child takes the scope's background");
            Assert.Equal (0, Coverage (RenderBackBuffer (outside, form), Magenta));
        }

        [Fact]
        public void Tokens_the_scope_does_not_set_follow_the_application_theme ()
        {
            var scope = new ThemeScope { AccentColor = Magenta };
            Theme.ForegroundColor = SKColors.Lime;

            using (scope.Enter ()) {
                Assert.Equal (Magenta, Theme.AccentColor);
                Assert.Equal (SKColors.Lime, Theme.ForegroundColor);
            }

            Theme.AccentColor = SKColors.Blue;

            using (scope.Enter ())
                Assert.Equal (Magenta, Theme.AccentColor);
            Assert.Equal (SKColors.Blue, Theme.AccentColor);
            Assert.Null (ThemeScope.Current);
        }

        [Fact]
        public void Type_level_styles_read_the_scope_and_follow_later_application_changes ()
        {
            var scope = new ThemeScope { AccentColor = Magenta };

            using (scope.Enter ()) {
                Assert.Equal (Magenta, Button.DefaultStyleHover.GetBackgroundColor ());
                Assert.Equal (Magenta, new Button ().StyleHover.GetBackgroundColor ());     // an instance style's chain
            }
            Assert.NotEqual (Magenta, Button.DefaultStyleHover.GetBackgroundColor ());

            Theme.ForegroundColorOnAccent = SKColors.Lime;                               // a token the scope does not set

            using (scope.Enter ())
                Assert.Equal (SKColors.Lime, Button.DefaultStyleHover.GetForegroundColor ());
        }

        [Fact]
        public void Scoped_borders_follow_the_scope ()
        {
            var scope = new ThemeScope { AccentColor2 = Magenta };

            using (scope.Enter ()) {
                Assert.Equal (Magenta, Button.DefaultStyleHover.Border.GetColor ());
                Assert.Equal (Magenta, new Button ().StyleHover.Border.Left.GetColor ());
            }
            Assert.NotEqual (Magenta, Button.DefaultStyleHover.Border.GetColor ());
        }

        [Fact]
        public void A_scoped_css_rule_leaves_the_static_style_alone ()
        {
            var scope = new ThemeScope ();
            scope.LoadFromCss ("Button { background-color: #ff00ff; border-radius: 6px; }");

            using (scope.Enter ()) {
                Assert.Equal (Magenta, Button.DefaultStyle.GetBackgroundColor ());
                Assert.Equal (6, Button.DefaultStyle.Border.GetRadius ());
            }
            Assert.NotEqual (Magenta, Button.DefaultStyle.GetBackgroundColor ());
            Assert.Equal (0, Button.DefaultStyle.Border.GetRadius ());
        }

        [Fact]
        public void Application_rules_apply_in_a_scope_until_it_has_a_theme_of_its_own ()
        {
            Theme.LoadFromCss ("Button { background-color: #ff00ff; }");
            var scope = new ThemeScope { AccentColor = SKColors.Lime };

            using (scope.Enter ())
                Assert.Equal (Magenta, Button.DefaultStyle.GetBackgroundColor ());

            scope.SetBuiltInTheme (BuiltInTheme.Dark);

            using (scope.Enter ())
                Assert.NotEqual (Magenta, Button.DefaultStyle.GetBackgroundColor ());
            Assert.Equal (Magenta, Button.DefaultStyle.GetBackgroundColor ());
        }

        [Fact]
        public void A_scope_extends_a_built_in_theme ()
        {
            var scope = new ThemeScope ();
            scope.LoadFromCss ("@theme \"S\" extends Dark; :root { --accent-color: #ff00ff; }");

            var dark = new ThemeScope ();
            dark.SetBuiltInTheme (BuiltInTheme.Dark);

            Assert.Equal (Magenta, scope.AccentColor);
            Assert.Equal (dark.BackgroundColor, scope.BackgroundColor);
            Assert.NotEqual (Theme.BackgroundColor, scope.BackgroundColor);
            Assert.Single (scope.CurrentStyleSheets);
        }

        [Fact]
        public void Scope_changes_leave_the_application_theme_alone ()
        {
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            Theme.ThemeChanged += handler;
            try {
                var scope = new ThemeScope ();
                scope.SetBuiltInTheme (BuiltInTheme.Dark);
                scope.LoadFromCss ("Button { background-color: #ff00ff; }");
                scope.AccentColor = Magenta;

                Assert.Equal (0, raised);
                Assert.Empty (Theme.CurrentStyleSheets);
            } finally {
                Theme.ThemeChanged -= handler;
            }
        }

        [Fact]
        public void Changed_is_raised_once_per_update ()
        {
            var scope = new ThemeScope ();
            var raised = 0;
            scope.Changed += (_, _) => raised++;

            scope.BeginUpdate ();
            scope.AccentColor = Magenta;
            scope.BackgroundColor = Magenta;
            scope.EndUpdate ();
            scope.Reset (nameof (Theme.AccentColor));

            Assert.Equal (2, raised);
            Assert.False (scope.IsSet (nameof (Theme.AccentColor)));
            Assert.True (scope.IsSet (nameof (Theme.BackgroundColor)));
        }

        [Fact]
        public void Dialogs_and_drop_downs_take_the_scope_of_what_opened_them ()
        {
            HeadlessRenderer.Use ();
            var scope = new ThemeScope ();

            using var owner = new Form ();
            owner.ThemeScope = scope;
            using var dialog = new Form { Owner = owner };
            Assert.Same (scope, dialog.ResolveThemeScope ());

            // a combo box in a scoped panel of an unscoped window
            using var form = new Form ();
            var panel = new Panel { Dock = DockStyle.Fill, ThemeScope = scope };
            var combo = new ComboBox ();
            combo.Items.AddRange (new object[] { "a", "b" });
            panel.Controls.Add (combo);
            form.Controls.Add (panel);
            form.Show ();

            combo.DroppedDown = true;
            var popup = (PopupWindow) typeof (ComboBox).GetField ("popup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue (combo)!;

            Assert.Same (scope, popup.ResolveThemeScope ());
            Assert.Null (form.ResolveThemeScope ());
        }

        [Fact]
        public void A_dialog_opened_from_a_scoped_control_takes_its_scope_when_its_owner_has_none ()
        {
            HeadlessRenderer.Use ();
            var scope = new ThemeScope ();

            // a scoped control inside an unscoped window -- an embedded designer
            using var host = new Form ();
            var panel = new Panel { Dock = DockStyle.Fill, ThemeScope = scope };
            var button = new Button ();
            panel.Controls.Add (button);
            host.Controls.Add (panel);
            host.Show ();

            Form? dialog = null;
            button.Click += (_, _) => dialog = new Form { Owner = host };
            button.RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, 1, 1, System.Drawing.Point.Empty));

            Assert.NotNull (dialog);
            using (dialog)
                Assert.Same (scope, dialog!.ResolveThemeScope ());
        }

        [Fact]
        public void An_owner_scope_wins_over_the_scope_a_window_was_created_under ()
        {
            var opener = new ThemeScope ();
            var owners = new ThemeScope ();
            using var owner = new Form { ThemeScope = owners };

            Form dialog;
            using (opener.Enter ())
                dialog = new Form { Owner = owner };

            using (dialog)
                Assert.Same (owners, dialog.ResolveThemeScope ());

            using var unrelated = new Form ();
            Assert.Null (unrelated.ResolveThemeScope ());
        }

        [Fact]
        public void A_scoped_font_lays_out_as_the_same_application_font_does ()
        {
            HeadlessRenderer.Use ();

            const string css = "Form { font-size: 40px; }";   // the ambient font every child control inherits

            var plain = LabelWidth (null);
            var scope = new ThemeScope ();
            scope.LoadFromCss (css);
            var scoped = LabelWidth (scope);

            Theme.LoadFromCss (css);
            var global = LabelWidth (null);

            Assert.True (global > plain, $"precondition: a 40px application font widens the label ({plain} -> {global})");
            Assert.Equal (global, scoped);
        }

        private static int LabelWidth (ThemeScope? scope)
        {
            using var form = new Form { Size = new System.Drawing.Size (800, 300), ThemeScope = scope };
            var label = new Label { AutoSize = true, Text = "The quick brown fox" };
            form.Controls.Add (label);
            form.Show ();
            form.PerformLayout ();
            return label.Width;
        }

        [Fact]
        public void A_dropped_scope_can_be_collected ()
        {
            var weak = AttachAndDrop ();

            for (var i = 0; i < 3; i++) {
                GC.Collect ();
                GC.WaitForPendingFinalizers ();
            }

            Assert.False (weak.TryGetTarget (out _));
        }

        [MethodImpl (MethodImplOptions.NoInlining)]
        private static WeakReference<ThemeScope> AttachAndDrop ()
        {
            var scope = new ThemeScope { AccentColor = Magenta };
            using (var form = new Form ()) {
                form.ThemeScope = scope;
                using (scope.Enter ())
                    _ = Button.DefaultStyleHover.GetBackgroundColor ();
                form.ThemeScope = null;
            }
            return new WeakReference<ThemeScope> (scope);
        }

        private static Form ButtonForm (out Button button)
        {
            var form = new Form { Size = new System.Drawing.Size (300, 200) };
            button = new Button { Dock = DockStyle.Fill, Text = "" };
            form.Controls.Add (button);
            return form;
        }

        private static SKBitmap RenderBackBuffer (Control control, Form form)
        {
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            return control.GetBackBuffer ();
        }

        private static double Coverage (SKBitmap bitmap, SKColor color)
        {
            var hits = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Red == color.Red && c.Green == color.Green && c.Blue == color.Blue)
                        hits++;
                }
            return (double) hits / (bitmap.Width * bitmap.Height);
        }
    }
}
