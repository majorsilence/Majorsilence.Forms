using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-59: per-item tool tips. Thirteen properties across eight controls were stored and read by
    // nothing, all waiting on the same missing thing -- ToolTip.SetToolTip associates text with a whole
    // CONTROL and shows it on MouseEnter, and nothing mapped a hover over a sub-element (a cell, an
    // item, a node, a tab, a strip button) to a tip.
    //
    // One seam does it: Control.GetToolTipText (Point), driven from the existing mouse-move path. Each
    // control's override is then the five lines that read its own two properties.
    [Collection ("Headless")]
    public class ItemToolTipTests
    {
        // ---------------- ListView

        private static ListView List (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 260, Height = 140, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 180 });
            view.Items.Add (new ListViewItem ("one") { ToolTipText = "the first one" });
            view.Items.Add (new ListViewItem ("two"));

            form = new Form { Width = 360, Height = 240 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        private static Point Centre (ListViewItem item)
        {
            var bounds = item.Bounds;

            return new Point (bounds.Left + 20, bounds.Top + bounds.Height / 2);
        }

        [Fact]
        public void A_ListView_item_offers_its_tip ()
        {
            using var view = List (out var form);

            try {
                view.ShowItemToolTips = true;

                Assert.Equal ("the first one", view.GetToolTipText (Centre (view.Items[0])));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ShowItemToolTips_off_offers_nothing ()
        {
            // The half only a read property can do: the text is still there, the list just does not
            // advertise it. Default is off, so no existing list starts showing tips.
            using var view = List (out var form);

            try {
                Assert.False (view.ShowItemToolTips);
                Assert.Null (view.GetToolTipText (Centre (view.Items[0])));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_item_with_no_tip_offers_nothing ()
        {
            // GUARD: the tip is per ITEM, so moving between items has to change the answer -- a
            // control-level tip could not express this at all, which is the whole reason for the seam.
            using var view = List (out var form);

            try {
                view.ShowItemToolTips = true;

                Assert.Equal ("the first one", view.GetToolTipText (Centre (view.Items[0])));
                Assert.True (string.IsNullOrEmpty (view.GetToolTipText (Centre (view.Items[1]))));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Empty_space_offers_nothing ()
        {
            using var view = List (out var form);

            try {
                view.ShowItemToolTips = true;

                Assert.Null (view.GetToolTipText (new Point (10, view.Height - 4)));
            } finally {
                form.Close ();
            }
        }

        // ---------------- TreeView

        [Fact]
        public void A_TreeView_node_offers_its_tip ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 340, Height = 240 };
            var tree = new TreeView { Width = 240, Height = 140, ShowNodeToolTips = true };
            var node = tree.Items.Add ("node");
            node.ToolTipText = "about the node";
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            try {
                // TreeNode.Bounds is in DEVICE pixels while GetToolTipText -- like GetItemAtLocation
                // beneath it -- takes LOGICAL, the space MouseEventArgs arrives in. Building the point
                // straight from Bounds passes at scale 1 and misses the node at MF_HEADLESS_SCALE=2.
                var bounds = node.Bounds;
                var at = new Point (tree.DeviceToLogicalUnits (bounds.Left) + 20,
                    tree.DeviceToLogicalUnits (bounds.Top + bounds.Height / 2));

                Assert.Equal ("about the node", tree.GetToolTipText (at));

                tree.ShowNodeToolTips = false;

                Assert.Null (tree.GetToolTipText (at));
            } finally {
                form.Close ();
            }
        }

        // ---------------- ToolStrip

        [Fact]
        public void A_ToolStrip_item_offers_its_tip ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 200 };
            var strip = new ToolStrip { Width = 300, Height = 30, ShowItemToolTips = true };
            var button = new ToolStripButton { Text = "Save", ToolTipText = "Save the document" };
            strip.Items.Add (button);
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            try {
                var bounds = button.Bounds;
                var at = new Point (bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

                Assert.Equal ("Save the document", strip.GetToolTipText (at));

                strip.ShowItemToolTips = false;

                Assert.Null (strip.GetToolTipText (at));
            } finally {
                form.Close ();
            }
        }

        // ---------------- TabControl

        [Fact]
        public void A_tab_offers_its_pages_tip ()
        {
            // The override lives on the STRIP, because the tabs are its children -- the pointer is
            // never over the TabControl itself when it is over a tab.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var tabs = new TabControl { Width = 300, Height = 200, ShowToolTips = true };
            var page = new TabPage { Text = "One", ToolTipText = "the first page" };
            tabs.TabPages.Add (page);
            tabs.TabPages.Add (new TabPage { Text = "Two" });
            form.Controls.Add (tabs);
            form.Show ();
            PaintSurface.Render (tabs).Dispose ();

            try {
                var tab = page.TabStripItem.Bounds;
                var at = new Point (tab.Left + tab.Width / 2, tab.Top + tab.Height / 2);

                Assert.Equal ("the first page", tabs.TabStrip.GetToolTipText (at));

                tabs.ShowToolTips = false;

                Assert.Null (tabs.TabStrip.GetToolTipText (at));
            } finally {
                form.Close ();
            }
        }

        // ---------------- the default

        [Fact]
        public void A_control_with_no_sub_elements_offers_nothing ()
        {
            // GUARD: the seam is on Control, so every control in the framework now has it. The default
            // must be null, or the mouse-move path would start showing tips on controls that have none.
            using var panel = new Panel { Width = 100, Height = 100 };

            Assert.Null (panel.GetToolTipText (new Point (10, 10)));
        }
    }
}
