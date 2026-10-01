using System;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #285: :active, :disabled and :focus pick the right ControlStyle at paint time, and box-shadow
    // paints a hard, offset, no-blur shadow behind the control's own shape. Each is proved by paint,
    // not by the parser accepting it -- the same standard ThemeCssSelectorPaintTests (#100) set.
    // Touches HeadlessRenderer.Use () / the active backend, so this runs in the serialized Headless
    // collection along with every other such test (a real, twice-reproduced CI race otherwise).
    [Collection ("Headless")]
    public class ThemeCssStatesAndShadowPaintTests : IDisposable
    {
        public ThemeCssStatesAndShadowPaintTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        [Fact]
        public void CurrentStyle_picks_the_right_pseudo_class_background ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss (@"
                Button { background-color: #000000; }
                Button:active { background-color: #ff00ff; }
                Button:disabled { background-color: #00ff00; }
                Button:focus { background-color: #0000ff; }
            ");

            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var button = new Button { Dock = DockStyle.Fill };
            form.Controls.Add (button);
            form.Show ();

            // Ground truth for the "none of the four states apply" case below: nothing has focused,
            // pressed or disabled the button yet. If this ever fails, some form-activation path now
            // focuses the lone control on Show () and the fixture (not the production code) needs the
            // update, e.g. giving it an unfocusable sibling.
            Assert.True (button.Enabled);
            Assert.False (button.Focused);
            Assert.False (button.IsPressed);
            Assert.Equal (SKColors.Black, CenterPixel (button, form));

            // Pressing also selects the control (W6 mechanisms), so this proves :active outranks
            // :focus too -- both are true here, and the active colour is what shows.
            button.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, System.Drawing.Point.Empty));
            Assert.True (button.IsPressed);
            Assert.Equal (new SKColor (0xff, 0x00, 0xff), CenterPixel (button, form));

            // Releasing clears :active but the control stays focused -- :focus shows on its own.
            button.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, System.Drawing.Point.Empty));
            Assert.False (button.IsPressed);
            Assert.True (button.Focused);
            Assert.Equal (new SKColor (0x00, 0x00, 0xff), CenterPixel (button, form));

            // Disabled outranks everything, regardless of the residual press/focus state above.
            button.Enabled = false;
            Assert.Equal (new SKColor (0x00, 0xff, 0x00), CenterPixel (button, form));
        }

        [Fact]
        public void A_selector_without_its_own_rule_still_resolves_through_the_default_chain ()
        {
            // No :active/:disabled/:focus rule is loaded at all -- TrackBar's pseudo-class styles must
            // still resolve (through DefaultStyleActive/Disabled/Focus -> DefaultStyle) to the same
            // background as the plain rule, exactly as :hover already does. A control themed for no
            // state at all must look exactly as it did before this existed.
            Theme.LoadFromCss ("TrackBar { background-color: #123456; }");

            var trackBar = new TrackBar ();

            Assert.Equal (new SKColor (0x12, 0x34, 0x56), trackBar.StyleActive.GetBackgroundColor ());
            Assert.Equal (new SKColor (0x12, 0x34, 0x56), trackBar.StyleDisabled.GetBackgroundColor ());
            Assert.Equal (new SKColor (0x12, 0x34, 0x56), trackBar.StyleFocus.GetBackgroundColor ());
        }

        [Fact]
        public void BoxShadow_paints_a_hard_offset_shadow_behind_the_face ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Panel { background-color: #000000; box-shadow: 20px 20px #ff00ff; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);

            var bitmap = Render (panel, form);

            // The face is flush with the top-left corner (a positive offset moves the shadow right and
            // down); only the shadow's own sliver shows past the face's far edge -- a hard edge, no
            // blur, no halo on the near corner.
            Assert.Equal (SKColors.Black, bitmap.GetPixel (5, 5));
            Assert.Equal (new SKColor (0xff, 0x00, 0xff), bitmap.GetPixel (bitmap.Width - 5, bitmap.Height - 5));
            Assert.NotEqual (new SKColor (0xff, 0x00, 0xff), bitmap.GetPixel (5, bitmap.Height - 5));
            Assert.NotEqual (new SKColor (0xff, 0x00, 0xff), bitmap.GetPixel (bitmap.Width - 5, 5));
        }

        [Fact]
        public void BoxShadow_negative_offset_mirrors_to_the_near_corner ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Panel { background-color: #000000; box-shadow: -20px -20px #ff00ff; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (panel);

            var bitmap = Render (panel, form);

            // A negative offset is the mirror image: the shadow peeks out past the TOP-LEFT corner
            // instead of the bottom-right one.
            Assert.Equal (new SKColor (0xff, 0x00, 0xff), bitmap.GetPixel (5, 5));
            Assert.Equal (SKColors.Black, bitmap.GetPixel (bitmap.Width - 5, bitmap.Height - 5));
        }

        private static SKColor CenterPixel (Control control, Form form)
        {
            var bitmap = Render (control, form);
            return bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);
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
