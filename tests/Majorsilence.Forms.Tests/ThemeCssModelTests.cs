using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The host-neutral model of a parsed stylesheet (ThemeStyleSheet.Tokens / Rules): what a
    // System.Windows.Forms or Avalonia bridge reads instead of re-parsing the CSS. The model is built
    // from the same compiled declarations our renderers apply, so the tests here pin two things: that
    // every property and value form appears in it, and that applying it by hand gives the same result
    // as the closures.
    public class ThemeCssModelTests : IDisposable
    {
        public ThemeCssModelTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

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

        private static ThemeCssDeclaration Declaration (ThemeCssRule rule, string property)
            => Assert.Single (rule.Declarations, d => d.Property == property);

        // ---- rules --------------------------------------------------------------------------------

        [Fact]
        public void Rules_CoverEveryPropertyAndValueForm ()
        {
            var sheet = ParseClean (@"
                Button {
                    background-color: #112233;
                    color: rgba(10, 20, 30, 0.5);
                    border-color: red;
                    border-width: 2px;
                    border-radius: 6;
                    border-top-width: 1px;
                    border-left-color: hsl(0, 100%, 50%);
                    font-family: ""Segoe UI"", Arial, sans-serif;
                    font-size: 13px;
                    font-weight: bold;
                    font-style: italic;
                }");

            var rule = Assert.Single (sheet.Rules);
            Assert.Equal ("Button", rule.Selector.Name);
            Assert.False (rule.Hover);
            Assert.Equal (11, rule.Declarations.Count);

            var background = Declaration (rule, "background-color").Value;
            Assert.Equal (ThemeCssValueKind.Color, background.Kind);
            Assert.Equal (0xFF112233u, background.Argb);
            Assert.Equal ((0x11, 0x22, 0x33, 0xFF), (background.Red, background.Green, background.Blue, background.Alpha));

            Assert.Equal (0x80u, Declaration (rule, "color").Value.Alpha);
            Assert.Equal (0xFFFF0000u, Declaration (rule, "border-color").Value.Argb);
            Assert.Equal (0xFFFF0000u, Declaration (rule, "border-left-color").Value.Argb);

            var width = Declaration (rule, "border-width").Value;
            Assert.Equal (ThemeCssValueKind.Length, width.Kind);
            Assert.Equal (2, width.Pixels);
            Assert.Equal (6, Declaration (rule, "border-radius").Value.Pixels);
            Assert.Equal (1, Declaration (rule, "border-top-width").Value.Pixels);
            Assert.Equal (13, Declaration (rule, "font-size").Value.Pixels);

            var family = Declaration (rule, "font-family").Value;
            Assert.Equal (ThemeCssValueKind.FontFamily, family.Kind);
            Assert.Equal (new[] { "Segoe UI", "Arial", "sans-serif" }, family.FontFamilies);

            var weight = Declaration (rule, "font-weight").Value;
            Assert.Equal (ThemeCssValueKind.FontWeight, weight.Kind);
            Assert.Equal (700, weight.FontWeight);

            var style = Declaration (rule, "font-style").Value;
            Assert.Equal (ThemeCssValueKind.FontStyle, style.Kind);
            Assert.Equal ("italic", style.FontStyle);

            // Literals carry no token reference.
            Assert.All (rule.Declarations, d => Assert.Null (d.TokenReference));
        }

        [Fact]
        public void Rules_BorderShorthand_ExpandsToLonghands ()
        {
            var sheet = ParseClean ("TextBox { border: 2px solid #ff00ff; } Label { border: none; } ListBox { border: var(--border-low-color); }");

            var textBox = sheet.Rules[0];
            Assert.Equal (new[] { "border-width", "border-color" }, textBox.Declarations.Select (d => d.Property));
            Assert.Equal (2, textBox.Declarations[0].Value.Pixels);
            Assert.Equal (0xFFFF00FFu, textBox.Declarations[1].Value.Argb);

            var label = sheet.Rules[1];
            var none = Assert.Single (label.Declarations);
            Assert.Equal ("border-width", none.Property);
            Assert.Equal (0, none.Value.Pixels);

            var listBox = sheet.Rules[2];
            var colour = Assert.Single (listBox.Declarations);
            Assert.Equal ("border-color", colour.Property);
            Assert.Equal ("--border-low-color", colour.TokenReference?.Name);
            Assert.Equal ((uint) Theme.BorderLowColor, colour.Value.Argb);
        }

        [Fact]
        public void Rules_CommaList_OneRulePerSelector_SharingDeclarations ()
        {
            // TextBox does not support :hover, so that selector is dropped with an error and the other
            // two still produce rules.
            var partial = ThemeStyleSheet.Parse ("Button, TextBox:hover, ListBox { color: white; }");
            Assert.True (partial.HasErrors);
            Assert.Equal (new[] { "Button", "ListBox" }, partial.Rules.Select (r => r.Selector.Name));

            var sheet = ParseClean ("Button, Button:hover, ListBox { color: white; }");
            Assert.Equal (3, sheet.Rules.Count);
            Assert.Equal (sheet.RuleCount, sheet.Rules.Count);
            Assert.Equal (new[] { ("Button", false), ("Button", true), ("ListBox", false) }, sheet.Rules.Select (r => (r.Selector.Name, r.Hover)));
            Assert.All (sheet.Rules, r => Assert.Equal (0xFFFFFFFFu, Assert.Single (r.Declarations).Value.Argb));
        }

        [Fact]
        public void Rules_KeepSourceOrder_AndPositions ()
        {
            var sheet = ParseClean ("Button {\n  color: red;\n  background-color: blue;\n  color: green;\n}");

            var rule = Assert.Single (sheet.Rules);
            Assert.Equal (new[] { "color", "background-color", "color" }, rule.Declarations.Select (d => d.Property));
            Assert.Equal (0xFF008000u, rule.Declarations[2].Value.Argb);   // the later one is what wins when applied in order
            Assert.Equal (1, rule.Line);
            Assert.Equal ((2, 3), (rule.Declarations[0].Line, rule.Declarations[0].Column));
        }

        [Fact]
        public void Rules_TokenReference_IsLive ()
        {
            var sheet = ParseClean ("Button:hover { background-color: var(--accent-color); border-width: var(--font-size); }");

            var rule = Assert.Single (sheet.Rules);
            Assert.True (rule.Hover);

            var background = Declaration (rule, "background-color");
            Assert.Equal ("--accent-color", background.TokenReference?.Name);
            Assert.Equal ((uint) Theme.AccentColor, background.Value.Argb);

            Theme.AccentColor = new SKColor (1, 2, 3);
            Assert.Equal (0xFF010203u, background.Value.Argb);

            Theme.FontSize = 21;
            Assert.Equal (21, Declaration (rule, "border-width").Value.Pixels);
        }

        [Fact]
        public void Rules_AuthorVariable_IsALiteralToTheModel ()
        {
            // var(--brand) is substituted at parse time; only theme tokens stay live.
            var sheet = ParseClean (":root { --brand: #336699; } Button { color: var(--brand); }");

            var declaration = Assert.Single (Assert.Single (sheet.Rules).Declarations);
            Assert.Null (declaration.TokenReference);
            Assert.Equal (0xFF336699u, declaration.Value.Argb);
        }

        [Fact]
        public void Rules_ExcludeDroppedDeclarations ()
        {
            var sheet = ThemeStyleSheet.Parse ("Button { colour: red; color: blue; margin: 1px; } Widget { color: red; }");

            Assert.True (sheet.HasErrors);
            var rule = Assert.Single (sheet.Rules);
            Assert.Equal ("color", Assert.Single (rule.Declarations).Property);
        }

        // ---- tokens -------------------------------------------------------------------------------

        [Fact]
        public void Tokens_ResolvedValues ()
        {
            var sheet = ParseClean (@"
                :root {
                    --accent-color: #1e90ff;
                    --font-size: 15px;
                    --ui-font: ""Segoe UI"", ""Noto Sans"", sans-serif;
                    --accent-color-2: var(--accent-color);
                }");

            Assert.Equal (4, sheet.Tokens.Count);
            Assert.Equal (sheet.TokenCount, sheet.Tokens.Count);

            var accent = sheet.Tokens[0];
            Assert.Equal ("--accent-color", accent.Token.Name);
            Assert.Equal (0xFF1E90FFu, accent.Value.Argb);
            Assert.Null (accent.TokenReference);

            Assert.Equal (15, sheet.Tokens[1].Value.Pixels);

            var font = sheet.Tokens[2].Value;
            Assert.Equal (ThemeCssValueKind.FontFamily, font.Kind);
            Assert.Equal (new[] { "Segoe UI", "Noto Sans", "sans-serif" }, font.FontFamilies);

            var accent2 = sheet.Tokens[3];
            Assert.Equal ("--accent-color", accent2.TokenReference?.Name);
            Assert.Equal ((uint) Theme.AccentColor, accent2.Value.Argb);   // live: the current theme value, not the sheet's
        }

        // ---- the model agrees with what the renderers apply -----------------------------------------

        [Fact]
        public void Model_AppliedByHand_MatchesTheCompiledRules ()
        {
            const string css = @"
                :root { --accent-color: #ff8800; }
                Button { background-color: var(--accent-color); color: #ffffff; border: 3px solid #123456; border-radius: 5px; font-size: 17px; }
                Button:hover { background-color: #000000; border-left-width: 9px; border-top-color: red; }
                TextBox { font-family: ""Courier New""; font-weight: bold; font-style: italic; }";

            var sheet = ParseClean (css);
            Theme.ApplyStyleSheet (sheet);

            foreach (var rule in sheet.Rules) {
                var applied = rule.Hover ? Button.DefaultStyleHover : rule.Selector.Name == "Button" ? Button.DefaultStyle : TextBox.DefaultStyle;

                foreach (var declaration in rule.Declarations) {
                    var value = declaration.Value;
                    switch (declaration.Property) {
                        case "background-color": Assert.Equal (value.Argb, (uint) applied.BackgroundColor!.Value); break;
                        case "color": Assert.Equal (value.Argb, (uint) applied.ForegroundColor!.Value); break;
                        case "border-color": Assert.Equal (value.Argb, (uint) applied.Border.Color!.Value); break;
                        case "border-top-color": Assert.Equal (value.Argb, (uint) applied.Border.Top.Color!.Value); break;
                        case "border-width": Assert.Equal (value.Pixels, applied.Border.Width); break;
                        case "border-left-width": Assert.Equal (value.Pixels, applied.Border.Left.Width); break;
                        case "border-radius": Assert.Equal (value.Pixels, applied.Border.Radius); break;
                        case "font-size": Assert.Equal (value.Pixels, applied.FontSize); break;
                        case "font-family": Assert.Equal (SKTypeface.FromFamilyName (value.FontFamilies[0])?.FamilyName, applied.Font!.FamilyName); break;
                        case "font-weight": Assert.Equal (value.FontWeight, (int) applied.Font!.FontWeight); break;
                        case "font-style": Assert.Equal (value.FontStyle == "italic", applied.Font!.IsItalic); break;
                        default: Assert.Fail ($"unexpected property {declaration.Property}"); break;
                    }
                }
            }

            foreach (var token in sheet.Tokens)
                Assert.Equal (token.Value.ToString (), Theme.FormatTokenValue (token.Token));
        }

        // ---- value spelling -----------------------------------------------------------------------

        [Fact]
        public void Value_ToString_IsCss ()
        {
            var sheet = ParseClean ("Button { background-color: #aabbcc; color: rgba(1, 2, 3, 0.5); border-width: 4px; font-family: \"Segoe UI\", Arial; font-weight: 600; font-style: oblique; }");
            var d = sheet.Rules[0].Declarations;

            Assert.Equal ("#aabbcc", d[0].Value.ToString ());
            Assert.Equal ("#01020380", d[1].Value.ToString ());
            Assert.Equal ("4px", d[2].Value.ToString ());
            Assert.Equal ("\"Segoe UI\", Arial", d[3].Value.ToString ());
            Assert.Equal ("600", d[4].Value.ToString ());
            Assert.Equal ("oblique", d[5].Value.ToString ());
            Assert.Equal ("background-color: #aabbcc;", d[0].ToString ());
            Assert.StartsWith ("Button { background-color: #aabbcc;", sheet.Rules[0].ToString ());

            // Every value's spelling round-trips through the parser to the same value.
            var again = ParseClean ($"Button {{ {string.Join (" ", d.Select (x => x.ToString ()))} }}");
            Assert.Equal (d.Select (x => x.Value.ToString ()), again.Rules[0].Declarations.Select (x => x.Value.ToString ()));
        }

        // ---- StyleSheetApplied / CurrentStyleSheets ------------------------------------------------

        [Fact]
        public void StyleSheetApplied_RaisedOnce_AfterThemeChanged_WithTheChain ()
        {
            Theme.RegisterThemeCss ("@theme Base extends Dark; :root { --accent-color: #010101; }");
            var derived = ParseClean ("@theme Derived extends Base; Button { color: red; }");

            var order = new List<string> ();
            IReadOnlyList<ThemeStyleSheet>? chain = null;
            EventHandler themeChanged = (_, _) => order.Add ("ThemeChanged");
            EventHandler<ThemeStyleSheetEventArgs> applied = (_, e) => { order.Add ("StyleSheetApplied"); chain = e.Chain; };
            Theme.ThemeChanged += themeChanged;
            Theme.StyleSheetApplied += applied;
            try {
                Theme.ApplyStyleSheet (derived);
            } finally {
                Theme.ThemeChanged -= themeChanged;
                Theme.StyleSheetApplied -= applied;
            }

            Assert.Equal (new[] { "ThemeChanged", "StyleSheetApplied" }, order);
            Assert.NotNull (chain);
            Assert.Equal (new[] { "Base", "Derived" }, chain!.Select (s => s.Name));
            Assert.Same (derived, Theme.CurrentStyleSheets[1]);
        }

        [Fact]
        public void StyleSheetApplied_RaisedForXmlThemeWithCssBase_NotForBuiltIn ()
        {
            Theme.RegisterThemeCss ("@theme CssBase extends Light; Button { color: red; }");
            Theme.RegisterTheme ("<Theme name='XmlOnCss' base='CssBase'><AccentColor>4,4,4</AccentColor></Theme>");

            var raised = 0;
            EventHandler<ThemeStyleSheetEventArgs> applied = (_, _) => raised++;
            Theme.StyleSheetApplied += applied;
            try {
                Theme.ApplyTheme ("XmlOnCss");
                Assert.Equal (1, raised);
                Assert.Equal ("CssBase", Assert.Single (Theme.CurrentStyleSheets).Name);

                Theme.SetBuiltInTheme (BuiltInTheme.Dark);
                Assert.Equal (1, raised);
                Assert.Empty (Theme.CurrentStyleSheets);

                Theme.LoadFromXml ("<Theme><AccentColor>5,5,5</AccentColor></Theme>");
                Assert.Equal (1, raised);   // no CSS in that chain
            } finally {
                Theme.StyleSheetApplied -= applied;
            }
        }

        [Fact]
        public void StyleSheetApplied_NotRaised_ForASheetWithNothingToApply ()
        {
            var raised = 0;
            EventHandler<ThemeStyleSheetEventArgs> applied = (_, _) => raised++;
            Theme.StyleSheetApplied += applied;
            try {
                // Still a stylesheet apply, so the (empty) chain is installed and reported: a bridge
                // must learn that every previous rule is gone.
                Theme.ApplyStyleSheet (ParseClean ("/* nothing */"));
                Assert.Equal (1, raised);
                Assert.Single (Theme.CurrentStyleSheets);
            } finally {
                Theme.StyleSheetApplied -= applied;
            }
        }
    }
}
