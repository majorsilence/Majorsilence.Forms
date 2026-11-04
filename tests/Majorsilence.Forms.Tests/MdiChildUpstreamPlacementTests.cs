using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In ported mode (Application.SetDefaultFont) an MDI child opens where upstream's MDI client puts it, and is never
    // pulled back inside the client: the client scrolls to reach it instead. Measured on WinForms (.NET 10, 96 DPI,
    // an 800 x 600 parent whose client is 796 x 596): default children cascade 26px a step and start again at the
    // corner after 8 (a third of the client's height in steps, plus one), whatever their size; CenterParent cascades
    // too; CenterScreen centres (98, 98 for a 600 x 400 child) and holds a larger child at the corner; a Manual child
    // at (900, 700), a 1000 x 750 child and a child left past the edges by a parent shrunk to 500 x 400 all stay where
    // they are, at their size, and the client shows both scroll bars; WindowsDefaultBounds opens 614 x 414.
    [Collection ("Headless")]
    public class MdiChildUpstreamPlacementTests
    {
        private static (Form parent, MdiClient client) Container ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            return (parent, parent.MdiClientControl!);
        }

        private static void Ported (Action test)
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
                test ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static MdiChildWindow Frame (Form child) => child.MdiHost!;

        [Fact]
        public void Default_children_cascade_and_restart_after_a_third_of_the_clients_height ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                var step = client.LogicalToDeviceUnits (26);
                var nstagger = client.ViewportHeight / (3 * step);
                Assert.True (nstagger > 1, $"client too short for the test: {client.ViewportHeight}");

                // Children far larger than the client cascade just the same: the client's height alone decides.
                var children = new List<Form> ();
                for (var i = 0; i < nstagger + 3; i++) {
                    var child = new Form { MdiParent = parent, Size = new Size (700, 600) };
                    child.Show ();
                    children.Add (child);
                }

                for (var i = 0; i < children.Count; i++) {
                    var offset = step * (i % (nstagger + 1));
                    Assert.Equal (new Point (offset, offset), Frame (children[i]).Location);
                    Assert.Equal (new Size (700, 600), children[i].Size);
                }
            });
        }

        [Fact]
        public void CenterParent_cascades_and_CenterScreen_centres_without_clamping ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                var step = client.LogicalToDeviceUnits (26);

                var first = new Form { MdiParent = parent, Size = new Size (300, 200), StartPosition = FormStartPosition.CenterParent };
                first.Show ();
                var second = new Form { MdiParent = parent, Size = new Size (300, 200), StartPosition = FormStartPosition.CenterParent };
                second.Show ();
                Assert.Equal (new Point (0, 0), Frame (first).Location);
                Assert.Equal (new Point (step, step), Frame (second).Location);

                var centred = new Form { MdiParent = parent, Size = new Size (300, 200), StartPosition = FormStartPosition.CenterScreen };
                centred.Show ();
                var frame = Frame (centred);
                Assert.Equal (new Point ((client.ViewportWidth - frame.Width) / 2, (client.ViewportHeight - frame.Height) / 2), frame.Location);

                var big = new Form { MdiParent = parent, Size = new Size (2000, 1500), StartPosition = FormStartPosition.CenterScreen };
                big.Show ();
                Assert.Equal (new Point (0, 0), Frame (big).Location);
                Assert.Equal (new Size (2000, 1500), big.Size);
            });
        }

        [Fact]
        public void A_manual_child_past_the_client_keeps_its_location_and_the_client_scrolls ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                Assert.False (client.HorizontalScroll.Visible);

                var child = new Form {
                    MdiParent = parent, Size = new Size (300, 200),
                    StartPosition = FormStartPosition.Manual, Location = new Point (850, 650),
                };
                child.Show ();

                Assert.Equal (new Point (850, 650), Frame (child).Location);
                Assert.Equal (new Size (300, 200), child.Size);
                Assert.True (client.HorizontalScroll.Visible);
                Assert.True (client.VerticalScroll.Visible);

                // The far corner can be scrolled into view.
                client.AutoScrollPosition = new Point (10000, 10000);
                Assert.Equal (client.ViewportWidth, Frame (child).Right);
                Assert.Equal (client.ViewportHeight, Frame (child).Bottom);
            });
        }

        [Fact]
        public void A_child_larger_than_the_client_keeps_its_size ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                var child = new Form { MdiParent = parent, Size = new Size (1200, 900) };
                child.Show ();

                Assert.Equal (new Size (1200, 900), child.Size);
                Assert.Equal (new Point (0, 0), Frame (child).Location);
                Assert.True (client.HorizontalScroll.Visible);
                Assert.True (client.VerticalScroll.Visible);
            });
        }

        [Fact]
        public void Shrinking_the_parent_leaves_a_child_where_it_is ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                var child = new Form {
                    MdiParent = parent, Size = new Size (300, 200),
                    StartPosition = FormStartPosition.Manual, Location = new Point (400, 300),
                };
                child.Show ();
                Assert.False (client.HorizontalScroll.Visible);

                HeadlessRenderer.CapturePng (parent, 500, 400);

                Assert.Equal (new Rectangle (400, 300, 300, 200), Frame (child).Bounds);
                Assert.True (client.HorizontalScroll.Visible);
                Assert.True (client.VerticalScroll.Visible);

                // Code moving it further out is obeyed too.
                child.Location = new Point (1000, 900);
                Assert.Equal (new Point (1000, 900), Frame (child).Location);
            });
        }

        [Fact]
        public void WindowsDefaultBounds_takes_the_size_the_cascade_leaves ()
        {
            Ported (() => {
                var (parent, client) = Container ();
                var step = client.LogicalToDeviceUnits (26);
                var nstagger = client.ViewportHeight / (3 * step);

                var child = new Form { MdiParent = parent, Size = new Size (1000, 750), StartPosition = FormStartPosition.WindowsDefaultBounds };
                child.Show ();

                Assert.Equal (new Rectangle (0, 0, client.ViewportWidth - nstagger * step, client.ViewportHeight - nstagger * step), Frame (child).Bounds);
            });
        }

        // An app that never chose a font keeps the old placement: the child pulled inside the client, no scroll bars.
        [Fact]
        public void Without_a_chosen_font_a_child_is_kept_inside_the_client ()
        {
            var (parent, client) = Container ();
            var child = new Form {
                MdiParent = parent, Size = new Size (300, 200),
                StartPosition = FormStartPosition.Manual, Location = new Point (850, 650),
            };
            child.Show ();

            var frame = Frame (child);
            Assert.True (frame.Right <= client.DisplayRectangle.Width, $"right {frame.Right}");
            Assert.True (frame.Bottom <= client.DisplayRectangle.Height, $"bottom {frame.Bottom}");
            Assert.False (client.HorizontalScroll.Visible);
            Assert.False (client.VerticalScroll.Visible);
        }
    }
}
