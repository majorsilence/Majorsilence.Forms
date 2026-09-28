using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    // Inside the namespace, so Avalonia types win over the Majorsilence.Forms ones of the same name.
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;

    // Applying a stylesheet to a headless Avalonia application: the Fluent resources and styles it
    // installs, the names it publishes, the diagnostics, and what a re-apply takes back.
    public class AvaloniaCssThemeTests : IAsyncDisposable
    {
        private const string Sheet = @"
            @theme ""Brand"" extends Light;
            :root {
                --brand: #00578E;
                --line2: #C9D5E0;
                --radius: 8px;
                --accent-color: var(--brand);
                --background-color: #FFFFFF;
                --foreground-color: #13212D;
                --ui-font: ""Segoe UI"", sans-serif;
                --font-size: 13px;
            }
            Button {
                background-color: #FFFFFF;
                border: 1px solid var(--line2);
                border-radius: var(--radius);
                font-weight: bold;
            }
            Button:hover { background-color: var(--brand); color: white; }
            Label { color: #4C5D6C; font-weight: bold; }
            Menu { background-color: #FFFFFF; border-bottom-width: 1px; border-bottom-color: #DCE4EC; }
            TabStrip::selected { border-bottom-color: var(--brand); border-bottom-width: 3px; }
            DataGridView::selection { background-color: #E9EFF6; }
            ToolBar { background-color: #FFFFFF; }
            Form { border-color: red; }";

        public ValueTask DisposeAsync ()
        {
            GC.SuppressFinalize (this);
            return new ValueTask (Headless.Run (() => {
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
                AvaloniaCssTheme.Apply (MF.ThemeStyleSheet.Parse ("Button { }"));
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
            }));
        }

        private static object? Resource (string key)
            => Application.Current!.TryGetResource (key, ThemeVariant.Light, out var value) ? value : null;

        private static Color BrushColor (object? brush) => Assert.IsAssignableFrom<ISolidColorBrush> (brush).Color;

        [Fact]
        public Task Apply_WritesFluentResourcesForColours () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);

            Assert.Equal (Colors.White, BrushColor (Resource ("ButtonBackground")));
            Assert.Equal (Color.Parse ("#C9D5E0"), BrushColor (Resource ("ButtonBorderBrush")));
            Assert.Equal (Color.Parse ("#00578E"), BrushColor (Resource ("ButtonBackgroundPointerOver")));
            Assert.Equal (Colors.White, BrushColor (Resource ("ButtonForegroundPointerOver")));
            Assert.Equal (Color.Parse ("#E9EFF6"), BrushColor (Resource ("DataGridRowSelectedBackgroundBrush")));
            Assert.Equal (1.0, Resource ("DataGridRowSelectedBackgroundOpacity"));
            Assert.Equal (Color.Parse ("#00578E"), BrushColor (Resource ("TabItemHeaderSelectedPipeFill")));
            Assert.Equal (3.0, Resource ("TabItemPipeThickness"));
            Assert.Equal (Color.Parse ("#00578E"), Resource ("SystemAccentColor"));
            Assert.Equal (13.0, Resource ("ControlContentThemeFontSize"));
        });

        [Fact]
        public Task Apply_PressedFill_IsTheHoverFillSlightlyDarker () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);

            var hover = BrushColor (Resource ("ButtonBackgroundPointerOver"));
            var pressed = BrushColor (Resource ("ButtonBackgroundPressed"));
            Assert.True (pressed.R <= hover.R && pressed.G < hover.G && pressed.B < hover.B, $"{pressed} is not darker than {hover}");
        });

        [Fact]
        public Task Apply_PublishesTokensAndVariablesUnderPascalCaseKeys () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);

            Assert.Equal (Color.Parse ("#C9D5E0"), BrushColor (Resource ("Line2")));
            Assert.Equal (Color.Parse ("#C9D5E0"), Resource ("Line2Color"));
            Assert.Equal (Color.Parse ("#00578E"), BrushColor (Resource ("Brand")));
            Assert.Equal (8.0, Resource ("Radius"));
            Assert.Equal (Color.Parse ("#00578E"), Resource ("AccentColor"));
            Assert.Equal (Color.Parse ("#00578E"), BrushColor (Resource ("AccentBrush")));
            Assert.Equal (Color.Parse ("#13212D"), Resource ("ForegroundColor"));
        });

        // An app that keeps a hand-written copy of the tokens in its own merged dictionary (as a fallback)
        // still gets the stylesheet's values: the applier's dictionary is merged after it.
        [Fact]
        public Task PublishedResources_WinOverTheAppsOwnMergedDictionaries () => Headless.Run (() => {
            var appTokens = new ResourceDictionary { ["Line2"] = new SolidColorBrush (Colors.Red) };
            var themed = new ResourceDictionary ();
            themed.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary { ["Line2"] = new SolidColorBrush (Colors.Green) };
            Application.Current!.Resources.MergedDictionaries.Add (appTokens);
            Application.Current!.Resources.MergedDictionaries.Add (themed);

            try {
                AvaloniaCssTheme.Apply (Sheet);
                Assert.Equal (Color.Parse ("#C9D5E0"), BrushColor (Resource ("Line2")));
            } finally {
                Application.Current!.Resources.MergedDictionaries.Remove (appTokens);
                Application.Current!.Resources.MergedDictionaries.Remove (themed);
            }
        });

        [Fact]
        public Task Apply_StylesRealControls () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);

            var button = new Button { Content = "OK" };
            var label = new Label { Content = "Name" };
            var menu = new Menu ();
            var window = new Window { Content = new StackPanel { Children = { menu, label, button } } };
            window.Show ();

            try {
                Assert.Equal (Colors.White, BrushColor (button.Background));
                Assert.Equal (new CornerRadius (8), button.CornerRadius);
                Assert.Equal (new Thickness (1), button.BorderThickness);
                Assert.Equal (FontWeight.Bold, button.FontWeight);
                Assert.Equal (Color.Parse ("#4C5D6C"), BrushColor (label.Foreground));
                Assert.Equal (FontWeight.Bold, label.FontWeight);
                Assert.Equal (new Thickness (0, 0, 0, 1), menu.BorderThickness);
                Assert.Equal (Color.Parse ("#DCE4EC"), BrushColor (menu.BorderBrush));
                Assert.Equal (Colors.White, BrushColor (window.Background));
            } finally {
                window.Close ();
            }
        });

        [Fact]
        public Task Apply_ReportsWhatAvaloniaCannotExpress () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);

            var diagnostics = AvaloniaCssTheme.Diagnostics;
            Assert.Contains (diagnostics, d => d.Severity == MF.ThemeCssSeverity.Info && d.Message.Contains ("'ToolBar' has no Avalonia counterpart"));
            Assert.Contains (diagnostics, d => d.Severity == MF.ThemeCssSeverity.Warning && d.Message.Contains ("'Form { border-color }' is not supported on Avalonia"));
            Assert.Contains (diagnostics, d => d.Severity == MF.ThemeCssSeverity.Info && d.Message.Contains ("'Menu { border-bottom-color }' applies approximately"));
            Assert.DoesNotContain (diagnostics, d => d.Message.Contains ("'Button {"));
        });

        [Fact]
        public Task ReApply_TakesBackWhatTheNewSheetNoLongerSays () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);
            AvaloniaCssTheme.Apply (":root { --accent-color: #112233; } Label { color: #010203; }");

            // Button rules are gone, so Fluent's own ButtonBackground is back (not the sheet's white).
            Assert.NotEqual (Colors.White, BrushColor (Resource ("ButtonBackground")));
            Assert.Null (Resource ("Line2"));
            Assert.Equal (Color.Parse ("#112233"), Resource ("SystemAccentColor"));
            Assert.Single (Application.Current!.Resources.MergedDictionaries, d => d is ResourceDictionary rd && rd.ContainsKey ("SystemAccentColor"));
        });

        // A Styles object cleared and refilled while attached is not applied to controls created
        // afterwards, so each apply swaps in a new one; this pins that.
        [Fact]
        public Task ReApply_StylesControlsCreatedAfterwards () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { border-radius: 2px; }");
            AvaloniaCssTheme.Apply ("Button { border-radius: 9px; }");

            var button = new Button ();
            var window = new Window { Content = button };
            window.Show ();

            try {
                Assert.Equal (new CornerRadius (9), button.CornerRadius);
                Assert.Single (Application.Current!.Styles, s => s is Styles styles && styles.OfType<Style> ().Any (x => x.Selector?.ToString () == "Button"));
            } finally {
                window.Close ();
            }
        });

        [Fact]
        public Task Apply_SetsTheThemeVariantFromTheBackground () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("@theme \"Night\" extends Dark;");
            Assert.Equal (ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);

            AvaloniaCssTheme.Apply (Sheet);
            Assert.Equal (ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        });

        [Fact]
        public Task Apply_ThrowsOnErrors_AndInstallsNothing () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);
            var ex = Assert.Throws<MF.ThemeCssException> (() => AvaloniaCssTheme.Apply ("Button { border-bottom: 1px solid red; }"));
            Assert.NotEmpty (ex.Diagnostics);
            Assert.Equal (Colors.White, BrushColor (Resource ("ButtonBackground")));
        });

        [Fact]
        public Task SheetsAppliedFromTheMajorsilenceSide_AreMirrored () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (Sheet);   // subscribes

            MF.Theme.LoadFromCss ("Button { background-color: #123456; }");
            Assert.Equal (Color.Parse ("#123456"), BrushColor (Resource ("ButtonBackground")));
        });

        [Fact]
        public async Task Watch_AppliesTheFileNow ()
        {
            var path = Path.Combine (Path.GetTempPath (), $"avalonia-theme-{Guid.NewGuid ():N}.css");
            File.WriteAllText (path, "Button { background-color: #0A0B0C; }");

            try {
                await Headless.Run (() => {
                    using var watcher = AvaloniaCssTheme.Watch (path);
                    Assert.Equal (1, watcher.LoadCount);
                    Assert.Equal (Color.Parse ("#0A0B0C"), BrushColor (Resource ("ButtonBackground")));
                });
            } finally {
                File.Delete (path);
            }
        }

        [Theory]
        [InlineData ("--line2", "Line2")]
        [InlineData ("--ts-blue-mid", "TsBlueMid")]
        [InlineData ("--accent-color", "AccentColor")]
        [InlineData ("--ui_font", "UiFont")]
        public void ResourceKeyFor_IsPascalCase (string css, string key)
            => Assert.Equal (key, AvaloniaThemeApplier.ResourceKeyFor (css));

        [Fact]
        public void ToFontFamily_MapsCssGenericNamesToTheDefaultFamily ()
        {
            var family = AvaloniaThemeApplier.ToFontFamily (new[] { "Segoe UI", "system-ui", "sans-serif" });
            Assert.Equal (new[] { "Segoe UI", FontFamily.DefaultFontFamilyName }, family.FamilyNames);
        }
    }
}
