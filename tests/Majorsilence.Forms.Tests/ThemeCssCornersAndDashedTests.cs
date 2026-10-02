using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #286: per-corner border-radius (the CSS one-to-four-value shorthand plus the four longhands) and
    // `border-style: dashed`. Parsed, modelled and painted -- the paint tests are the ones that prove the
    // mechanism, the parser tests only that it is accepted or told off clearly.
    [Collection ("Headless")]
    public class ThemeCssCornersAndDashedTests : IDisposable
    {
        public ThemeCssCornersAndDashedTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static ThemeStyleSheet Parse (string css) => ThemeStyleSheet.Parse (css);

        private static ThemeStyleSheet ParseClean (string css)
        {
            var sheet = Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            return sheet;
        }

        [Theory]
        [InlineData ("4px", 4, 4, 4, 4)]
        [InlineData ("1px 2px", 1, 2, 1, 2)]
        [InlineData ("1px 2px 3px", 1, 2, 3, 2)]
        [InlineData ("1px 2px 3px 4px", 1, 2, 3, 4)]
        [InlineData ("0 8px 0 8px", 0, 8, 0, 8)]
        public void Border_radius_fills_the_four_corners_the_css_way (string value, int tl, int tr, int br, int bl)
        {
            Theme.LoadFromCss ($"Panel {{ border-radius: {value}; }}");
            var border = Panel.DefaultStyle.Border;

            Assert.Equal ((tl, tr, br, bl), (border.GetTopLeftRadius (), border.GetTopRightRadius (), border.GetBottomRightRadius (), border.GetBottomLeftRadius ()));
        }

        [Fact]
        public void A_corner_longhand_wins_over_an_earlier_shorthand_and_a_later_shorthand_resets_it ()
        {
            Theme.LoadFromCss ("Panel { border-radius: 10px; border-top-left-radius: 0; }");
            Assert.Equal (0, Panel.DefaultStyle.Border.GetTopLeftRadius ());
            Assert.Equal (10, Panel.DefaultStyle.Border.GetBottomRightRadius ());

            Theme.LoadFromCss ("Panel { border-top-left-radius: 0; border-radius: 10px; }");
            Assert.Equal (10, Panel.DefaultStyle.Border.GetTopLeftRadius ());
        }

        [Fact]
        public void A_multi_value_radius_reaches_a_host_as_four_longhands ()
        {
            var rule = ParseClean ("Panel { border-radius: 1px 2px; border-style: dashed; }").Rules.Single ();

            Assert.Equal (
                new[] { "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius", "border-style" },
                rule.Declarations.Select (d => d.Property));
            Assert.Equal (new[] { 1, 2, 1, 2 }, rule.Declarations.Take (4).Select (d => d.Value.Pixels));
            Assert.Equal (ThemeCssValueKind.Keyword, rule.Declarations[4].Value.Kind);
            Assert.Equal ("dashed", rule.Declarations[4].Value.Keyword);
        }

        [Fact]
        public void The_border_shorthand_takes_dashed_and_solid_resets_it ()
        {
            Theme.LoadFromCss ("Panel { border: 2px dashed #ff0000; }");
            Assert.Equal (ControlBorderLineStyle.Dashed, Panel.DefaultStyle.Border.GetLineStyle ());
            Assert.Equal (2, Panel.DefaultStyle.Border.GetWidth ());

            Theme.LoadFromCss ("Panel { border-style: dashed; border: 1px solid #ff0000; }");
            Assert.Equal (ControlBorderLineStyle.Solid, Panel.DefaultStyle.Border.GetLineStyle ());

            // An existing solid theme reports no new declaration to a host.
            Assert.DoesNotContain (ParseClean ("Panel { border: 1px solid red; }").Rules.Single ().Declarations, d => d.Property == "border-style");
        }

        [Theory]
        [InlineData ("Panel { border-style: dotted; }", "'dotted' border style is not supported")]
        [InlineData ("Panel { border-style: wavy; }", "not a border style")]
        [InlineData ("Panel { border-style: solid dashed; }", "not a border style")]
        [InlineData ("Panel { border: 1px double red; }", "'double' border style is not supported")]
        [InlineData ("Panel { border-radius: 1px 2px 3px 4px 5px; }", "at most four values")]
        [InlineData ("Panel { border-radius: 1px -2px; }", "negative")]
        [InlineData ("Panel { border-radius: 1px / 2px; }", "border-radius")]
        [InlineData ("Panel { border-top-left-radius: 1px 2px; }", "border-top-left-radius")]
        [InlineData ("ScrollBar::thumb { border-top-left-radius: 2px; }", "does not apply to ScrollBar::thumb")]
        [InlineData ("ScrollBar::thumb { border-style: dashed; }", "does not apply to ScrollBar::thumb")]
        public void Misuse_is_a_diagnostic_never_a_silent_no_op (string css, string fragment)
        {
            var sheet = Parse (css);

            Assert.True (sheet.HasErrors, "expected an error for: " + css);
            Assert.Contains (sheet.Diagnostics, d => d.Message.Contains (fragment, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Border_radius_paints_each_corner_on_its_own ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Panel { background-color: #ff0000; border-radius: 0 40px 0 40px; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);
            var bitmap = Render (panel, form);
            var right = bitmap.Width - 1;
            var bottom = bitmap.Height - 1;

            // The square corners are filled to the very pixel; the rounded ones have been cut away.
            Assert.Equal (255, bitmap.GetPixel (1, 1).Alpha);
            Assert.Equal (255, bitmap.GetPixel (right - 1, bottom - 1).Alpha);
            Assert.Equal (0, bitmap.GetPixel (right - 1, 1).Alpha);
            Assert.Equal (0, bitmap.GetPixel (1, bottom - 1).Alpha);
        }

        [Fact]
        public void A_uniform_radius_still_paints_all_four_corners_round ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Panel { background-color: #ff0000; border-radius: 40px; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);
            var bitmap = Render (panel, form);

            Assert.All (new[] { (1, 1), (bitmap.Width - 2, 1), (1, bitmap.Height - 2), (bitmap.Width - 2, bitmap.Height - 2) },
                p => Assert.Equal (0, bitmap.GetPixel (p.Item1, p.Item2).Alpha));
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_dashed_border_has_gaps_a_solid_one_does_not (bool explicit_zero_radius)
        {
            HeadlessRenderer.Use ();
            var corner = explicit_zero_radius ? "border-radius: 0 0 0 0;" : "";
            Theme.LoadFromCss ($"Panel {{ background-color: #ffffff; border: 3px dashed #000000; {corner} }}");
            var dashed = TopEdgeBorderPixels ();

            Theme.LoadFromCss ($"Panel {{ background-color: #ffffff; border: 3px solid #000000; {corner} }}");
            var solid = TopEdgeBorderPixels ();

            // Along the middle of the top border: solid is one unbroken run, dashed alternates -- both
            // ink and background, in roughly equal measure (dash and gap are the same length).
            Assert.All (solid, black => Assert.True (black));
            Assert.Contains (dashed, black => black);
            Assert.Contains (dashed, black => !black);
            var runs = dashed.Zip (dashed.Skip (1), (a, b) => a != b).Count (changed => changed);
            Assert.True (runs >= 8, $"expected a repeating dash pattern, saw {runs} transitions");
        }

        [Fact]
        public void A_dashed_rounded_border_has_gaps_too ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Panel { background-color: #ffffff; border: 3px dashed #000000; border-radius: 12px 12px 0 0; }");

            var edge = TopEdgeBorderPixels (inset: 30);

            Assert.Contains (edge, black => black);
            Assert.Contains (edge, black => !black);
        }

        // Is each pixel along the middle of the top border (y = 1, inside a 3px stroke) dark? `inset`
        // skips the corners, where a radius bends the line away from the row.
        private static bool[] TopEdgeBorderPixels (int inset = 5)
        {
            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);
            var bitmap = Render (panel, form);

            return Enumerable.Range (inset, bitmap.Width - 2 * inset)
                .Select (x => bitmap.GetPixel (x, 1).Red < 128 && bitmap.GetPixel (x, 1).Alpha > 128)
                .ToArray ();
        }

        private static SKBitmap Render (Control control, Form form)
        {
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) buffer.Invoke (control, null)!;
        }
    }
}
