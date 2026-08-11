using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class MdiChildInitialPlacementTests
    {
        private static (Form parent, MdiClient client) Container ()
        {
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            return (parent, parent.MdiClientControl!);
        }

        [Fact]
        public void First_child_opens_centred_in_the_client_area ()
        {
            // Opening hard against the top-left corner butted the child's caption into the container's
            // own chrome, making it hard to see where the child window began.
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();

            var frame = client.Controls.OfType<MdiChildWindow> ().Single ();
            var area = client.DisplayRectangle;

            var expectedX = (area.Width - frame.Width) / 2;
            var expectedY = (area.Height - frame.Height) / 2;

            // Exact centre for the first child (no cascade offset yet).
            Assert.Equal (expectedX, frame.Left);
            Assert.Equal (expectedY, frame.Top);
        }

        [Fact]
        public void Successive_children_cascade_off_centre_without_leaving_the_client_area ()
        {
            var (parent, client) = Container ();

            for (var i = 0; i < 4; i++)
                new Form { MdiParent = parent, ClientSize = new Size (300, 200) }.Show ();

            var frames = client.Controls.OfType<MdiChildWindow> ().ToList ();
            var area = client.DisplayRectangle;

            Assert.Equal (4, frames.Count);

            // Distinct positions, so a stack stays individually reachable. Compared as a set rather
            // than by index: Controls order tracks z-order, not creation order.
            var origins = frames.Select (f => (f.Left, f.Top)).ToList ();
            Assert.Equal (origins.Count, origins.Distinct ().Count ());

            // ...and none pushed outside the client area.
            foreach (var f in frames) {
                Assert.True (f.Left >= 0 && f.Top >= 0, $"frame at {f.Left},{f.Top} is off the top/left");
                Assert.True (f.Left + f.Width <= area.Width, $"frame right edge {f.Left + f.Width} exceeds {area.Width}");
                Assert.True (f.Top + f.Height <= area.Height, $"frame bottom edge {f.Top + f.Height} exceeds {area.Height}");
            }
        }
    }
}
