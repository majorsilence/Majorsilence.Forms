using System;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A CSS transition proved by paint: hovering a themed Button moves its painted background from the normal colour to the :hover
    // colour over the transition's duration, frame by frame on the Headless animation clock, instead of switching at once.
    [Collection ("Headless")]
    public class ThemeCssTransitionPaintTests : IDisposable
    {
        private static readonly SKColor Black = new (0, 0, 0);
        private static readonly SKColor White = new (0xff, 0xff, 0xff);

        // 100ms at 60 Hz is 6 frames; the first frame is time zero.
        private const string LinearSheet = @"
            Button { background-color: #000000; transition: background-color 100ms linear; }
            Button:hover { background-color: #ffffff; }";

        public ThemeCssTransitionPaintTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static (Form Form, Button Button) Show ()
        {
            var form = new Form { Size = new System.Drawing.Size (200, 150) };
            // A decoy takes the shown form's initial focus, so the button is in its plain state, not :focus.
            form.Controls.Add (new Button { Location = System.Drawing.Point.Empty, Size = new System.Drawing.Size (20, 15), TabIndex = 0 });
            var button = new Button { Location = new System.Drawing.Point (40, 40), Size = new System.Drawing.Size (100, 60), TabIndex = 1 };
            form.Controls.Add (button);
            form.Show ();
            return (form, button);
        }

        private static SKColor CenterPixel (Control control, Form form)
        {
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SKBitmap) buffer.Invoke (control, null)!;
            return bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);
        }

        private static void Hover (Button button) => button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, System.Drawing.Point.Empty));

        private static void Unhover (Button button) => button.RaiseMouseLeave (EventArgs.Empty);

        [Fact]
        public void Hover_fades_the_background_over_the_duration ()
        {
            Theme.LoadFromCss (LinearSheet);
            var (form, button) = Show ();
            using var _ = form;

            Assert.Equal (Black, CenterPixel (button, form));

            Hover (button);

            // Painted right after the state change: still the start colour, and a transition is running.
            Assert.Equal (Black, CenterPixel (button, form));
            Assert.True (button.IsStyleTransitionRunning);

            // Frame 1 is time zero; frame 4 is 50ms in, half way on a linear curve.
            HeadlessRenderer.AnimationClock.Step (4);
            var middle = CenterPixel (button, form);
            Assert.InRange (middle.Red, 0x70, 0x90);
            Assert.Equal (middle.Red, middle.Green);

            HeadlessRenderer.AnimationClock.Step (10);
            Assert.False (button.IsStyleTransitionRunning);
            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void The_easing_shapes_the_movement ()
        {
            // ease-in is slow at first: a quarter of the way through time, it is well under a quarter of the way through the colour.
            Theme.LoadFromCss (@"
                Button { background-color: #000000; transition: background-color 120ms ease-in; }
                Button:hover { background-color: #ffffff; }");
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);
            CenterPixel (button, form);
            HeadlessRenderer.AnimationClock.Step (3);   // 33ms of 120ms

            Assert.InRange (CenterPixel (button, form).Red, 1, 0x30);
        }

        [Fact]
        public void Leaving_part_way_starts_back_from_the_colour_on_screen ()
        {
            Theme.LoadFromCss (LinearSheet);
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);
            CenterPixel (button, form);
            HeadlessRenderer.AnimationClock.Step (4);
            var middle = CenterPixel (button, form);

            Unhover (button);

            // Not a jump to white (where hover-in was heading) nor to black (where it is going): it continues from the middle.
            Assert.Equal (middle, CenterPixel (button, form));
            HeadlessRenderer.AnimationClock.Step (3);
            var back = CenterPixel (button, form);
            Assert.True (back.Red < middle.Red, $"expected darker than {middle} on the way back, got {back}");

            HeadlessRenderer.AnimationClock.Step (10);
            Assert.Equal (Black, CenterPixel (button, form));
        }

        [Fact]
        public void Reduced_motion_changes_at_once ()
        {
            HeadlessRenderer.PrefersReducedMotion = true;
            Theme.LoadFromCss (LinearSheet);
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);

            Assert.False (button.IsStyleTransitionRunning);
            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void Without_a_transition_the_change_is_instant ()
        {
            Theme.LoadFromCss (@"
                Button { background-color: #000000; }
                Button:hover { background-color: #ffffff; }");
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);

            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void A_property_the_transition_does_not_list_switches_at_once ()
        {
            Theme.LoadFromCss (@"
                Button { background-color: #000000; transition: color 100ms linear; }
                Button:hover { background-color: #ffffff; }");
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);

            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void A_delay_holds_the_start_value ()
        {
            Theme.LoadFromCss (@"
                Button { background-color: #000000; transition: background-color 50ms linear 50ms; }
                Button:hover { background-color: #ffffff; }");
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            Hover (button);
            CenterPixel (button, form);
            HeadlessRenderer.AnimationClock.Step (3);   // 33ms: still inside the delay

            Assert.Equal (Black, CenterPixel (button, form));
            HeadlessRenderer.AnimationClock.Step (10);
            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void A_shadow_collapses_on_press ()
        {
            // The "hard shadow that collapses on press" look, animated: the shadow offset moves 8px -> 0 rather than jumping.
            Theme.LoadFromCss (@"
                Button { background-color: #000000; box-shadow: 8px 8px #ff0000; transition: box-shadow 100ms linear; }
                Button:active { box-shadow: 0px 0px #ff0000; }");
            var (form, button) = Show ();
            using var _ = form;
            CenterPixel (button, form);

            button.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, System.Drawing.Point.Empty));
            CenterPixel (button, form);
            HeadlessRenderer.AnimationClock.Step (4);
            CenterPixel (button, form);

            var shadow = button.CurrentStyle.GetBoxShadow ();
            Assert.NotNull (shadow);
            Assert.InRange (shadow!.Value.OffsetX, 3, 5);

            HeadlessRenderer.AnimationClock.Step (10);
            CenterPixel (button, form);
            Assert.Equal (0, button.CurrentStyle.GetBoxShadow ()!.Value.OffsetX);
        }

        [Fact]
        public void An_explicit_back_colour_outlasts_focus_without_animating ()
        {
            // The control's own BackColor wins in :focus too (GetEffectiveBackgroundColor), so both ends of the change are the same
            // colour and there is nothing to animate.
            Theme.LoadFromCss (@"
                Button { background-color: #000000; transition: background-color 100ms linear; }
                Button:focus { background-color: #ffffff; }");
            var (form, button) = Show ();
            using var _ = form;
            button.Style.BackgroundColor = new SKColor (0x20, 0x40, 0x60);
            CenterPixel (button, form);

            button.Focus ();

            Assert.False (button.IsStyleTransitionRunning);
            Assert.Equal (new SKColor (0x20, 0x40, 0x60), CenterPixel (button, form));
        }

        [Fact]
        public void Interpolation_is_premultiplied ()
        {
            // Fading from transparent to opaque red stays red all the way; straight (unpremultiplied) interpolation would drag the
            // colour channels towards transparent's black.
            var half = Control.InterpolateColor (SKColors.Transparent, new SKColor (0xff, 0, 0), 0.5f);
            Assert.Equal (0xff, half.Red);
            Assert.InRange (half.Alpha, 0x7f, 0x80);
        }
    }
}
