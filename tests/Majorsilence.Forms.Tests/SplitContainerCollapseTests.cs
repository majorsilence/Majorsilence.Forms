using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Collapsing Panel1 hid only the panel: the 5px splitter stayed docked at the left edge, so Panel2 --
    // meant to take the whole container -- started 5px in, with the bar drawn beside it. Found by
    // Recital's reader, whose page sat beside a stray bar whenever its side panel was closed
    // (FRAMEWORK_GAPS GAP-010).
    [Collection ("Headless")]
    public class SplitContainerCollapseTests
    {
        [Fact]
        public void Collapsing_Panel1_gives_Panel2_the_whole_container ()
        {
            HeadlessRenderer.Use ();

            var split = new SplitContainer { Dock = DockStyle.Fill, Panel1Collapsed = true };
            var form = new Form { Width = 500, Height = 300 };
            form.Controls.Add (split);
            form.Show ();

            try {
                HeadlessRenderer.CapturePng (form, 500, 300);

                Assert.Equal (new Rectangle (Point.Empty, split.ClientSize), split.Panel2.Bounds);

                split.Panel1Collapsed = false;
                HeadlessRenderer.CapturePng (form, 500, 300);
                Assert.True (split.Panel2.Left >= split.SplitterDistance + split.SplitterWidth);
            } finally {
                form.Close ();
            }
        }
    }
}
