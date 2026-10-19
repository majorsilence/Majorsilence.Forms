using System;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The value syntax of modern CSS: rem, calc() and its relatives, the CSS Color 4/5 colour functions (oklch(), lab(), color(),
    // color-mix(), relative colours, light-dark()) and custom properties declared inside control rules. Colours are checked against
    // reference conversions, not against this implementation: sRGB red is oklch(62.8% 0.2577 29.23), lab(54.29% 80.82 69.91) and
    // lch(54.29% 106.84 40.85) in CSS Color 4's own sample conversions.
    [Collection ("Headless")]
    public class ThemeCssModernValueTests : IDisposable
    {
        public ThemeCssModernValueTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
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

        private static ThemeCssValue Value (string css, string property)
            => ParseClean (css).Rules.SelectMany (r => r.Declarations).Last (d => d.Property == property).Value;

        private static SKColor Color (string value) => (SKColor) Value ($"Button {{ color: {value}; }}", "color").Argb;

        private static int Length (string value) => Value ($"Button {{ border-width: {value}; }}", "border-width").Pixels;

        private static void Near (SKColor expected, SKColor actual, int tolerance = 1)
        {
            Assert.True (Math.Abs (expected.Red - actual.Red) <= tolerance && Math.Abs (expected.Green - actual.Green) <= tolerance
                         && Math.Abs (expected.Blue - actual.Blue) <= tolerance && Math.Abs (expected.Alpha - actual.Alpha) <= tolerance,
                $"expected {expected} (±{tolerance}), got {actual}");
        }

        // ---- lengths: rem and calc() ------------------------------------------------------------

        [Theory]
        [InlineData ("1rem", 16)]
        [InlineData ("0.125rem", 2)]
        [InlineData ("calc(1rem - 4px)", 12)]
        [InlineData ("calc((2px + 3px) * 2)", 10)]
        [InlineData ("calc(10px / 4)", 2)]          // 2.5 rounds to even, as Math.Round does everywhere in the subset
        [InlineData ("min(4px, 1rem)", 4)]
        [InlineData ("max(4px, 1rem)", 16)]
        [InlineData ("clamp(2px, 10px, 6px)", 6)]
        [InlineData ("calc(2 * 3px)", 6)]
        [InlineData ("calc(4)", 4)]
        public void Lengths_compute (string value, int expected) => Assert.Equal (expected, Length (value));

        [Fact]
        public void A_rem_follows_the_root_font_size_token ()
        {
            Assert.Equal (32, Length ("2rem"));   // 16px by default
            Theme.LoadFromCss (":root { --root-font-size: 20px; } Button { border-width: 0.5rem; }");
            Assert.Equal (10, Button.DefaultStyle.Border.Width);
        }

        [Fact]
        public void A_length_token_in_calc_stays_live ()
        {
            Theme.LoadFromCss ("Button { border-radius: calc(var(--font-size) / 2); }");
            Assert.Equal (Theme.FontSize / 2, Button.DefaultStyle.Border.Radius);

            Theme.FontSize = 30;
            Assert.Equal (15, Button.DefaultStyle.Border.Radius);
        }

        [Fact]
        public void A_time_can_be_computed ()
            => Assert.Equal (200, Value ("Button { transition-duration: calc(100ms * 2); }", "transition-duration").Items[0].Milliseconds);

        [Theory]
        [InlineData ("Button { border-width: 1em; }", "'em' is relative to the inherited font size")]
        [InlineData ("Button { border-width: 10%; }", "nothing to be a percentage of")]
        [InlineData ("Button { border-width: 2pt; }", "points are not supported")]
        [InlineData ("Button { border-width: calc(1px + 2); }", "cannot add a number to a length")]
        [InlineData ("Button { border-width: calc(2px * 3px); }", "one side of '*' must be a plain number")]
        [InlineData ("Button { border-width: calc(2px / 0); }", "division by zero")]
        [InlineData ("Button { border-width: calc(2px -1px); }", "put spaces around '+' and '-'")]
        [InlineData ("Button { border-width: calc(1s); }", "a time, not a length")]
        [InlineData ("Button { border-width: calc(0px - 3px); }", "negative")]
        [InlineData ("Button { border-width: clamp(1px, 2px); }", "clamp() takes three values")]
        [InlineData ("Button { color: (red); }", "not a supported color function")]
        public void Value_mistakes_are_errors_that_say_why (string css, string expected)
            => Assert.Contains (expected, ErrorsOf (css));

        // ---- colours ------------------------------------------------------------------------------

        [Theory]
        [InlineData ("oklch(62.8% 0.2577 29.23)")]
        [InlineData ("oklch(0.628 0.2577 29.23deg)")]
        [InlineData ("oklab(0.62796 0.22486 0.12585)")]
        [InlineData ("lab(54.29% 80.82 69.91)")]
        [InlineData ("lch(54.29 106.84 40.85)")]
        [InlineData ("hwb(0 0% 0%)")]
        [InlineData ("rgb(255 0 0)")]
        [InlineData ("rgb(100% 0% 0%)")]
        [InlineData ("color(srgb 1 0 0)")]
        [InlineData ("color(srgb-linear 1 0 0)")]
        [InlineData ("hsl(0 100% 50%)")]
        [InlineData ("hsl(0turn 100 50)")]
        public void Every_colour_space_spells_red (string value) => Near (new SKColor (0xff, 0, 0), Color (value), 2);

        [Fact]
        public void Hwb_mixes_white_and_black ()
        {
            Near (new SKColor (0x80, 0x80, 0x80), Color ("hwb(0 50% 50%)"));
            Near (new SKColor (0x00, 0xff, 0x00), Color ("hwb(120 0% 0%)"));
        }

        [Fact]
        public void Alpha_after_a_slash ()
        {
            Assert.Equal (128, Color ("rgb(255 0 0 / 50%)").Alpha);
            Assert.Equal (64, Color ("oklch(62.8% 0.2577 29.23 / 0.25)").Alpha);
        }

        [Fact]
        public void None_is_zero ()
            => Near (new SKColor (0, 0, 0xff), Color ("rgb(none none 255)"));

        [Fact]
        public void A_colour_outside_srgb_is_gamut_mapped_keeping_its_hue ()
        {
            // display-p3 red is outside sRGB. Mapping lowers its chroma in OKLCH rather than clipping each channel, so it comes out as a
            // saturated red, not as an off-hue colour.
            var p3 = Color ("color(display-p3 1 0 0)");
            Assert.Equal (255, p3.Red);
            Assert.True (p3.Green < 40 && p3.Blue < 40, $"expected a saturated red, got {p3}");

            // A very high-chroma oklch() blue stays blue.
            var blue = Color ("oklch(50% 0.4 264)");
            Assert.True (blue.Blue > 200 && blue.Red < 100 && blue.Green < 100, $"expected blue, got {blue}");
        }

        [Fact]
        public void Gamut_mapping_keeps_lightness_and_hue_where_clipping_would_not ()
        {
            // A green far outside sRGB. Clipping each channel would move its OKLCH lightness and hue; CSS's mapping only lowers chroma,
            // so both stay where they were written (within the 8-bit rounding of the result).
            var mapped = Color ("oklch(70% 0.35 145)");
            var lch = ThemeCssColorSpaces.FromSKColor (mapped, CssColorSpace.Oklch);

            Assert.InRange (lch.C0, 0.69, 0.71);
            Assert.InRange (lch.C2, 142, 148);
            Assert.True (lch.C1 < 0.35);
        }

        [Fact]
        public void Relative_colours_read_the_origins_channels ()
        {
            Near (new SKColor (0x33, 0x66, 0x99, 128), Color ("rgb(from #336699 r g b / 50%)"));
            Near (new SKColor (0x80, 0x00, 0x00), Color ("hsl(from #ff0000 h s calc(l - 25))"));
            Near (new SKColor (0xff, 0, 0), Color ("oklch(from #ff0000 l c h)"), 2);

            // Bootstrap 6's hover: a little darker and more saturated, same hue.
            var origin = new SKColor (0x0d, 0x6e, 0xfd);
            var darker = Color ("oklch(from #0d6efd calc(l * .9) calc(c * 1.15) h)");
            Assert.True (darker.Red + darker.Green + darker.Blue < origin.Red + origin.Green + origin.Blue);
            Assert.True (darker.Blue > darker.Red && darker.Blue > darker.Green);
        }

        [Fact]
        public void A_relative_colour_of_a_token_follows_the_token ()
        {
            Theme.LoadFromCss ("Button { background-color: rgb(from var(--accent-color) r g b / 50%); }");
            Theme.AccentColor = new SKColor (0x10, 0x20, 0x30);
            Near (new SKColor (0x10, 0x20, 0x30, 128), Button.DefaultStyle.BackgroundColor!.Value);
        }

        [Theory]
        [InlineData ("color-mix(in srgb, red, blue)", 0x80, 0, 0x80, 0xff)]
        [InlineData ("color-mix(in srgb, red 25%, blue)", 0x40, 0, 0xbf, 0xff)]
        [InlineData ("color-mix(in srgb, red, blue 25%)", 0xbf, 0, 0x40, 0xff)]
        [InlineData ("color-mix(in srgb, red 30%, blue 30%)", 0x80, 0, 0x80, 0x99)]   // under 100% in total: that much transparent
        [InlineData ("color-mix(in srgb, red, transparent)", 0xff, 0, 0, 0x80)]       // premultiplied: half-transparent red, not dark red
        public void Color_mix_weights_and_premultiplies (string value, int r, int g, int b, int a)
            => Near (new SKColor ((byte) r, (byte) g, (byte) b, (byte) a), Color (value));

        [Fact]
        public void Color_mix_in_oklch_with_white_keeps_the_hue ()
        {
            // White is achromatic: its hue is powerless, so the mix takes the blue's hue instead of passing through another one. This
            // is how Bootstrap 6 builds its tint scales.
            var tint = Color ("color-mix(in oklch, white 80%, oklch(60% 0.24 240deg))");
            Assert.True (tint.Blue > tint.Red && tint.Blue > tint.Green, $"expected a light blue, got {tint}");
            Assert.True (tint.Red > 180, $"expected a light tint, got {tint}");
        }

        [Fact]
        public void Hue_interpolation_methods_go_the_chosen_way_round ()
        {
            // red (hue 0) to blue (hue 240): the shorter way passes through magenta, the longer through green.
            var shorter = Color ("color-mix(in hsl, hsl(0 100% 50%), hsl(240 100% 50%))");
            var longer = Color ("color-mix(in hsl longer hue, hsl(0 100% 50%), hsl(240 100% 50%))");
            Near (new SKColor (0xff, 0x00, 0xff), shorter);
            Near (new SKColor (0x00, 0xff, 0x00), longer);
        }

        [Fact]
        public void Light_dark_picks_by_the_themes_background ()
        {
            Theme.LoadFromCss ("Button { color: light-dark(#111111, #eeeeee); }");
            Assert.Equal (new SKColor (0x11, 0x11, 0x11), Button.DefaultStyle.ForegroundColor);

            Theme.LoadFromCss ("@theme \"Night\" extends Dark; Button { color: light-dark(#111111, #eeeeee); }");
            Assert.Equal (new SKColor (0xee, 0xee, 0xee), Button.DefaultStyle.ForegroundColor);
        }

        [Theory]
        [InlineData ("Button { color: currentcolor; }", "var(--foreground-color)")]
        [InlineData ("Button { color: color-mix(in hsv, red, blue); }", "not a colour space")]
        [InlineData ("Button { color: color-mix(in srgb, red 0%, blue 0%); }", "add up to 0%")]
        [InlineData ("Button { color: color-mix(in srgb longer hue, red, blue); }", "has no hue")]
        [InlineData ("Button { color: color-mix(red, blue); }", "starts with 'in'")]
        [InlineData ("Button { color: oklch(50% 0.1); }", "needs three channels")]
        [InlineData ("Button { color: oklch(50% 0.1 20 30); }", "is a fourth")]
        [InlineData ("Button { color: oklch(50%, 0.1, 20); }", "Commas are only the legacy form")]
        [InlineData ("Button { color: oklch(50% 0.1 20%); }", "must be a number of degrees")]
        [InlineData ("Button { color: color(rec2020 1 0 0); }", "starts with a colour space")]
        [InlineData ("Button { color: rgb(from #ff0000 r g q); }", "origin colour's channels")]
        [InlineData ("Button { color: light-dark(red); }", "takes two colours")]
        public void Colour_mistakes_are_errors_that_say_why (string css, string expected)
            => Assert.Contains (expected, ErrorsOf (css));

        // ---- custom properties inside rules -----------------------------------------------------

        [Fact]
        public void A_rule_can_declare_its_own_variable ()
        {
            Theme.LoadFromCss ("Button { --bg: #102030; background-color: var(--bg); }");
            Assert.Equal (new SKColor (0x10, 0x20, 0x30), Button.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void A_state_rule_overriding_a_variable_changes_the_base_declarations_that_read_it ()
        {
            // The pattern every component framework uses: the base rule reads the variable, the state rule only sets it.
            var sheet = ParseClean (@"
                Button { --bg: #000000; background-color: var(--bg); border-color: var(--bg); color: #ffffff; }
                Button:hover { --bg: #ffffff; }");

            Theme.ApplyStyleSheet (sheet);

            Assert.Equal (SKColors.Black, Button.DefaultStyle.BackgroundColor);
            Assert.Equal (SKColors.White, Button.DefaultStyleHover.BackgroundColor);
            Assert.Equal (SKColors.White, Button.DefaultStyleHover.Border.Color);

            // Host appliers see it as a :hover rule with the cascaded declarations, and only those (color reads no variable).
            var hover = Assert.Single (sheet.Rules, r => r.Hover);
            Assert.Equal (new[] { "background-color", "border-color" }, hover.Declarations.Select (d => d.Property));
        }

        [Fact]
        public void A_state_rules_own_declarations_win_over_the_cascaded_ones ()
        {
            Theme.LoadFromCss (@"
                Button { --bg: #000000; background-color: var(--bg); }
                Button:hover { --bg: #ffffff; background-color: #ff0000; }");

            Assert.Equal (new SKColor (0xff, 0, 0), Button.DefaultStyleHover.BackgroundColor);
        }

        [Fact]
        public void Each_selector_of_a_list_sees_its_own_variables ()
        {
            Theme.LoadFromCss (@"
                Button { --c: #ff0000; }
                LinkLabel { --c: #0000ff; }
                Button, LinkLabel { color: var(--c); }");

            Assert.Equal (new SKColor (0xff, 0, 0), Button.DefaultStyle.ForegroundColor);
            Assert.Equal (new SKColor (0, 0, 0xff), LinkLabel.DefaultStyle.ForegroundColor);
        }

        [Fact]
        public void A_root_variable_means_what_it_means_on_root ()
        {
            Theme.LoadFromCss (@"
                :root { --a: var(--b); --b: #ff0000; }
                Button { --b: #0000ff; color: var(--a); background-color: var(--b); }");

            Assert.Equal (new SKColor (0xff, 0, 0), Button.DefaultStyle.ForegroundColor);
            Assert.Equal (new SKColor (0, 0, 0xff), Button.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void A_mistake_in_a_selector_list_is_reported_once ()
            => Assert.Single (ThemeStyleSheet.Parse ("Button, Label { color: var(--nope); }").Diagnostics, d => d.Severity == ThemeCssSeverity.Error);

        [Fact]
        public void A_rule_cannot_override_a_theme_token ()
            => Assert.Contains ("theme-wide", ErrorsOf ("Button { --accent-color: red; }"));

        [Fact]
        public void An_unused_rule_variable_is_a_warning ()
        {
            var sheet = ThemeStyleSheet.Parse ("Button { --pad: 4px; color: red; }");
            Assert.False (sheet.HasErrors);
            Assert.Contains (sheet.Diagnostics, d => d.Severity == ThemeCssSeverity.Warning && d.Message.Contains ("--pad"));
        }

        [Fact]
        public void The_root_font_size_token_is_in_the_reference_and_exports ()
        {
            Assert.NotNull (ThemeCssReference.FindToken ("--root-font-size"));
            Assert.Contains ("--root-font-size: 16px;", Theme.ExportCss ());
        }
    }
}
