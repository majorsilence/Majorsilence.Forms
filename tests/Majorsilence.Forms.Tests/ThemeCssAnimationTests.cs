using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // @keyframes and the animation-* properties: what parses, and what a control shows frame by frame on the Headless clock.
    [Collection ("Headless")]
    public class ThemeCssAnimationTests : IDisposable
    {
        private static readonly SKColor Black = new (0, 0, 0);
        private static readonly SKColor White = new (0xff, 0xff, 0xff);

        private const string Fade = "@keyframes fade { from { background-color: #000000; } to { background-color: #ffffff; } }";

        public ThemeCssAnimationTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            HeadlessRenderer.PrefersDarkColorScheme = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        private static (Form Form, Panel Panel) Show ()
        {
            var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var panel = new Panel { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 40) };
            form.Controls.Add (panel);
            form.Show ();
            return (form, panel);
        }

        private static SKColor Background (Control control) => control.CurrentStyle.GetBackgroundColor ();

        // 60 Hz: frame 1 is time zero, frame n is (n - 1) * 16.7ms.
        private static void Frames (int count) => HeadlessRenderer.AnimationClock.Step (count);

        // ---- parsing ------------------------------------------------------------------------------

        [Fact]
        public void The_shorthand_expands_to_the_eight_longhands ()
        {
            var sheet = ThemeStyleSheet.Parse ("@keyframes pulse { from { opacity: 1; } 50% { opacity: .5; } to { opacity: 1; } } Panel { animation: pulse 1s ease-in-out infinite alternate; }");
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));

            var rule = Assert.Single (sheet.Rules);
            Assert.Equal (new[] { "animation-name", "animation-duration", "animation-timing-function", "animation-delay", "animation-iteration-count", "animation-direction", "animation-fill-mode", "animation-play-state" },
                rule.Declarations.Select (d => d.Property));
            Assert.Equal ("pulse", rule.Declarations[0].Value.ToString ());
            Assert.Equal ("infinite", rule.Declarations[4].Value.ToString ());
            Assert.Equal ("alternate", rule.Declarations[5].Value.ToString ());
        }

        [Fact]
        public void Keyframes_resolve_into_the_style ()
        {
            Theme.LoadFromCss ("@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: .5; animation-timing-function: linear; } } Panel { animation: pulse 2s 3; }");

            var animation = Panel.DefaultStyle.GetAnimation ()!;
            var frames = Assert.Single (animation.Keyframes!);
            Assert.Equal ("pulse", frames.Name);
            Assert.Equal (new[] { 0.0, 0.5, 1.0 }, frames.Frames.Select (f => f.Offset));
            Assert.Same (ControlTimingFunction.Linear, frames.Frames[1].TimingFunction);
            Assert.Equal (3, animation.At (0).Iterations);
            Assert.Equal (TimeSpan.FromSeconds (2), animation.At (0).Duration);
        }

        [Theory]
        [InlineData ("Panel { animation: nope 1s; }", "there is no '@keyframes nope'")]
        [InlineData ("@keyframes fade { to { opacity: 0; } } Panel { animation: fdae 1s; }", "Did you mean 'fade'")]
        [InlineData ("@keyframes big { to { font-size: 20px; } }", "'font-size' cannot be animated")]
        [InlineData ("@keyframes big { to { border-width: 2px; } }", "'border-width' cannot be animated")]
        [InlineData ("@keyframes odd { 120% { opacity: 0; } }", "a percentage from 0% to 100%")]
        [InlineData ("@keyframes none { to { opacity: 0; } }", "cannot name keyframes")]
        [InlineData ("@-webkit-keyframes fade { to { opacity: 0; } }", "vendor-prefixed")]
        [InlineData ("@media (prefers-reduced-motion) { @keyframes fade { to { opacity: 0; } } }", "cannot be inside '@media'")]
        [InlineData ("@keyframes fade { to { --x: 1px; } }", "cannot declare variables")]
        [InlineData (Fade + " Form { animation: fade 1s; }", "a window's own style does not animate")]
        [InlineData (Fade + " ScrollBar::thumb { animation: fade 1s; }", "does not apply to ScrollBar::thumb")]
        [InlineData (Fade + " Panel { animation: fade 1s 2s 3s; }", "third time")]
        [InlineData (Fade + " Panel { animation: fade fade 1s; }", "a second name")]
        [InlineData (Fade + " Panel { animation: 1s; }", "names no keyframes")]
        [InlineData (Fade + " Panel { animation-direction: sideways; }", "not a direction")]
        [InlineData (Fade + " Panel { animation-iteration-count: -1; }", "not an iteration count")]
        [InlineData (Fade + " Panel { animation-fill-mode: always; }", "not a fill mode")]
        [InlineData (Fade + " Panel { animation-play-state: stopped; }", "not a play state")]
        public void Mistakes_are_errors_that_say_why (string css, string expected) => Assert.Contains (expected, ErrorsOf (css));

        // ---- running ------------------------------------------------------------------------------

        [Fact]
        public void An_animation_plays_through_its_keyframes_then_lets_go ()
        {
            Theme.LoadFromCss (Fade + " Panel { background-color: #ff0000; animation: fade 100ms linear; }");
            var (form, panel) = Show ();
            using var _ = form;

            // It starts the moment the control shows the style: time zero is the first keyframe, not the style's own red.
            Assert.Equal (Black, Background (panel));
            Assert.True (panel.IsStyleAnimationRunning);

            Frames (4);   // 50ms of 100ms
            var middle = Background (panel);
            Assert.InRange (middle.Red, 0x70, 0x90);

            // Without a fill mode the style's own value comes back when it ends.
            Frames (10);
            Assert.False (panel.IsStyleAnimationRunning);
            Assert.Equal (new SKColor (0xff, 0, 0), Background (panel));
        }

        [Fact]
        public void Forwards_keeps_the_last_keyframe ()
        {
            Theme.LoadFromCss (Fade + " Panel { background-color: #ff0000; animation: fade 50ms linear forwards; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);

            Frames (10);
            Assert.Equal (White, Background (panel));
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);   // held, so no more frames are needed
        }

        [Fact]
        public void A_fractional_count_with_forwards_stops_part_way ()
        {
            Theme.LoadFromCss (Fade + " Panel { animation: fade 60ms linear 0.5 forwards; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);

            Frames (10);
            Assert.InRange (Background (panel).Red, 0x70, 0x90);
        }

        [Fact]
        public void Alternate_runs_back_on_odd_iterations ()
        {
            Theme.LoadFromCss (Fade + " Panel { animation: fade 120ms linear infinite alternate; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);

            Frames (10);   // 150ms: iteration 1 (backwards), 25% in, so 75% of the way to white
            var early = Background (panel).Red;
            Frames (3);    // 200ms: 67% in, so 33% of the way
            var late = Background (panel).Red;

            Assert.True (early > late, $"on the way back it darkens: {early:x2} then {late:x2}");
            Assert.True (panel.IsStyleAnimationRunning);
        }

        [Fact]
        public void A_keyframe_that_leaves_a_property_out_animates_from_the_style ()
        {
            Theme.LoadFromCss ("@keyframes brighten { to { background-color: #ffffff; } } Panel { background-color: #000000; animation: brighten 100ms linear; }");
            var (form, panel) = Show ();
            using var _ = form;

            Assert.Equal (Black, Background (panel));
            Frames (4);
            Assert.InRange (Background (panel).Red, 0x70, 0x90);
        }

        [Fact]
        public void A_delay_waits_and_backwards_shows_the_first_keyframe_meanwhile ()
        {
            Theme.LoadFromCss (Fade + " Panel { background-color: #ff0000; animation: fade 50ms linear 100ms; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);
            Frames (3);
            Assert.Equal (new SKColor (0xff, 0, 0), Background (panel));   // in the delay, without a fill: the style's own

            Theme.LoadFromCss (Fade + " Panel { background-color: #ff0000; animation: fade 50ms linear 100ms backwards; }");
            Background (panel);
            Frames (3);
            Assert.Equal (Black, Background (panel));
        }

        [Fact]
        public void A_paused_animation_holds_its_first_frame ()
        {
            Theme.LoadFromCss (Fade + " Panel { animation: fade 50ms linear paused; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);

            Frames (10);
            Assert.Equal (Black, Background (panel));
        }

        [Fact]
        public void Reduced_motion_plays_nothing ()
        {
            HeadlessRenderer.PrefersReducedMotion = true;
            Theme.LoadFromCss (Fade + " Panel { background-color: #ff0000; animation: fade 100ms; }");
            var (form, panel) = Show ();
            using var _ = form;

            Assert.False (panel.IsStyleAnimationRunning);
            Assert.Equal (new SKColor (0xff, 0, 0), Background (panel));
        }

        [Fact]
        public void Applying_a_sheet_without_the_animation_stops_it ()
        {
            Theme.LoadFromCss (Fade + " Panel { animation: fade 1s infinite; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);
            Assert.True (panel.IsStyleAnimationRunning);

            Theme.LoadFromCss ("Panel { background-color: #00ff00; }");
            Assert.Equal (new SKColor (0, 0xff, 0), Background (panel));
            Assert.False (panel.IsStyleAnimationRunning);
        }

        [Fact]
        public void An_animation_wins_over_a_transition_and_its_state ()
        {
            Theme.LoadFromCss (Fade + @"
                Button { background-color: #ff0000; transition: background-color 100ms; animation: fade 1s linear infinite; }
                Button:hover { background-color: #0000ff; }");

            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var button = new Button { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 30) };
            form.Controls.Add (button);
            form.Show ();
            Background (button);
            Frames (3);

            button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, System.Drawing.Point.Empty));
            Frames (2);

            // The animation, still running across the state change (the hover style inherits it), decides the colour: a grey, not
            // the red-to-blue transition.
            var colour = Background (button);
            Assert.Equal (colour.Red, colour.Blue);
            Assert.Equal (colour.Red, colour.Green);
        }

        [Fact]
        public void An_infinite_animation_stops_asking_for_frames_when_hidden ()
        {
            Theme.LoadFromCss (Fade + " Panel { animation: fade 100ms infinite; }");
            var (form, panel) = Show ();
            using var _ = form;
            Background (panel);
            Frames (2);
            Assert.Equal (1, HeadlessRenderer.AnimationClock.PendingCount);

            panel.Visible = false;
            Frames (2);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);

            // Shown and painted again, it picks up.
            panel.Visible = true;
            Background (panel);
            Assert.Equal (1, HeadlessRenderer.AnimationClock.PendingCount);
        }

        [Fact]
        public void An_animation_is_painted ()
        {
            Theme.LoadFromCss ("@keyframes glow { to { box-shadow: 0 0 0 6px #ff0000; } } Panel { background-color: #000000; box-shadow: 0 0 0 6px transparent; animation: glow 50ms linear forwards; }");
            var (form, panel) = Show ();
            using var _ = form;
            HeadlessRenderer.CapturePng (form);
            Frames (10);
            HeadlessRenderer.CapturePng (form);

            var buffer = (SKBitmap) typeof (Control).GetMethod ("GetBackBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke (panel, null)!;
            Assert.Equal (new SKColor (0xff, 0, 0), buffer.GetPixel (2, buffer.Height / 2));
        }
    }
}
