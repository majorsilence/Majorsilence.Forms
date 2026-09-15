using System.Drawing;
using Majorsilence.Forms.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.4 (RC-9): a member that REPORTS STATE must compute it or throw -- never return a plausible
    // constant. A plausible constant is the worst failure mode available here, because it is
    // indistinguishable from a correct answer at the call site and no test that merely calls the
    // member ever goes red.
    //
    // Graphics had four of them, all in the clipping family and all answerable from ClipBounds, which
    // has been real the whole time: IsVisibleClipEmpty returned false and the three IsVisible overloads
    // returned true. Between them they are the early-out every custom-drawn control is written around
    // -- `if (e.Graphics.IsVisibleClipEmpty) return;` and `if (!g.IsVisible (row)) continue;` -- so a
    // constant answer means the control does all the work it asked to skip, and answers "visible" for
    // a point that demonstrably is not.
    public class GettersThatReportStateTests
    {
        private static Bitmap Surface () => new Bitmap (100, 100);

        // ---------------- Graphics.IsVisible

        [Fact]
        public void A_point_outside_the_clip_is_not_visible ()
        {
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 20, 20));

            Assert.True (g.IsVisible (new Point (5, 5)));
            Assert.False (g.IsVisible (new Point (50, 50)));
        }

        [Fact]
        public void A_rectangle_outside_the_clip_is_not_visible ()
        {
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 20, 20));

            Assert.False (g.IsVisible (new Rectangle (40, 40, 10, 10)));
        }

        [Fact]
        public void A_rectangle_straddling_the_clip_edge_is_visible ()
        {
            // Intersects, not contains -- which is what System.Drawing.Graphics.IsVisible means. A
            // rectangle half inside the clip is partly visible, and a caller that skipped it on a
            // "contained" test would leave a hole exactly at the clip boundary.
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 20, 20));

            Assert.True (g.IsVisible (new Rectangle (10, 10, 40, 40)));
        }

        [Fact]
        public void Everything_is_visible_when_nothing_is_clipped ()
        {
            // GUARD, not proof: the common case has to keep answering true, or every custom-drawn
            // control that early-outs on IsVisible would stop drawing entirely.
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            Assert.True (g.IsVisible (new Point (50, 50)));
            Assert.True (g.IsVisible (new Rectangle (10, 10, 20, 20)));
        }

        // ---------------- Graphics.IsVisibleClipEmpty

        [Fact]
        public void An_empty_clip_reports_empty ()
        {
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            Assert.False (g.IsVisibleClipEmpty);

            g.SetClip (Rectangle.Empty);

            Assert.True (g.IsVisibleClipEmpty);
        }

        [Fact]
        public void A_clip_with_area_does_not_report_empty ()
        {
            // GUARD, not proof: the early-out `if (e.Graphics.IsVisibleClipEmpty) return;` at the top
            // of a paint handler must not start firing on a normal paint.
            using var surface = Surface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 20, 20));

            Assert.False (g.IsVisibleClipEmpty);
        }
    }
}
