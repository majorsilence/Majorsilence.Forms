using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression: an anchored control stayed wherever a previous resize had left it. The anchor
    // engine skips re-capturing a control's distance-from-edges while its bounds still match the last
    // capture, but moving it during layout did not refresh that capture -- so the NEXT resize saw
    // "bounds changed", re-snapshotted a control still sitting at its old position against the
    // container's already-updated DisplayRectangle, and stored a garbage delta that pinned it there.
    // Growing a window then shrinking it back therefore left its right-anchored controls hanging off
    // the edge, which is how this was found (a media player's transport buttons).
    public class AnchorRepeatedResizeTests
    {
        private static (Form Form, Panel Panel, Button Anchored) BuildBottomBar ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            var panel = new Panel {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point (0, 100),
                Size = new Size (600, 80)
            };
            var anchored = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point (520, 20),
                Size = new Size (60, 40)
            };

            panel.Controls.Add (anchored);
            form.Controls.Add (panel);
            form.ClientSize = new Size (600, 180);

            return (form, panel, anchored);
        }

        // CapturePng forces a real layout pass at the new size, the way a resize would.
        private static void Resize (Form form, int width, int height)
        {
            form.ClientSize = new Size (width, height);
            HeadlessRenderer.CapturePng (form, width, height);
        }

        [Fact]
        public void Right_anchored_child_returns_to_its_gap_after_grow_then_shrink ()
        {
            var (form, panel, anchored) = BuildBottomBar ();
            using (form) {
                Resize (form, 600, 180);
                var gap = panel.Width - anchored.Bounds.Right;

                Resize (form, 1000, 400);
                Assert.Equal (gap, panel.Width - anchored.Bounds.Right);

                Resize (form, 600, 180);
                Assert.Equal (gap, panel.Width - anchored.Bounds.Right);
                Assert.True (anchored.Bounds.Right <= panel.Width,
                    $"anchored child hangs off the panel: {anchored.Bounds} in a panel {panel.Width} wide");
            }
        }

        [Fact]
        public void Right_anchored_child_keeps_its_gap_across_several_resizes ()
        {
            // The capture went stale on the FIRST move, so every resize after that one was wrong --
            // the direction did not matter, shrinking just made it visible.
            var (form, panel, anchored) = BuildBottomBar ();
            using (form) {
                Resize (form, 600, 180);
                var gap = panel.Width - anchored.Bounds.Right;

                foreach (var width in new[] { 900, 1200, 700, 1400, 600, 1000 }) {
                    Resize (form, width, 300);
                    Assert.Equal (gap, panel.Width - anchored.Bounds.Right);
                }
            }
        }

        [Fact]
        public void Bottom_anchored_child_keeps_its_gap_after_grow_then_shrink ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var anchored = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point (20, 120),
                Size = new Size (60, 40)
            };
            form.Controls.Add (anchored);
            form.ClientSize = new Size (400, 200);

            Resize (form, 400, 200);
            var gap = form.ClientSize.Height - anchored.Bounds.Bottom;

            Resize (form, 400, 500);
            Assert.Equal (gap, form.ClientSize.Height - anchored.Bounds.Bottom);

            Resize (form, 400, 200);
            Assert.Equal (gap, form.ClientSize.Height - anchored.Bounds.Bottom);
        }
    }
}
