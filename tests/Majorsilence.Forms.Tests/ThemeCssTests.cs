using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Tests for CSS-defined themes (Theme.Css.cs / ThemeStyleSheet). Theme is global state, so each test
    // resets to the Light built-in (which also clears every stylesheet control rule) and unregisters
    // anything it registered.
    public class ThemeCssTests : IDisposable
    {
        public ThemeCssTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);

            foreach (var name in Theme.RegisteredThemes.ToList ())
                Theme.UnregisterTheme (name);

            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static ThemeStyleSheet ParseClean (string css)
        {
            var sheet = ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            return sheet;
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        // ---- header and structure -----------------------------------------------------------------

        [Fact]
        public void Header_ReadsNameAndBase ()
        {
            var sheet = ParseClean ("@theme \"Ocean\" extends Dark;");

            Assert.Equal ("Ocean", sheet.Name);
            Assert.Equal ("Dark", sheet.BaseName);
        }

        [Fact]
        public void Header_UnquotedNames_And_Comments ()
        {
            var sheet = ParseClean ("/* a comment */ @theme Ocean extends Dark; /* another */ :root { --accent-color: #123456; /* inline */ }");

            Assert.Equal ("Ocean", sheet.Name);
            Assert.Equal ("Dark", sheet.BaseName);
            Assert.Equal (1, sheet.TokenCount);
        }

        [Fact]
        public void Counts_TokensAndRules ()
        {
            var sheet = ParseClean (@"
                :root { --accent-color: red; --font-size: 15px; }
                Button, TextBox { color: white; }
                Button:hover { background-color: black; }");

            Assert.Equal (2, sheet.TokenCount);
            Assert.Equal (3, sheet.RuleCount);   // Button, TextBox, Button:hover
        }

        // ---- tokens -------------------------------------------------------------------------------

        [Fact]
        public void Tokens_SetThemeProperties ()
        {
            Theme.LoadFromCss (@"
                :root {
                    --accent-color: #1e90ff;
                    --accent-color-2: rgb(0, 105, 148);
                    --background-color: rgba(10, 25, 41, 0.5);
                    --foreground-color: white;
                    --border-low-color: transparent;
                    --warning-highlight-color: hsl(0, 100%, 50%);
                    --font-size: 17px;
                    --item-font-size: 11;
                }");

            Assert.Equal (new SKColor (0x1e, 0x90, 0xff), Theme.AccentColor);
            Assert.Equal (new SKColor (0, 105, 148), Theme.AccentColor2);
            Assert.Equal (new SKColor (10, 25, 41, 128), Theme.BackgroundColor);
            Assert.Equal (SKColors.White, Theme.ForegroundColor);
            Assert.Equal (SKColors.Transparent, Theme.BorderLowColor);
            Assert.Equal (new SKColor (255, 0, 0), Theme.WarningHighlightColor);
            Assert.Equal (17, Theme.FontSize);
            Assert.Equal (11, Theme.ItemFontSize);
        }

        [Fact]
        public void HexColor_ShortForms_And_AlphaLast ()
        {
            Theme.LoadFromCss (":root { --accent-color: #f00; --accent-color-2: #0f08; --background-color: #11223380; }");

            Assert.Equal (new SKColor (255, 0, 0), Theme.AccentColor);
            Assert.Equal (new SKColor (0, 255, 0, 0x88), Theme.AccentColor2);
            // CSS puts alpha LAST (#rrggbbaa); the XML themes use SkiaSharp's #AARRGGBB. Mixing them up
            // silently produces a near-invisible colour, so this is the one place the order is pinned.
            Assert.Equal (new SKColor (0x11, 0x22, 0x33, 0x80), Theme.BackgroundColor);
        }

        [Fact]
        public void Tokens_Fonts ()
        {
            Theme.LoadFromCss (":root { --ui-font: \"Courier New\", monospace; --ui-font-bold: \"Courier New\"; }");

            var expected = SKTypeface.FromFamilyName ("Courier New")?.FamilyName ?? string.Empty;
            Assert.Equal (expected, Theme.UIFont.FamilyName);
            Assert.True (Theme.UIFontBold.IsBold);
        }

        [Fact]
        public void Tokens_AppliedInOrder_LaterWins ()
        {
            Theme.LoadFromCss (":root { --accent-color: red; } :root { --accent-color: blue; }");

            Assert.Equal (new SKColor (0, 0, 255), Theme.AccentColor);
        }

        [Fact]
        public void Token_TypeMismatch_IsError ()
        {
            var errors = ErrorsOf (":root { --font-size: #fff; --accent-color: 14px; }");

            Assert.Contains ("'--font-size' expects a length", errors);
            Assert.Contains ("'--accent-color' expects a color", errors);
        }

        // ---- variables ----------------------------------------------------------------------------

        [Fact]
        public void Var_UserVariable_Substitutes ()
        {
            Theme.LoadFromCss (":root { --brand: #336699; --accent-color: var(--brand); --accent-color-2: var(--brand); }");

            Assert.Equal (new SKColor (0x33, 0x66, 0x99), Theme.AccentColor);
            Assert.Equal (new SKColor (0x33, 0x66, 0x99), Theme.AccentColor2);
        }

        [Fact]
        public void Var_Fallback_UsedWhenUndefined ()
        {
            Theme.LoadFromCss (":root { --accent-color: var(--nope, #010203); }");

            Assert.Equal (new SKColor (1, 2, 3), Theme.AccentColor);
        }

        [Fact]
        public void Var_TokenReference_InControlRule_TracksLaterThemeChanges ()
        {
            Theme.LoadFromCss ("TextBox { background-color: var(--accent-color); }");

            Assert.Equal (Theme.AccentColor, TextBox.DefaultStyle.BackgroundColor);

            // The rule is compiled against the token, not its value: a later change to the token is
            // picked up when the theme change re-runs the type defaults.
            Theme.AccentColor = new SKColor (9, 8, 7);

            Assert.Equal (new SKColor (9, 8, 7), TextBox.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void Var_Unknown_ErrorSuggestsToken ()
        {
            var errors = ErrorsOf (":root { --accent-color: var(--acent-color); }");

            Assert.Contains ("Unknown variable '--acent-color'", errors);
            Assert.Contains ("Did you mean '--accent-color'?", errors);
        }

        [Fact]
        public void Var_Cycle_IsError ()
        {
            var errors = ErrorsOf (":root { --a: var(--b); --b: var(--a); --accent-color: var(--a); }");

            Assert.Contains ("refers to itself", errors);
        }

        [Fact]
        public void UnusedVariable_WarnsWithSuggestion ()
        {
            var sheet = ThemeStyleSheet.Parse (":root { --acent-color: red; }");

            Assert.False (sheet.HasErrors);
            var warning = Assert.Single (sheet.Diagnostics);
            Assert.Equal (ThemeCssSeverity.Warning, warning.Severity);
            Assert.Contains ("Did you mean '--accent-color'?", warning.Message);
        }

        // ---- control rules ------------------------------------------------------------------------

        [Fact]
        public void ControlRule_SetsTypeDefaultStyle ()
        {
            Theme.LoadFromCss ("Button { background-color: #ff0000; color: #00ff00; border-color: #0000ff; border-width: 3px; border-radius: 6; }");

            Assert.Equal (new SKColor (255, 0, 0), Button.DefaultStyle.BackgroundColor);
            Assert.Equal (new SKColor (0, 255, 0), Button.DefaultStyle.ForegroundColor);
            Assert.Equal (new SKColor (0, 0, 255), Button.DefaultStyle.Border.Color);
            Assert.Equal (3, Button.DefaultStyle.Border.Width);
            Assert.Equal (6, Button.DefaultStyle.Border.Radius);

            // ...and an instance without its own values resolves through the type default.
            using var button = new Button ();
            Assert.Equal (new SKColor (255, 0, 0), button.Style.GetBackgroundColor ());
            Assert.Equal (3, button.Style.Border.GetWidth ());
        }

        [Fact]
        public void ControlRule_Hover ()
        {
            Theme.LoadFromCss ("Button:hover { background-color: #123456; }");

            Assert.Equal (new SKColor (0x12, 0x34, 0x56), Button.DefaultStyleHover.BackgroundColor);
            // The normal style is untouched.
            Assert.Null (Button.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void ControlRule_CommaList_AppliesToEach ()
        {
            Theme.LoadFromCss ("Button, TextBox, ListBox { color: #abcdef; }");

            Assert.Equal (new SKColor (0xab, 0xcd, 0xef), Button.DefaultStyle.ForegroundColor);
            Assert.Equal (new SKColor (0xab, 0xcd, 0xef), TextBox.DefaultStyle.ForegroundColor);
            Assert.Equal (new SKColor (0xab, 0xcd, 0xef), ListBox.DefaultStyle.ForegroundColor);
        }

        [Fact]
        public void ControlRule_SelectorsAreCaseInsensitive ()
        {
            Theme.LoadFromCss ("button { color: red; }");

            Assert.Equal (new SKColor (255, 0, 0), Button.DefaultStyle.ForegroundColor);
        }

        [Fact]
        public void ControlRule_PerSideBorder ()
        {
            Theme.LoadFromCss ("Panel { border-left-width: 2px; border-left-color: red; border-bottom-width: 1px; }");

            Assert.Equal (2, Panel.DefaultStyle.Border.Left.Width);
            Assert.Equal (new SKColor (255, 0, 0), Panel.DefaultStyle.Border.Left.Color);
            Assert.Equal (1, Panel.DefaultStyle.Border.Bottom.Width);
            Assert.Null (Panel.DefaultStyle.Border.Top.Width);
        }

        [Fact]
        public void Border_Shorthand ()
        {
            Theme.LoadFromCss ("TextBox { border: 2px solid #ff00ff; } Label { border: none; } ListBox { border: red 4px; }");

            Assert.Equal (2, TextBox.DefaultStyle.Border.Width);
            Assert.Equal (new SKColor (255, 0, 255), TextBox.DefaultStyle.Border.Color);
            Assert.Equal (0, Label.DefaultStyle.Border.Width);
            Assert.Equal (4, ListBox.DefaultStyle.Border.Width);
            Assert.Equal (new SKColor (255, 0, 0), ListBox.DefaultStyle.Border.Color);
        }

        [Fact]
        public void Font_Family_Weight_Style_Size ()
        {
            Theme.LoadFromCss ("TextBox { font-family: \"Courier New\", monospace; font-weight: bold; font-style: italic; font-size: 18px; }");

            var font = TextBox.DefaultStyle.Font;
            Assert.NotNull (font);
            Assert.True (font!.IsBold);
            Assert.True (font.IsItalic);
            Assert.Equal (SKTypeface.FromFamilyName ("Courier New")?.FamilyName, font.FamilyName);
            Assert.Equal (18, TextBox.DefaultStyle.FontSize);
        }

        [Fact]
        public void Font_WeightWithoutFamily_UsesDefaultFamilyBold ()
        {
            Theme.LoadFromCss ("Label { font-weight: bold; }");

            Assert.NotNull (Label.DefaultStyle.Font);
            Assert.True (Label.DefaultStyle.Font!.IsBold);
        }

        [Fact]
        public void PanelRule_ReachesDerivedPanels ()
        {
            Theme.LoadFromCss ("Panel { background-color: #010203; }");

            using var flow = new FlowLayoutPanel ();
            using var page = new TabPage ();

            Assert.Equal (new SKColor (1, 2, 3), flow.Style.GetBackgroundColor ());
            Assert.Equal (new SKColor (1, 2, 3), page.Style.GetBackgroundColor ());
        }

        [Fact]
        public void FormRule_Font_IsAmbientForNestedControls ()
        {
            // The documented way to change the app-wide text font is a Form rule: every control that
            // sets no font of its own inherits it through the parent chain, however deeply nested.
            Theme.LoadFromCss ("Form { font-family: \"Courier New\"; font-size: 19px; }");

            using var form = new Form ();
            var tabs = new TabControl ();
            var page = tabs.TabPages.Add ("Page");
            var label = new Label { Text = "Label" };
            var button = new Button { Text = "Button" };
            var box = new TextBox { Text = "Text" };
            var check = new CheckBox { Text = "Check" };
            page.Controls.Add (label);
            page.Controls.Add (button);
            page.Controls.Add (box);
            page.Controls.Add (check);
            form.Controls.Add (tabs);

            var effectiveFont = typeof (Control).GetMethod ("GetEffectiveFont", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var effectiveSize = typeof (Control).GetMethod ("GetEffectiveFontSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var expected = SKTypeface.FromFamilyName ("Courier New")?.FamilyName;

            Assert.Equal (expected, Form.DefaultStyle.Font?.FamilyName);

            // A Form is a WindowBase, not a Control: the parent walk ends at the window's adapter, and
            // the resolution has to ask the window's style explicitly (as the colour ones do).
            foreach (Control control in new Control[] { tabs, page, label, button, box, check }) {
                Assert.Equal (expected, ((SKTypeface) effectiveFont.Invoke (control, null)!).FamilyName);
                Assert.Equal (19, (int) effectiveSize.Invoke (control, null)!);
            }
        }

        // ---- lifecycle: replace / reset / survive theme changes ------------------------------------

        [Fact]
        public void ApplyingAnotherSheet_ReplacesControlRules ()
        {
            Theme.LoadFromCss ("Button { background-color: red; } TextBox { color: blue; }");
            Theme.LoadFromCss ("TextBox { color: green; }");

            // Button is no longer mentioned, so it returns to its declared default (no colour of its own).
            Assert.Null (Button.DefaultStyle.BackgroundColor);
            Assert.Equal (new SKColor (0, 128, 0), TextBox.DefaultStyle.ForegroundColor);
        }

        [Fact]
        public void ApplyingAnotherSheet_RestoresDeclaredDefaults ()
        {
            // ComboBox declares Border.Width = 1 and a ControlMid background in its defaults.
            Theme.LoadFromCss ("ComboBox { border-width: 5px; background-color: red; }");
            Assert.Equal (5, ComboBox.DefaultStyle.Border.Width);

            Theme.LoadFromCss ("Label { color: red; }");

            Assert.Equal (1, ComboBox.DefaultStyle.Border.Width);
            Assert.Equal (Theme.ControlMidColor, ComboBox.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void SetBuiltInTheme_ClearsControlRules ()
        {
            Theme.LoadFromCss ("Button { background-color: red; } Button:hover { border-radius: 9px; }");
            Assert.Equal (new SKColor (255, 0, 0), Button.DefaultStyle.BackgroundColor);

            Theme.SetBuiltInTheme (BuiltInTheme.Dark);

            Assert.Null (Button.DefaultStyle.BackgroundColor);
            Assert.Null (Button.DefaultStyleHover.Border.Radius);
            // The hover default still re-derives from the (now Dark) theme.
            Assert.Equal (Theme.AccentColor, Button.DefaultStyleHover.BackgroundColor);
        }

        [Fact]
        public void ControlRule_SurvivesLaterThemePropertyChange ()
        {
            // Button's hover default sets BackgroundColor = Theme.AccentColor on every theme change; the
            // stylesheet rule has to keep winning after that re-run, or the theme "forgets" the CSS.
            Theme.LoadFromCss ("Button:hover { background-color: #0a0b0c; }");

            Theme.AccentColor = new SKColor (1, 1, 1);

            Assert.Equal (new SKColor (0x0a, 0x0b, 0x0c), Button.DefaultStyleHover.BackgroundColor);
        }

        [Fact]
        public void XmlTheme_WithoutBase_LeavesCssControlRulesInPlace ()
        {
            Theme.LoadFromCss ("Button { background-color: red; }");
            Theme.LoadFromXml ("<Theme><AccentColor>1,2,3</AccentColor></Theme>");

            Assert.Equal (new SKColor (255, 0, 0), Button.DefaultStyle.BackgroundColor);
            Assert.Equal (new SKColor (1, 2, 3), Theme.AccentColor);
        }

        // ---- registration and inheritance ---------------------------------------------------------

        [Fact]
        public void RegisterThemeCss_ReturnsName_AndApplyTheme_Works ()
        {
            var name = Theme.RegisterThemeCss ("@theme \"Ocean\" extends Dark; :root { --accent-color: #1e90ff; } Button { border-radius: 4px; }");

            Assert.Equal ("Ocean", name);
            Assert.True (Theme.IsThemeRegistered ("Ocean"));

            Theme.ApplyTheme ("Ocean");

            Assert.Equal (new SKColor (0x1e, 0x90, 0xff), Theme.AccentColor);
            Assert.Equal (SKColor.Parse ("#FF282828"), Theme.BackgroundColor);   // from Dark
            Assert.Equal (4, Button.DefaultStyle.Border.Radius);
        }

        [Fact]
        public void RegisterThemeCss_RequiresName ()
        {
            var ex = Assert.Throws<ThemeCssException> (() => Theme.RegisterThemeCss (":root { --accent-color: red; }"));
            Assert.Contains ("@theme", ex.Message);
        }

        [Fact]
        public void RegisterThemeCss_WithErrors_ThrowsWithAllDiagnostics ()
        {
            var ex = Assert.Throws<ThemeCssException> (() => Theme.RegisterThemeCss ("@theme X; Button { colour: red; widht: 3px; }"));

            Assert.Equal (2, ex.Diagnostics.Count (d => d.Severity == ThemeCssSeverity.Error));
            Assert.Contains ("colour", ex.Message);
            Assert.Contains ("widht", ex.Message);
            Assert.False (Theme.IsThemeRegistered ("X"));
        }

        [Fact]
        public void LoadFromCss_WithErrors_ThrowsAndAppliesNothing ()
        {
            Assert.Throws<ThemeCssException> (() => Theme.LoadFromCss (":root { --accent-color: #010203; } Button { colour: red; }"));

            Assert.NotEqual (new SKColor (1, 2, 3), Theme.AccentColor);
        }

        [Fact]
        public void ApplyStyleSheet_WithErrors_AppliesTheValidParts ()
        {
            var sheet = ThemeStyleSheet.Parse (":root { --accent-color: #010203; --font-size: 12pt; } Button { colour: red; color: blue; }");
            Assert.True (sheet.HasErrors);

            Theme.ApplyStyleSheet (sheet);

            Assert.Equal (new SKColor (1, 2, 3), Theme.AccentColor);
            Assert.Equal (14, Theme.FontSize);   // the bad declaration was dropped, Light's value stays
            Assert.Equal (new SKColor (0, 0, 255), Button.DefaultStyle.ForegroundColor);
        }

        [Fact]
        public void Extends_RegisteredCss_LayersTokensAndRules ()
        {
            Theme.RegisterThemeCss ("@theme Base extends Dark; :root { --accent-color: #010101; } Button { border-radius: 3px; } TextBox { color: red; }");
            Theme.RegisterThemeCss ("@theme Derived extends Base; :root { --accent-color-2: #020202; } TextBox { color: blue; }");

            Theme.ApplyTheme ("Derived");

            Assert.Equal (new SKColor (1, 1, 1), Theme.AccentColor);                  // from Base
            Assert.Equal (new SKColor (2, 2, 2), Theme.AccentColor2);                 // from Derived
            Assert.Equal (SKColor.Parse ("#FF282828"), Theme.BackgroundColor);        // from Dark via Base
            Assert.Equal (3, Button.DefaultStyle.Border.Radius);                       // Base rule kept
            Assert.Equal (new SKColor (0, 0, 255), TextBox.DefaultStyle.ForegroundColor);   // Derived wins
        }

        [Fact]
        public void Extends_RegisteredXml_AndXmlExtendsCss ()
        {
            Theme.RegisterTheme ("<Theme name='XmlBase' base='Dark'><AccentColor>10,10,10</AccentColor></Theme>");
            Theme.RegisterThemeCss ("@theme CssOnXml extends XmlBase; :root { --accent-color-2: #030303; } Button { border-radius: 2px; }");
            Theme.RegisterTheme ("<Theme name='XmlOnCss' base='CssOnXml'><ForegroundColor>4,4,4</ForegroundColor></Theme>");

            Theme.ApplyTheme ("XmlOnCss");

            Assert.Equal (new SKColor (10, 10, 10), Theme.AccentColor);
            Assert.Equal (new SKColor (3, 3, 3), Theme.AccentColor2);
            Assert.Equal (new SKColor (4, 4, 4), Theme.ForegroundColor);
            Assert.Equal (2, Button.DefaultStyle.Border.Radius);   // the CSS link in the chain still applied its rules
        }

        [Fact]
        public void Extends_Unknown_Throws ()
        {
            Theme.RegisterThemeCss ("@theme Orphan extends Nope; :root { --accent-color: red; }");

            var ex = Assert.Throws<ThemeCssException> (() => Theme.ApplyTheme ("Orphan"));
            Assert.Contains ("Nope", ex.Message);
        }

        [Fact]
        public void Extends_Cycle_Throws ()
        {
            Theme.RegisterThemeCss ("@theme A extends B; :root { --accent-color: red; }");
            Theme.RegisterThemeCss ("@theme B extends A; :root { --accent-color: blue; }");

            Assert.Throws<ThemeCssException> (() => Theme.ApplyTheme ("A"));
        }

        [Fact]
        public void ApplyTheme_RaisesThemeChangedExactlyOnce ()
        {
            Theme.RegisterThemeCss ("@theme Multi extends Dark; :root { --accent-color: red; --foreground-color: blue; } Button { color: red; } TextBox { color: red; }");

            var count = 0;
            EventHandler handler = (_, _) => count++;
            Theme.ThemeChanged += handler;
            try {
                Theme.ApplyTheme ("Multi");
            } finally {
                Theme.ThemeChanged -= handler;
            }

            Assert.Equal (1, count);
        }

        // ---- export -------------------------------------------------------------------------------

        [Fact]
        public void ExportCss_RoundTrips_EveryToken ()
        {
            Theme.SetBuiltInTheme (BuiltInTheme.HotDog);
            var css = Theme.ExportCss ("HotDogCopy", "Light");
            var expected = ThemeCssReference.Tokens.ToDictionary (t => t.Name, Theme.FormatTokenValue);

            var sheet = ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            Assert.Equal ("HotDogCopy", sheet.Name);
            Assert.Equal ("Light", sheet.BaseName);
            Assert.Equal (ThemeCssReference.Tokens.Count, sheet.TokenCount);

            Theme.SetBuiltInTheme (BuiltInTheme.Dark);
            Theme.ApplyStyleSheet (sheet);

            foreach (var token in ThemeCssReference.Tokens)
                Assert.Equal (expected[token.Name], Theme.FormatTokenValue (token));
        }

        // ---- diagnostics --------------------------------------------------------------------------

        [Theory]
        [InlineData ("Button { colour: red; }", "Unknown property 'colour'. Did you mean 'color'?")]
        [InlineData ("Buton { color: red; }", "Unknown selector 'Buton'. Did you mean 'Button'?")]
        [InlineData ("Button:disabled { color: red; }", "':disabled' is not supported")]
        [InlineData ("Button:disabled { color: red; }", "--foreground-disabled-color")]
        [InlineData ("TextBox:hover { color: red; }", "'TextBox:hover' is not supported")]
        [InlineData ("* { color: red; }", "universal selector")]
        [InlineData (".primary { color: red; }", "class or id selector")]
        [InlineData ("#main { color: red; }", "class or id selector")]
        [InlineData ("Panel Button { color: red; }", "descendant and combinator selectors are not supported")]
        [InlineData ("Panel > Button { color: red; }", "descendant and combinator selectors are not supported")]
        [InlineData ("Button { font-size: 12pt; }", "Unsupported unit 'pt'")]
        [InlineData ("Button { font-size: 1.2em; }", "Unsupported unit 'em'")]
        [InlineData ("Button { color: red !important; }", "'!important' is not supported")]
        [InlineData ("@import url(x.css);", "'@import' is not supported")]
        [InlineData ("@media (min-width: 100px) { Button { color: red; } }", "'@media' is not supported")]
        [InlineData ("Button { Label { color: red; } }", "Nested blocks are not supported")]
        [InlineData (":root { color: red; }", "':root' only accepts theme tokens")]
        [InlineData ("Button { --x: red; }", "custom properties can only be declared in ':root'")]
        [InlineData ("Button { color: red;", "a '}' is missing")]
        [InlineData ("/* open comment", "Unterminated comment")]
        [InlineData ("Button { color: #12345; }", "is not a valid hex color")]
        [InlineData ("Button { color: #ggg; }", "is not a valid hex color")]
        [InlineData ("Button { border: 1px dashed red; }", "'dashed' border style is not supported")]
        [InlineData ("Button { color: reddish; }", "'reddish' is not a CSS color name")]
        [InlineData ("Button { color: linear-gradient(red, blue); }", "'linear-gradient()' is not a supported color function")]
        [InlineData ("Button { margin: 4px; }", "Unknown property 'margin'")]
        [InlineData ("Button { margin: 4px; }", "Layout properties (margin, padding, width, height, display) have no meaning")]
        [InlineData ("color: red;", "no '{' block")]
        [InlineData ("@theme; Button { color: red; }", "'@theme' must be followed by the theme's name")]
        [InlineData ("@theme A; @theme B;", "Only one '@theme' declaration is allowed")]
        [InlineData ("Button { font-weight: heavy; }", "is not a valid font-weight")]
        [InlineData ("Button { border-width: -1px; }", "is negative")]
        public void Diagnostics_ExplainUnsupportedInput (string css, string expectedFragment)
        {
            var errors = ErrorsOf (css);

            Assert.Contains (expectedFragment, errors);
        }

        [Fact]
        public void Diagnostics_CarryLineAndColumn ()
        {
            var sheet = ThemeStyleSheet.Parse ("Button {\n  color: red;\n  colour: blue;\n}");

            var error = Assert.Single (sheet.Diagnostics);
            Assert.Equal (3, error.Line);
            Assert.Equal (3, error.Column);
            Assert.Contains ("(3:3)", error.ToString ());
        }

        [Fact]
        public void Diagnostics_UnknownSelector_ListsAllSelectors ()
        {
            var errors = ErrorsOf ("Widget { color: red; }");

            foreach (var selector in ThemeCssReference.Selectors)
                Assert.Contains (selector.Name, errors);
        }

        [Fact]
        public void Diagnostics_UnknownProperty_ListsAllProperties ()
        {
            var errors = ErrorsOf ("Button { widget: red; }");

            foreach (var property in ThemeCssReference.PropertyNames)
                Assert.Contains (property, errors);
        }

        [Fact]
        public void Parse_KeepsGoingAfterErrors ()
        {
            var sheet = ThemeStyleSheet.Parse (@"
                Buton { color: red; }
                Button { colour: red; color: blue; margin: 1px; }
                :root { --accent-color: #010203; --accent-color: 12pt; }
                TextBox { color: green; }");

            Assert.True (sheet.HasErrors);
            Assert.Equal (2, sheet.RuleCount);     // Button (one good declaration) and TextBox
            Assert.Equal (1, sheet.TokenCount);
        }

        // ---- reference tables ---------------------------------------------------------------------

        [Fact]
        public void Reference_HasATokenForEveryThemableThemeProperty ()
        {
            var themable = typeof (Theme).GetProperties (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where (p => p.CanWrite && (p.PropertyType == typeof (SKColor) || p.PropertyType == typeof (int) || p.PropertyType == typeof (SKTypeface)))
                .Select (p => p.Name)
                .OrderBy (n => n)
                .ToList ();

            var tokens = ThemeCssReference.Tokens.Select (t => t.PropertyName).OrderBy (n => n).ToList ();

            Assert.Equal (themable, tokens);
        }

        [Fact]
        public void Reference_TokenNamesAreKebabCaseOfProperties ()
        {
            Assert.Equal ("accent-color-2", ThemeCssReference.ToKebabCase ("AccentColor2"));
            Assert.Equal ("ui-font-bold", ThemeCssReference.ToKebabCase ("UIFontBold"));
            Assert.Equal ("control-highlight-low-color", ThemeCssReference.ToKebabCase ("ControlHighlightLowColor"));
            Assert.Equal ("--foreground-color-on-accent", ThemeCssReference.FindToken ("--foreground-color-on-accent")!.Name);
        }

        [Fact]
        public void Reference_EverySelectorTargetsItsOwnStyle ()
        {
            // A selector whose style resolved to Control.DefaultStyle (because the type never declared
            // its own) would silently restyle every control; each must own a distinct layer.
            var seen = new System.Collections.Generic.HashSet<ControlStyle> ();

            foreach (var selector in ThemeCssReference.Selectors) {
                var style = selector.GetStyle ();
                Assert.NotSame (Control.DefaultStyle, style);
                Assert.True (seen.Add (style), $"{selector.Name} shares a style with another selector");

                if (selector.SupportsHover) {
                    var hover = selector.GetHoverStyle! ();
                    Assert.NotSame (Control.DefaultStyleHover, hover);
                    Assert.True (seen.Add (hover), $"{selector.Name}:hover shares a style with another selector");
                }
            }
        }

        [Fact]
        public void Reference_NamedColors_MatchSkiaSharp ()
        {
            var checkedCount = 0;

            foreach (var field in typeof (SKColors).GetFields (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
                if (field.FieldType != typeof (SKColor) || field.Name == "Empty" || field.Name == "Transparent")
                    continue;

                if (!ThemeCssReference.NamedColors.TryGetValue (field.Name, out var ours))
                    continue;

                // The one name where the CSS spec (#8fbc8f) and the GDI/X11 table SkiaSharp inherited
                // (#8fbc8b) disagree. The table follows CSS, since that is what the document promises.
                if (field.Name == "DarkSeaGreen")
                    continue;

                Assert.True (ours == (SKColor) field.GetValue (null)!, $"{field.Name}: table has {ours}, SkiaSharp has {field.GetValue (null)}");
                checkedCount++;
            }

            Assert.True (checkedCount > 100, $"only {checkedCount} names compared");
            Assert.Equal (148, ThemeCssReference.NamedColors.Count);
        }

        // ---- pixels -------------------------------------------------------------------------------

        private static SKBitmap RenderBackBuffer (Control control, Form form)
        {
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) buffer.Invoke (control, null)!;
        }

        private static double Coverage (SKBitmap bitmap, SKColor color)
        {
            var hits = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) == color)
                        hits++;
            return (double) hits / (bitmap.Width * bitmap.Height);
        }

        [Fact]
        public void ButtonRule_IsWhatIsPainted ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Button { background-color: #ff0000; border: none; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var button = new Button { Left = 10, Top = 10, Width = 120, Height = 40, Text = string.Empty };
            form.Controls.Add (button);

            var bitmap = RenderBackBuffer (button, form);

            Assert.True (Coverage (bitmap, new SKColor (255, 0, 0)) > 0.9, "the button should be painted red edge to edge");
        }

        [Fact]
        public void MenuRule_ItemsPaintOnTheMenuBackground ()
        {
            // The strip renderers used to fill every item with Theme.BackgroundColor, so a Menu rule
            // (or a coloured parent) only showed in the gaps between items.
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Menu { background-color: #00ff00; }");

            using var form = new Form { Size = new System.Drawing.Size (400, 200) };
            var menu = new Menu ();
            menu.Items.Add ("File");
            menu.Items.Add ("Edit");
            form.Controls.Add (menu);

            var bitmap = RenderBackBuffer (menu, form);

            Assert.True (Coverage (bitmap, new SKColor (0, 255, 0)) > 0.8, "the whole strip, items included, should be green");
        }
    }
}
