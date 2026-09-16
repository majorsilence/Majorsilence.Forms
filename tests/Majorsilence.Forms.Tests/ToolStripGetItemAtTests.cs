using System.Linq;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ToolStrip.GetItemAt, carried over from the W6.3 hit-test audit as "public API with no internal
    // mouse caller, so the question is which space an application is expected to pass".
    //
    // The space question turned out to be the smaller half. GetItemAt built its hit rectangle from
    // `new Rectangle (item.Bounds.Location, item.Size)` -- the laid-out POSITION paired with the
    // requested SIZE. Those are two separate stores here (see ToolStripItem.Size's remarks): Size is
    // what the application asked for and stays 0,0 on an item nobody sized explicitly. An empty
    // rectangle contains no point, so GetItemAt returned null for every point on a normally-built
    // strip, at every scale. Not a coordinate-space defect -- an API that never answered at all.
    //
    // With that fixed to test item.Bounds, the space answer falls out and is cross-validated rather
    // than read off the source: Bounds is logical, the same space MouseEventArgs.Location arrives in
    // and the same space the sibling MenuBase.GetItemAtLocation takes. The scale-2 test below pins
    // that by agreeing with a real click driven through the backend, which is the check #189 showed
    // is the only reliable one here -- MenuBase reads identically to the code that was broken in
    // Ribbon and is correct, so reading cannot settle it.
    [Collection ("Headless")]
    public class ToolStripGetItemAtTests
    {
        private static ToolStrip Built (out Form form)
        {
            HeadlessRenderer.Use ();

            var strip = new ToolStrip { Width = 300, Height = 30 };
            strip.Items.Add (new ToolStripButton { Text = "Alpha" });
            strip.Items.Add (new ToolStripButton { Text = "Beta" });
            strip.Items.Add (new ToolStripButton { Text = "Gamma" });

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        private static Point Centre (ToolStripItem item)
            => new (item.Bounds.Left + item.Bounds.Width / 2, item.Bounds.Top + item.Bounds.Height / 2);

        [Fact]
        public void It_returns_the_item_under_the_point ()
        {
            // The whole of the defect: every one of these answered null before, because the items were
            // never explicitly sized and so carried a 0,0 requested size.
            using var strip = Built (out var form);

            try {
                foreach (var item in strip.Items.Cast<ToolStripItem> ())
                    Assert.Same (item, strip.GetItemAt (Centre (item)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_items_really_were_never_sized ()
        {
            // The premise the test above rests on, asserted rather than assumed: Size is a separate
            // store from the laid-out Bounds and nothing populates it during layout. Without this,
            // "returns the item" could be passing on a strip that happened to have sizes.
            using var strip = Built (out var form);

            try {
                foreach (var item in strip.Items.Cast<ToolStripItem> ()) {
                    Assert.Equal (Size.Empty, item.Size);
                    Assert.NotEqual (Size.Empty, item.Bounds.Size);
                }
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_wrong_requested_size_does_not_move_the_hit_region ()
        {
            // Discriminates the fix from the old code in the one configuration where the old code
            // answered anything at all. Setting Size makes the old rectangle non-empty, so a test that
            // only sized items and asked for a hit would pass either way. Here Size is set to a
            // deliberately wrong extent -- a sliver -- while AutoSize keeps the laid-out Bounds as they
            // were. The old code would miss the item's real right-hand side and, at the far edge, hand
            // back the wrong item; the fix ignores the request and tests where the item actually is.
            using var strip = Built (out var form);

            try {
                var beta = (ToolStripItem)strip.Items[1];
                var laid_out = beta.Bounds;

                beta.Size = new Size (2, 2);

                Assert.Equal (laid_out, beta.Bounds);
                Assert.Same (beta, strip.GetItemAt (new Point (laid_out.Right - 1, laid_out.Top + laid_out.Height / 2)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_point_in_no_item_returns_null ()
        {
            // GUARD: a hit-test that returns the first item for everything would satisfy the tests
            // above and be just as broken in the other direction.
            using var strip = Built (out var form);

            try {
                var last = strip.Items.Cast<ToolStripItem> ().Last ();

                Assert.Null (strip.GetItemAt (new Point (last.Bounds.Right + 20, 5)));
                Assert.Null (strip.GetItemAt (new Point (5, strip.Height + 40)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_unavailable_item_is_skipped ()
        {
            // Pre-existing behaviour the fix must keep: the Available check was the one part of the
            // original that worked, and it was unobservable while the method answered null regardless.
            using var strip = Built (out var form);

            try {
                var beta = (ToolStripItem)strip.Items[1];
                var centre = Centre (beta);

                Assert.Same (beta, strip.GetItemAt (centre));

                beta.Available = false;

                Assert.Null (strip.GetItemAt (centre));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_int_overload_agrees ()
        {
            using var strip = Built (out var form);

            try {
                var beta = (ToolStripItem)strip.Items[1];
                var centre = Centre (beta);

                // Naming the expected item, not just agreement between the two: while GetItemAt
                // answered null for everything the overloads agreed perfectly and said nothing.
                Assert.Same (beta, strip.GetItemAt (centre.X, centre.Y));
                Assert.Same (strip.GetItemAt (centre), strip.GetItemAt (centre.X, centre.Y));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void At_scale_two_it_agrees_with_a_real_click ()
        {
            // The space answer, cross-validated against the backend instead of against a reading.
            //
            // Aims at the SECOND item, because the first item's rectangle starts at the origin and a
            // device-vs-logical mix-up there lands inside itself -- the accident that made two earlier
            // versions of the Ribbon test pass while proving nothing. The guard below asserts this
            // fixture genuinely separates the two readings before the assertion means anything.
            Application.UiScale = 1;

            using (var probe = new Form ())
                Application.UiScale = probe.DesktopScaling > 0 ? 2 / probe.DesktopScaling : 2;

            try {
                using var strip = Built (out var form);

                try {
                    var beta = (ToolStripItem)strip.Items[1];
                    var gamma = (ToolStripItem)strip.Items[2];
                    var centre = Centre (beta);

                    // PREMISE: reading this logical point as though it were device lands in a
                    // different item, so "GetItemAt returned Beta" is a real discrimination.
                    Assert.True (gamma.Bounds.Contains (new Point (centre.X * 2, centre.Y)),
                        $"fixture does not discriminate: 2x of {centre} is not inside Gamma at {gamma.Bounds}");

                    Assert.Same (beta, strip.GetItemAt (centre));

                    // And the same logical point, driven as a real click, reaches the same item.
                    ToolStripItem? clicked = null;
                    foreach (var item in strip.Items.Cast<ToolStripItem> ())
                        item.Click += (s, _) => clicked = (ToolStripItem?)s;

                    var window = WindowPoint.In (strip, centre);
                    HeadlessRenderer.MouseDown (form, window.X, window.Y);
                    HeadlessRenderer.MouseUp (form, window.X, window.Y);
                    Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

                    Assert.Same (beta, clicked);
                } finally {
                    form.Close ();
                }
            } finally {
                Application.UiScale = 1;
            }
        }
    }
}
