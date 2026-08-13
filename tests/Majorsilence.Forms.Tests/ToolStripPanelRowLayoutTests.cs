using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ToolStripPanelRowLayoutTests
    {
        [Fact]
        public void Two_strips_on_one_edge_stack_into_rows ()
        {
            // Regression: an edge panel was a plain docked Panel, so a menu and a toolbar added to
            // the SAME edge fought over it -- one claimed the edge and the other collapsed, leaving
            // only a single visible strip. Real WinForms gives each strip its own ToolStripPanelRow.
            // Found via a migrated app whose module windows (TsToolBar : ToolStripContainer) put a
            // MainMenu and a button toolbar in TopToolStripPanel and showed only the toolbar.
            using var container = new ToolStripContainer { Size = new Size (900, 400) };

            var menu = new ToolStrip { Dock = DockStyle.Top, Height = 24 };
            var toolbar = new ToolStrip { Dock = DockStyle.Fill, Height = 90 };

            container.TopToolStripPanel.Controls.Add (menu);
            container.TopToolStripPanel.Controls.Add (toolbar);

            container.PerformLayout ();

            // Both are on screen with real height -- neither collapsed away.
            Assert.True (menu.Height > 0, $"menu collapsed (height {menu.Height})");
            Assert.True (toolbar.Height > 0, $"toolbar collapsed (height {toolbar.Height})");

            // Stacked, not overlapping: the toolbar begins at or below the menu's bottom edge.
            Assert.True (
                toolbar.Top >= menu.Top + menu.Height,
                $"strips overlap: menu {menu.Top}..{menu.Top + menu.Height}, toolbar top {toolbar.Top}");

            // Each row spans the panel width.
            Assert.Equal (container.TopToolStripPanel.ClientRectangle.Width, menu.Width);
            Assert.Equal (container.TopToolStripPanel.ClientRectangle.Width, toolbar.Width);

            // The edge panel grew to hold both rows rather than just the taller one.
            Assert.True (
                container.TopToolStripPanel.Height >= menu.Height + toolbar.Height,
                $"panel {container.TopToolStripPanel.Height} < rows {menu.Height} + {toolbar.Height}");
        }

        [Fact]
        public void Menu_takes_the_top_row_even_when_added_after_the_toolbar ()
        {
            // A ToolStripContainer subclass typically creates its own toolbar in its constructor, so
            // the toolbar is in Controls before the designer adds the menu. Insertion order would
            // then put the menu underneath the buttons; a menu bar belongs on top.
            using var container = new ToolStripContainer { Size = new Size (900, 400) };

            var toolbar = new ToolStrip { Dock = DockStyle.Fill };
            toolbar.Items.Add (new ToolStripButton { Text = "Close", AutoSize = false, Size = new Size (150, 64) });
            var menu = new MenuStrip { Dock = DockStyle.None };
            menu.Items.Add (new ToolStripMenuItem { Text = "&File" });

            container.TopToolStripPanel.Controls.Add (toolbar);   // added FIRST
            container.TopToolStripPanel.Controls.Add (menu);      // added SECOND

            container.PerformLayout ();

            Assert.True (
                menu.Top < toolbar.Top,
                $"menu should sit above the toolbar: menu top {menu.Top}, toolbar top {toolbar.Top}");
            Assert.True (
                toolbar.Top >= menu.Top + menu.Height,
                $"rows overlap: menu {menu.Top}..{menu.Top + menu.Height}, toolbar top {toolbar.Top}");
        }

        [Fact]
        public void Fixed_size_item_keeps_its_size_instead_of_shrinking_to_its_caption ()
        {
            // Regression: ToolStripItem.AutoSize/Size were unconsulted stubs, so the renderer's text
            // measurement always won and a host-assigned button box (image above text) collapsed to
            // caption width. WinForms treats AutoSize=false + explicit Size as a fixed item box.
            var item = new ToolStripButton { Text = "Open File", AutoSize = false, Size = new Size (150, 64) };

            var preferred = item.GetPreferredSize (Size.Empty);

            Assert.Equal (new Size (150, 64), preferred);
        }

        [Fact]
        public void Auto_sized_item_still_measures_itself ()
        {
            // The fixed-size path must not swallow the default: AutoSize items keep measuring.
            var item = new ToolStripButton { Text = "Open File", AutoSize = true, Size = new Size (150, 64) };

            Assert.NotEqual (new Size (150, 64), item.GetPreferredSize (Size.Empty));
        }

        [Fact]
        public void Toolbar_asks_for_enough_height_for_its_tallest_button ()
        {
            // Regression: a strip reported only its explicitly-set bounds, so it stayed at whatever
            // height its container handed it. StackLayoutEngine gives every item the strip's client
            // height, so a short strip squashed tall image-above-text buttons flat.
            using var toolbar = new ToolStrip ();

            toolbar.Items.Add (new ToolStripButton { Text = "Close", AutoSize = false, Size = new Size (150, 64) });
            toolbar.Items.Add (new ToolStripButton { Text = "Save", AutoSize = false, Size = new Size (150, 64) });

            var preferred = toolbar.GetPreferredSize (Size.Empty);

            Assert.True (preferred.Height >= 64, $"strip only asked for {preferred.Height}px, needs >= 64");
            Assert.True (preferred.Width >= 300, $"strip only asked for {preferred.Width}px, needs >= 300");
        }

        [Fact]
        public void Empty_edge_panel_still_collapses ()
        {
            // Row layout must not resurrect the empty-edge-panel bug the container already fixed.
            using var container = new ToolStripContainer { Size = new Size (400, 300) };

            container.PerformLayout ();

            Assert.Equal (0, container.LeftToolStripPanel.Width);
            Assert.Equal (0, container.RightToolStripPanel.Width);
            Assert.Equal (0, container.BottomToolStripPanel.Height);
        }
    }
}
