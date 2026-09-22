using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the dead-event sweep (RC-5) -- ToolStripItem's change events. Seven were declared behind
    // the CS0067 pragma at the top of ToolStripParity.cs and raised by nothing:
    //
    //   TextChanged, BackColorChanged, ForeColorChanged, RightToLeftChanged   property setters
    //   LocationChanged                                                       layout / SetBounds
    //   SelectedChanged                                                       hover (Selected => Hovered)
    //   OwnerChanged                                                          Items.Add / Remove
    //
    // plus MouseMove, which the strip's own mouse path never forwarded. dotnet/winforms raises every
    // one of these from the corresponding setter or path, checked in ToolStripItem.cs before writing
    // any of this.
    //
    // The state lives on MenuItem (this library's base) and the events on ToolStripItem (the WinForms
    // face), so MenuItem announces through internal seams and ToolStripItem raises. A plain MenuItem
    // has no subscribers to tell, which is why the seams are no-ops there.
    [Collection ("Headless")]
    public class ToolStripItemChangeEventsTests
    {
        // Drives the protected mouse entry point a backend would, as the DataGridView tests do.
        private sealed class DrivenStrip : ToolStrip
        {
            internal void MoveTo (Point at)
                => OnMouseMove (new MouseEventArgs (MouseButtons.None, 0, at.X, at.Y, Point.Empty));

            internal void PointerLeaves () => OnMouseLeave (System.EventArgs.Empty);
        }

        private static DrivenStrip Strip (out Form form, out ToolStripButton item)
        {
            HeadlessRenderer.Use ();

            item = new ToolStripButton { Text = "Open" };

            var strip = new DrivenStrip { Width = 300, Height = 30, GripVisible = false };
            strip.Items.Add (item);

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        // ---------------- the four setter-driven events

        [Fact]
        public void Text_notifies_on_change ()
        {
            Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.TextChanged += (_, _) => raised++;

                item.Text = "Save";

                Assert.Equal (1, raised);

                item.Text = "Save";

                Assert.Equal (1, raised);
            }
        }

        [Theory]
        [InlineData ("BackColor")]
        [InlineData ("ForeColor")]
        public void Colours_notify_on_change_and_not_on_reassignment (string which)
        {
            Strip (out var form, out var item);

            using (form) {
                var raised = 0;

                if (which == "BackColor") {
                    item.BackColorChanged += (_, _) => raised++;
                    item.BackColor = Color.Red;
                    item.BackColor = Color.Red;
                } else {
                    item.ForeColorChanged += (_, _) => raised++;
                    item.ForeColor = Color.Red;
                    item.ForeColor = Color.Red;
                }

                Assert.Equal (1, raised);
            }
        }

        [Fact]
        public void RightToLeft_notifies_on_change ()
        {
            Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.RightToLeftChanged += (_, _) => raised++;

                item.RightToLeft = RightToLeft.Yes;
                item.RightToLeft = RightToLeft.Yes;

                Assert.Equal (1, raised);
            }
        }

        // ---------------- LocationChanged

        // Turning the grip on reserves a band at the strip's leading edge and layout shifts every item
        // right to make room (TSM-43) -- a genuine location change produced by layout, which is how
        // upstream's LocationChanged is normally reached.
        [Fact]
        public void Layout_moving_an_item_raises_LocationChanged ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.LocationChanged += (_, _) => raised++;

                var before = item.Bounds.Left;
                strip.GripVisible = true;
                strip.PerformLayout ();
                PaintSurface.Render (strip).Dispose ();

                Assert.True (item.Bounds.Left > before, "the grip did not move the item -- fixture problem, not the event");
                Assert.True (raised > 0, "Layout moved the item and LocationChanged did not fire.");
            }
        }

        // Layout re-runs SetBounds on every paint. If a resize that leaves the top-left in place
        // counted as a move, this event would fire on every frame and be useless.
        [Fact]
        public void Repainting_without_moving_does_not_raise_LocationChanged ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.LocationChanged += (_, _) => raised++;

                PaintSurface.Render (strip).Dispose ();
                PaintSurface.Render (strip).Dispose ();

                Assert.Equal (0, raised);
            }
        }

        [Fact]
        public void A_resize_that_keeps_the_top_left_is_not_a_move ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.LocationChanged += (_, _) => raised++;

                var b = item.Bounds;
                item.SetBounds (b.X, b.Y, b.Width + 20, b.Height);

                Assert.Equal (0, raised);

                item.SetBounds (b.X + 5, b.Y, b.Width, b.Height);

                Assert.Equal (1, raised);
            }
        }

        // ---------------- SelectedChanged and MouseMove, through the strip's own mouse path

        [Fact]
        public void Hovering_an_item_raises_SelectedChanged_both_ways ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                var states = string.Empty;
                item.SelectedChanged += (_, _) => states += item.Selected ? "S" : "s";

                var b = item.Bounds;
                strip.MoveTo (new Point (b.Left + b.Width / 2, b.Top + b.Height / 2));

                Assert.Equal ("S", states);

                strip.PointerLeaves ();

                Assert.Equal ("Ss", states);
            }
        }

        [Fact]
        public void Moving_over_an_item_raises_its_MouseMove_in_item_coordinates ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                // The item must NOT sit at the strip's origin, or item-relative and strip-relative
                // coordinates coincide and the assertion below cannot tell them apart -- the first
                // version of this test passed with the subtraction removed for exactly that reason.
                // The grip band pushes the item right (TSM-43).
                strip.GripVisible = true;
                strip.PerformLayout ();
                PaintSurface.Render (strip).Dispose ();

                var b = item.Bounds;
                Assert.True (b.Left > 0, "fixture: the item is still at the origin");

                Point? seen = null;
                item.MouseMove += (_, e) => seen = e.Location;

                strip.MoveTo (new Point (b.Left + 7, b.Top + 5));

                Assert.NotNull (seen);

                // Item-relative, as upstream's per-item mouse events are.
                Assert.Equal (new Point (7, 5), seen!.Value);
            }
        }

        [Fact]
        public void Moving_over_empty_strip_raises_no_item_MouseMove ()
        {
            var strip = Strip (out var form, out var item);

            using (form) {
                var raised = 0;
                item.MouseMove += (_, _) => raised++;

                strip.MoveTo (new Point (strip.Width - 2, strip.Height / 2));

                Assert.Equal (0, raised);
            }
        }

        // ---------------- OwnerChanged

        [Fact]
        public void Adding_and_removing_from_a_strip_raises_OwnerChanged ()
        {
            HeadlessRenderer.Use ();

            var strip = new ToolStrip ();
            var item = new ToolStripButton { Text = "Open" };
            var raised = 0;
            item.OwnerChanged += (_, _) => raised++;

            strip.Items.Add (item);

            Assert.Equal (1, raised);
            Assert.Same (strip, item.Owner);

            strip.Items.Remove (item);

            Assert.Equal (2, raised);
            Assert.Null (item.Owner);
        }
    }
}
