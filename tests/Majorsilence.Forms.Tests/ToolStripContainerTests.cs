using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ToolStripContainerTests
    {
        [Fact]
        public void Sub_panels_are_parented ()
        {
            // Regression: the sub-panels were constructed but never added to the container's own
            // Controls collection, so they never took part in layout or rendering at all -- found
            // via a real migrated app (TsToolBar : ToolStripContainer, hosting a menu + toolbar)
            // rendering as a completely blank strip where the menu/toolbar should be.
            using var container = new ToolStripContainer ();

            Assert.Contains (container.ContentPanel, container.Controls);
            Assert.Contains (container.TopToolStripPanel, container.Controls);
            Assert.Contains (container.BottomToolStripPanel, container.Controls);
            Assert.Contains (container.LeftToolStripPanel, container.Controls);
            Assert.Contains (container.RightToolStripPanel, container.Controls);
        }

        [Fact]
        public void Empty_edge_panels_collapse_and_content_fills ()
        {
            // The real-world shape: TsToolBar docks a menu/toolbar into the TOP panel over a Fill
            // content area; Left/Right/Bottom are unused. Empty edge panels must collapse to zero
            // (they AutoSize to their -- absent -- content) so they don't steal space. The original
            // bug left them at the Panel default 200x100, squeezing content and forcing a negative
            // height on the opposite edge.
            using var container = new ToolStripContainer { Size = new Size (400, 300) };

            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            container.TopToolStripPanel.Controls.Add (top);

            container.PerformLayout ();

            // Unused edges took no width/height.
            Assert.Equal (0, container.LeftToolStripPanel.Width);
            Assert.Equal (0, container.RightToolStripPanel.Width);
            Assert.Equal (0, container.BottomToolStripPanel.Height);

            // Top panel sits at the top, full width, sized to fit its 40px child (>= 40; AutoSize
            // also includes the child's margin, so the exact value isn't pinned).
            Assert.Equal (0, container.TopToolStripPanel.Top);
            Assert.Equal (400, container.TopToolStripPanel.Width);
            Assert.True (container.TopToolStripPanel.Height >= 40);

            // Content fills the remainder with no overlap and no negative dimensions -- the invariant
            // the bug violated (it left content mis-sized and the bottom edge at a negative height).
            var topHeight = container.TopToolStripPanel.Height;
            Assert.Equal (topHeight, container.ContentPanel.Top);
            Assert.Equal (400, container.ContentPanel.Width);
            Assert.Equal (300 - topHeight, container.ContentPanel.Height);
            Assert.True (container.ContentPanel.Height > 0);
        }

        [Fact]
        public void Hiding_a_panel_reflects_in_its_Visible_property ()
        {
            using var container = new ToolStripContainer ();

            container.TopToolStripPanelVisible = false;

            Assert.False (container.TopToolStripPanel.Visible);
            Assert.False (container.TopToolStripPanelVisible);
        }
    }
}
