using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The paint effects of the theme CSS -- opacity, blurred / spread / inset / multiple box-shadow, outline, :focus-visible and
    // background-image gradients -- and @media. Each is proved by the pixels it paints (or the state it selects), not by the parser
    // accepting it.
    [Collection ("Headless")]
    public class ThemeCssEffectsTests : IDisposable
    {
        public ThemeCssEffectsTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.PrefersDarkColorScheme = false;
            HeadlessRenderer.PrefersReducedMotion = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            HeadlessRenderer.PrefersDarkColorScheme = false;
            HeadlessRenderer.PrefersReducedMotion = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        // A white container holding one child, both shown on a form.
        private static (Form Form, Panel Container, Panel Child) Scene (int childWidth = 60, int childHeight = 40)
        {
            var form = new Form { Size = new System.Drawing.Size (240, 200) };
            var container = new Panel { Location = new System.Drawing.Point (0, 0), Size = new System.Drawing.Size (200, 150), BackColor = System.Drawing.Color.White };
            var child = new Panel { Location = new System.Drawing.Point (40, 40), Size = new System.Drawing.Size (childWidth, childHeight) };
            container.Controls.Add (child);
            form.Controls.Add (container);
            form.Show ();
            return (form, container, child);
        }

        private static SKBitmap Buffer (Control control, Form form)
        {
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);
            var method = typeof (Control).GetMethod ("GetBackBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) method.Invoke (control, null)!;
        }

        // A pixel at a logical position in a control's surface, which is in device pixels (MF_HEADLESS_SCALE=2 doubles it). Effect
        // lengths (outline width and offset, shadow offsets, gradient stops in px) are device pixels, as border widths are.
        private static SKColor Logical (SKBitmap bitmap, Control control, int x, int y)
        {
            var scale = bitmap.Width / (double) control.Width;
            return bitmap.GetPixel ((int) (x * scale), (int) (y * scale));
        }

        // ---- opacity ------------------------------------------------------------------------------

        [Fact]
        public void Opacity_composites_the_child_translucent_over_its_parent ()
        {
            Theme.LoadFromCss ("Panel { background-color: #000000; opacity: 0.5; }");
            var (form, container, child) = Scene ();
            using var _ = form;
            container.Style.Opacity = 1;   // the container itself stays opaque; only the child fades

            var pixel = Logical (Buffer (container, form), container, 60, 60);

            // Half black over white: mid grey, which only the composite can produce (the child's own buffer is black).
            Assert.InRange (pixel.Red, 0x70, 0x90);
            Assert.Equal (SKColors.Black, Buffer (child, form).GetPixel (10, 10).WithAlpha (255));
        }

        [Fact]
        public void Opacity_takes_a_percentage_and_is_rejected_on_a_window ()
        {
            Theme.LoadFromCss ("Button:disabled { opacity: 65%; }");
            Assert.Equal (0.65f, Button.DefaultStyleDisabled.GetOpacity (), 3);
            Assert.Contains ("Form.Opacity", ErrorsOf ("Form { opacity: .5; }"));
        }

        // ---- box-shadow ---------------------------------------------------------------------------

        [Fact]
        public void A_blurred_shadow_fades_away_from_the_face ()
        {
            Theme.LoadFromCss ("Panel { background-color: #000000; box-shadow: 0 0 12px #ff0000; }");
            var (form, _, child) = Scene (80, 60);
            using var __ = form;
            var bitmap = Buffer (child, form);

            // The face gives up 12px on every side for the shadow.
            Assert.Equal (SKColors.Black, bitmap.GetPixel (40, 30));
            var near = bitmap.GetPixel (10, 30);   // 2px outside the face
            var far = bitmap.GetPixel (1, 30);     // 11px outside
            Assert.True (near.Alpha > far.Alpha, $"a blur fades outwards: {near} near the face, {far} further out");
            Assert.True (near.Red > 0);
        }

        [Fact]
        public void A_spread_grows_a_solid_ring_and_an_inset_shadow_is_cast_inside ()
        {
            Theme.LoadFromCss ("Panel { background-color: #000000; box-shadow: 0 0 0 6px #0000ff, inset 0 0 0 4px #ff0000; }");
            var (form, _, child) = Scene (80, 60);
            using var __ = form;
            var bitmap = Buffer (child, form);

            Assert.Equal (new SKColor (0, 0, 0xff), bitmap.GetPixel (3, 30));     // the spread ring, outside the face (6px)
            Assert.Equal (new SKColor (0xff, 0, 0), bitmap.GetPixel (8, 30));     // the inset ring, just inside the face
            Assert.Equal (SKColors.Black, bitmap.GetPixel (40, 30));              // the face's middle
        }

        [Fact]
        public void None_overrides_an_inherited_shadow ()
        {
            var parent = new ControlStyle (Control.DefaultStyle) { BoxShadow = new ControlBoxShadow (4, 4, SKColors.Red) };
            var style = new ControlStyle (parent) { BoxShadows = Array.Empty<ControlBoxShadow> () };

            Assert.Empty (style.GetBoxShadows ());
            Assert.Null (style.GetBoxShadow ());
            Assert.Single (parent.GetBoxShadows ());
        }

        [Fact]
        public void Shadows_parse_in_any_css_order ()
        {
            var sheet = ThemeStyleSheet.Parse ("Panel { box-shadow: red 1px 2px 3px 4px inset, 0 1px rgb(0 0 0 / 50%); }");
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));

            var value = sheet.Rules.Single ().Declarations.Single ().Value;
            Assert.Equal (ThemeCssValueKind.List, value.Kind);
            Assert.True (value.Items[0].Inset);
            Assert.Equal (3, value.Items[0].Blur);
            Assert.Equal (4, value.Items[0].Spread);
        }

        // ---- outline ------------------------------------------------------------------------------

        [Fact]
        public void An_outline_is_drawn_outside_the_control_by_its_parent ()
        {
            Theme.LoadFromCss ("Panel { background-color: #000000; outline: 2px solid #ff0000; outline-offset: 3px; }");
            var (form, container, child) = Scene ();
            using var _ = form;
            container.Style.OutlineStyle = ControlOutlineStyle.None;   // only the child's outline

            var parent = Buffer (container, form);
            var left = (int) (40 * (parent.Width / (double) container.Width));   // the child's left edge, in device pixels
            var y = (int) (60 * (parent.Width / (double) container.Width));

            // The outline is 3px out and 2px wide: the two pixels at left - 5 and left - 4.
            Assert.Equal (new SKColor (0xff, 0, 0), parent.GetPixel (left - 4, y));
            Assert.Equal (SKColors.White, parent.GetPixel (left - 2, y));            // the 3px gap before the child
            Assert.Equal (SKColors.Black, Logical (parent, container, 60, 60));      // the child, untouched

            // The child's own surface has no outline in it: it takes no room.
            var own = Buffer (child, form);
            Assert.Equal (SKColors.Black, own.GetPixel (0, 20));
        }

        [Fact]
        public void An_outline_without_a_style_draws_nothing ()
        {
            Theme.LoadFromCss ("Panel { background-color: #000000; outline-width: 2px; outline-color: #ff0000; }");
            var (form, container, _) = Scene ();
            using var __ = form;

            var parent = Buffer (container, form);
            Assert.Equal (SKColors.White, parent.GetPixel ((int) (40 * (parent.Width / (double) container.Width)) - 1, 60));
        }

        [Theory]
        [InlineData ("Form { outline: 1px solid red; }", "an outline is drawn by the parent")]
        [InlineData ("Panel { outline: 1px dotted red; }", "'dotted' outline style is not supported")]
        [InlineData ("Panel { outline: 1px solid red blue; }", "not an outline width, style or colour")]
        [InlineData ("ScrollBar::thumb { outline: 1px solid red; }", "does not apply to ScrollBar::thumb")]
        public void Outline_mistakes_are_errors (string css, string expected) => Assert.Contains (expected, ErrorsOf (css));

        // ---- :focus-visible -----------------------------------------------------------------------

        [Fact]
        public void Focus_visible_follows_the_windows_focus_cues ()
        {
            Theme.LoadFromCss (@"
                Button:focus { background-color: #0000ff; }
                Button:focus-visible { background-color: #00ff00; }");

            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var button = new Button { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 30) };
            form.Controls.Add (button);
            form.Show ();
            form.ShowFocusCues = false;

            // Focus from the mouse: no cues, so :focus.
            button.Focus ();
            Assert.Same (button.StyleFocus, button.CurrentStyle);
            Assert.Equal (new SKColor (0, 0, 0xff), button.CurrentStyle.GetBackgroundColor ());

            // The keyboard turns the window's cues on: now :focus-visible, which layers on :focus.
            form.ShowFocusCues = true;
            Assert.Same (button.StyleFocusVisible, button.CurrentStyle);
            Assert.Equal (new SKColor (0, 0xff, 0), button.CurrentStyle.GetBackgroundColor ());
        }

        [Fact]
        public void A_focused_text_box_is_always_focus_visible ()
        {
            Theme.LoadFromCss ("TextBox:focus-visible { border-color: #ff0000; }");

            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var box = new TextBox { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 24) };
            form.Controls.Add (box);
            form.Show ();
            form.ShowFocusCues = false;
            box.Focus ();

            Assert.Same (box.StyleFocusVisible, box.CurrentStyle);
        }

        [Fact]
        public void Focus_visible_is_only_on_controls_with_focus ()
            => Assert.Contains ("':focus-visible' is available on: Button, LinkLabel, TextBox, TrackBar", ErrorsOf ("Label:focus-visible { color: red; }"));

        // ---- background-image ---------------------------------------------------------------------

        // The child's pixels, copied out so the form can be closed: an open form left behind is in Application.OpenForms for every
        // test after this one.
        private SKBitmap Painted (string css, int width = 100, int height = 50)
        {
            Theme.LoadFromCss (css);
            var (form, _, child) = Scene (width, height);
            using (form) {
                var buffer = Buffer (child, form);

                // Scaled back to logical pixels, so the samples below mean the same point at any display scale: these gradients are
                // all proportional to the box.
                var logical = new SKBitmap (width, height);
                using (var canvas = new SKCanvas (logical))
                    canvas.DrawBitmap (buffer, new SKRect (0, 0, width, height));
                return logical;
            }
        }

        [Fact]
        public void A_linear_gradient_runs_along_its_direction ()
        {
            var bitmap = Painted ("Panel { background-color: #ffffff; background-image: linear-gradient(to right, #000000, #ffffff); }");

            Assert.True (bitmap.GetPixel (2, 25).Red < 0x20);
            Assert.True (bitmap.GetPixel (97, 25).Red > 0xe0);
            Assert.InRange (bitmap.GetPixel (50, 25).Red, 0x70, 0x90);
            Assert.Equal (bitmap.GetPixel (50, 5), bitmap.GetPixel (50, 45));   // constant across the other axis
        }

        [Fact]
        public void An_angle_and_to_bottom_aim_the_same_ways_css_does ()
        {
            var right = Painted ("Panel { background-image: linear-gradient(90deg, #000000, #ffffff); }");
            Assert.True (right.GetPixel (2, 25).Red < right.GetPixel (97, 25).Red);

            var down = Painted ("Panel { background-image: linear-gradient(#000000, #ffffff); }");   // the default is 'to bottom'
            Assert.True (down.GetPixel (50, 2).Red < down.GetPixel (50, 47).Red);
            Assert.Equal (down.GetPixel (5, 25), down.GetPixel (95, 25));
        }

        [Fact]
        public void A_repeating_gradient_with_hard_stops_paints_bands ()
        {
            // Stops in px are device pixels, so this one is sampled on the device surface itself.
            Theme.LoadFromCss ("Panel { background-image: repeating-linear-gradient(to right, #ff0000 0 10px, #0000ff 10px 20px); }");
            var (form, _, child) = Scene (100, 50);
            using var _ = form;
            var bitmap = Buffer (child, form);

            Assert.Equal (new SKColor (0xff, 0, 0), bitmap.GetPixel (5, 25));
            Assert.Equal (new SKColor (0, 0, 0xff), bitmap.GetPixel (15, 25));
            Assert.Equal (new SKColor (0xff, 0, 0), bitmap.GetPixel (25, 25));
        }

        [Fact]
        public void A_radial_gradient_changes_outwards_from_its_centre ()
        {
            var bitmap = Painted ("Panel { background-image: radial-gradient(circle at center, #000000, #ffffff); }");

            Assert.True (bitmap.GetPixel (50, 25).Red < bitmap.GetPixel (5, 5).Red);
            // Pixel centres 20.5px either side of the centre; within a few levels, since a scaled surface is resampled here.
            Assert.InRange (bitmap.GetPixel (29, 25).Red - bitmap.GetPixel (70, 25).Red, -4, 4);
        }

        [Fact]
        public void A_conic_gradient_changes_around_its_centre ()
        {
            var bitmap = Painted ("Panel { background-image: conic-gradient(#ff0000, #0000ff); }", 100, 100);

            // From the top, clockwise: just right of 12 o'clock is still red, just left of it nearly blue.
            Assert.True (bitmap.GetPixel (55, 5).Red > bitmap.GetPixel (45, 5).Red);
        }

        [Fact]
        public void Fading_to_transparent_fades_the_colour_not_through_black ()
        {
            // Premultiplied, as CSS: half way it is red at half alpha, which over white is (255, 128, 128). Skia left to itself does
            // not interpolate that way (it gives about (255, 193, 193) here).
            var bitmap = Painted ("Panel { background-color: #ffffff; background-image: linear-gradient(to right, #ff0000, transparent); }");
            var middle = bitmap.GetPixel (50, 25);
            Assert.True (middle.Red > 0xf0, $"got {middle}");
            Assert.InRange (middle.Green, 0x70, 0x90);
        }

        [Fact]
        public void Gradients_follow_the_corner_radius ()
        {
            Theme.LoadFromCss ("Panel { background-color: transparent; border-radius: 20px; background-image: linear-gradient(#000000, #000000); }");
            var (form, _, child) = Scene (100, 50);
            using var _ = form;
            var bitmap = Buffer (child, form);
            Assert.Equal (0, bitmap.GetPixel (0, 0).Alpha);
            Assert.Equal (255, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2).Alpha);
        }

        [Theory]
        [InlineData ("Panel { background-image: url(a.png); }", "a theme has no file to load an image from")]
        [InlineData ("Panel { background-image: linear-gradient(red); }", "at least two colour stops")]
        [InlineData ("Panel { background-image: linear-gradient(red, 30%, blue); }", "transition hint")]
        [InlineData ("Panel { background-image: linear-gradient(in oklch, red, blue); }", "interpolation method is not supported")]
        [InlineData ("Panel { background-image: linear-gradient(to up, red, blue); }", "'to' is followed by a side")]
        [InlineData ("Panel { background-image: radial-gradient(circle 10% at center, red, blue); }", "a circle's size is one length")]
        [InlineData ("Panel { background-image: conic-gradient(red 10px, blue); }", "angles or percentages")]
        [InlineData ("Panel { background-image: lineer-gradient(red, blue); }", "Did you mean 'linear-gradient'")]
        public void Gradient_mistakes_are_errors (string css, string expected) => Assert.Contains (expected, ErrorsOf (css));

        // ---- @media -------------------------------------------------------------------------------

        [Fact]
        public void A_media_query_applies_its_rules_while_it_matches_and_the_sheet_follows_a_change ()
        {
            Theme.LoadFromCss (@"
                :root { --page: #ffffff; }
                @media (prefers-color-scheme: dark) {
                    :root { --page: #000000; --accent-color: #123456; }
                }
                Panel { background-color: var(--page); }");

            Assert.Equal (SKColors.White, Panel.DefaultStyle.BackgroundColor);
            Assert.NotEqual (new SKColor (0x12, 0x34, 0x56), Theme.AccentColor);

            // The operating system goes dark: the sheet applies again, the dark variables and tokens with it.
            HeadlessRenderer.PrefersDarkColorScheme = true;
            Assert.Equal (SKColors.Black, Panel.DefaultStyle.BackgroundColor);
            Assert.Equal (new SKColor (0x12, 0x34, 0x56), Theme.AccentColor);

            HeadlessRenderer.PrefersDarkColorScheme = false;
            Assert.Equal (SKColors.White, Panel.DefaultStyle.BackgroundColor);
        }

        [Fact]
        public void Reduced_motion_can_turn_transitions_off_in_css ()
        {
            Theme.LoadFromCss (@"
                Button { transition: background-color 200ms; }
                @media (prefers-reduced-motion: reduce) { Button { transition: none; } }");

            Assert.True (Button.DefaultStyle.GetTransition ()!.TryGetTiming ("background-color", out _, out _, out _));

            HeadlessRenderer.PrefersReducedMotion = true;
            Assert.False (Button.DefaultStyle.GetTransition ()!.TryGetTiming ("background-color", out _, out _, out _));
        }

        [Theory]
        [InlineData ("(prefers-color-scheme: dark)", true, false, true)]
        [InlineData ("not (prefers-color-scheme: dark)", true, false, false)]
        [InlineData ("screen and (prefers-color-scheme: dark)", true, false, true)]
        [InlineData ("not screen and (prefers-color-scheme: dark)", true, false, false)]
        [InlineData ("(prefers-color-scheme: light), (prefers-reduced-motion)", true, true, true)]
        [InlineData ("(prefers-color-scheme: dark) and (prefers-reduced-motion: no-preference)", true, true, false)]
        [InlineData ("(prefers-color-scheme: light) or (prefers-reduced-motion: reduce)", false, false, true)]
        [InlineData ("print", false, false, false)]
        public void Media_queries_combine (string query, bool dark, bool reducedMotion, bool matches)
        {
            HeadlessRenderer.PrefersDarkColorScheme = dark;
            HeadlessRenderer.PrefersReducedMotion = reducedMotion;

            var sheet = ThemeStyleSheet.Parse ($"@media {query} {{ Panel {{ color: #010203; }} }}");
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            Assert.True (sheet.UsesMediaQueries);
            Assert.Equal (matches, sheet.Rules.Count == 1);
        }

        [Fact]
        public void A_rule_inside_media_says_so_in_the_model ()
        {
            HeadlessRenderer.PrefersDarkColorScheme = true;
            var sheet = ThemeStyleSheet.Parse ("@media (prefers-color-scheme: dark) { Panel { color: #010203; } }");
            Assert.Equal ("(prefers-color-scheme: dark)", sheet.Rules.Single ().Media);
        }

        [Fact]
        public void Nested_media_queries_must_all_match ()
        {
            HeadlessRenderer.PrefersDarkColorScheme = true;
            var sheet = ThemeStyleSheet.Parse ("@media (prefers-color-scheme: dark) { @media (prefers-reduced-motion) { Panel { color: red; } } }");
            Assert.Empty (sheet.Rules);

            HeadlessRenderer.PrefersReducedMotion = true;
            Assert.Single (sheet.Rules);
        }

        [Fact]
        public void A_variable_used_only_under_one_media_is_not_reported_unused ()
        {
            var sheet = ThemeStyleSheet.Parse (":root { --night: #000000; } @media (prefers-color-scheme: dark) { Panel { color: var(--night); } }");
            Assert.DoesNotContain (sheet.Diagnostics, d => d.Message.Contains ("--night"));
        }

        [Fact]
        public void The_system_colour_mode_reports_the_backends_answer ()
        {
            HeadlessRenderer.PrefersDarkColorScheme = true;
            Assert.Equal (SystemColorMode.Dark, Application.SystemColorMode);
            Assert.True (SystemInformation.PrefersDarkColorScheme);
        }

        [Theory]
        [InlineData ("@media (min-width: 600px) { Panel { color: red; } }", "no one viewport size")]
        [InlineData ("@media (hover: hover) { Panel { color: red; } }", "input devices are not detected")]
        [InlineData ("@media (prefers-contrast: more) { Panel { color: red; } }", "no backend reports it yet")]
        [InlineData ("@media (prefers-color-scheme: sepia) { Panel { color: red; } }", "'light' or 'dark'")]
        [InlineData ("@media tv { Panel { color: red; } }", "not a media type")]
        [InlineData ("@media (prefers-color-scheme: dark) and (prefers-reduced-motion) or (prefers-color-scheme: light) { }", "cannot mix 'and' and 'or'")]
        [InlineData ("@media (prefers-color-scheme: dark) { @theme X; }", "'@theme' cannot be inside '@media'")]
        [InlineData ("@media (prefers-color-scheme: dark);", "needs a block")]
        public void Media_mistakes_are_errors (string css, string expected) => Assert.Contains (expected, ErrorsOf (css));

        [Fact]
        public void Print_media_warns_that_it_never_applies ()
            => Assert.Contains (ThemeStyleSheet.Parse ("@media print { Panel { color: red; } }").Diagnostics,
                d => d.Severity == ThemeCssSeverity.Warning && d.Message.Contains ("never matches"));
    }
}
