using System;
using System.Collections.Specialized;
using Xunit;

using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the flat tail of the WinForms surface (docs/winforms-gap-plan.md).
    ///
    /// The members worth guarding are the ones that navigate or compute — <c>TreeNode</c>'s
    /// visible-node walk in particular, which has to stop descending into a collapsed node, and
    /// <c>SplitContainer.SplitterRectangle</c>, which has to follow the orientation.
    /// </summary>
    public class TailParityTests
    {
        [Fact]
        public void A_nodes_visibility_depends_on_its_ancestors_being_expanded ()
        {
            using var tree = new TreeView ();
            var root = tree.Nodes.Add ("root");
            var child = root.Nodes.Add ("child");

            root.Collapse ();
            Assert.False (child.IsVisible);
            Assert.True (root.IsVisible);        // the root itself is always visible

            root.Expand ();
            Assert.True (child.IsVisible);
        }

        [Fact]
        public void The_visible_node_walk_skips_the_children_of_a_collapsed_node ()
        {
            using var tree = new TreeView ();
            var first = tree.Nodes.Add ("first");
            first.Nodes.Add ("hidden");
            var second = tree.Nodes.Add ("second");

            first.Collapse ();

            // "hidden" is not reachable, so the node after "first" is "second".
            Assert.Same (second, first.NextVisibleNode);
            Assert.Same (first, second.PrevVisibleNode);
        }

        [Fact]
        public void The_visible_node_walk_descends_into_an_expanded_node ()
        {
            using var tree = new TreeView ();
            var first = tree.Nodes.Add ("first");
            var child = first.Nodes.Add ("child");
            tree.Nodes.Add ("second");

            first.Expand ();

            Assert.Same (child, first.NextVisibleNode);
        }

        [Fact]
        public void The_first_visible_node_has_nothing_before_it ()
        {
            using var tree = new TreeView ();
            var first = tree.Nodes.Add ("first");
            tree.Nodes.Add ("second");

            Assert.Null (first.PrevVisibleNode);
        }

        [Fact]
        public void ExpandAll_expands_every_descendant ()
        {
            using var tree = new TreeView ();
            var root = tree.Nodes.Add ("root");
            var child = root.Nodes.Add ("child");
            var grandchild = child.Nodes.Add ("grandchild");

            root.ExpandAll ();

            Assert.True (root.IsExpanded);
            Assert.True (child.IsExpanded);
            Assert.True (grandchild.IsVisible);
        }

        [Fact]
        public void Cloning_a_node_copies_its_children_without_sharing_them ()
        {
            var node = new TreeViewItem ("root") { Name = "key" };
            node.Nodes.Add (new TreeViewItem ("child"));

            var clone = (TreeViewItem)node.Clone ();

            Assert.Equal ("root", clone.Text);
            Assert.Equal ("key", clone.Name);
            Assert.Equal (1, clone.Nodes.Count);
            Assert.NotSame (node.Nodes[0], clone.Nodes[0]);
        }

        [Fact]
        public void SplitterRectangle_follows_the_orientation ()
        {
            // This control reads Orientation as the direction of the layout, not of the bar, which is
            // the opposite of WinForms -- see SplitterRectangle's remarks. Horizontal therefore puts
            // the panels side by side and the splitter bar runs vertically.
            using var split = new SplitContainer {
                Size = new Size (200, 100),
                Orientation = Orientation.Horizontal,
                SplitterDistance = 60,
                SplitterWidth = 4,
            };

            var sideBySide = split.SplitterRectangle;
            Assert.Equal (60, sideBySide.X);
            Assert.Equal (4, sideBySide.Width);
            Assert.Equal (100, sideBySide.Height);

            split.Orientation = Orientation.Vertical;

            var stacked = split.SplitterRectangle;
            Assert.Equal (60, stacked.Y);
            Assert.Equal (4, stacked.Height);
            Assert.Equal (200, stacked.Width);
        }

        [Fact]
        public void The_splitter_events_are_raised_by_their_raisers ()
        {
            // Both were declared with empty accessors, so OnSplitterMoved had nothing to raise.
            using var split = new SplitContainer ();
            var moved = 0;
            var moving = 0;
            split.SplitterMoved += (_, _) => moved++;
            split.SplitterMoving += (_, _) => moving++;

            split.OnSplitterMoved (new SplitterEventArgs (0, 0, 0, 0));
            split.OnSplitterMoving (new SplitterCancelEventArgs (0, 0, 0, 0));

            Assert.Equal (1, moved);
            Assert.Equal (1, moving);
        }

        [Fact]
        public void ToolStripPanel_Join_puts_the_strip_on_a_row ()
        {
            using var panel = new ToolStripPanel ();
            using var strip = new ToolStrip ();

            panel.Join (strip);

            Assert.Single (panel.Rows);
            Assert.Contains (strip, panel.Rows[0].Controls);
            Assert.Contains (strip, panel.Controls);
        }

        [Fact]
        public void ToolStripPanel_Join_can_target_a_specific_row ()
        {
            using var panel = new ToolStripPanel ();
            using var first = new ToolStrip ();
            using var second = new ToolStrip ();

            panel.Join (first, 0);
            panel.Join (second, 2);

            Assert.Equal (3, panel.Rows.Length);       // rows are created up to the requested index
            Assert.Contains (second, panel.Rows[2].Controls);
            Assert.Empty (panel.Rows[1].Controls);
        }

        [Fact]
        public void ToolStripPanel_Renderer_notifies_once_per_change ()
        {
            using var panel = new ToolStripPanel ();
            var raised = 0;
            panel.RendererChanged += (_, _) => raised++;

            var renderer = new ToolStripProfessionalRenderer ();
            panel.Renderer = renderer;
            panel.Renderer = renderer;

            Assert.Same (renderer, panel.Renderer);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void IsValidShortcut_requires_a_modifier_unless_it_is_a_function_key ()
        {
            // Without this rule every letter typed into a text box would look like a shortcut.
            Assert.True (ToolStripManager.IsValidShortcut (Keys.Control | Keys.S));
            Assert.True (ToolStripManager.IsValidShortcut (Keys.Alt | Keys.F4));
            Assert.True (ToolStripManager.IsValidShortcut (Keys.F5));

            Assert.False (ToolStripManager.IsValidShortcut (Keys.S));
            Assert.False (ToolStripManager.IsValidShortcut (Keys.Shift | Keys.S));
            Assert.False (ToolStripManager.IsValidShortcut (Keys.None));
        }

        [Fact]
        public void Menu_MergeMenu_copies_the_source_items ()
        {
            using var target = new Menu ();
            using var source = new Menu ();
            source.Items.Add (new MenuItem ("Open"));

            target.MergeMenu (source);

            Assert.Equal (1, target.MenuItems.Count);
            Assert.NotSame (source.Items[0], target.MenuItems[0]);
            Assert.True (target.IsParent);
        }

        [Fact]
        public void Menu_FindMenuItem_matches_by_shortcut ()
        {
            using var menu = new Menu ();
            var save = new MenuItem ("Save") { Shortcut = Shortcut.CtrlS };
            menu.Items.Add (save);

            Assert.Same (save, menu.FindMenuItem (Menu.FindShortcut, new IntPtr ((int)Shortcut.CtrlS)));
            Assert.Null (menu.FindMenuItem (Menu.FindShortcut, new IntPtr ((int)Shortcut.CtrlZ)));

            // A handle search cannot match: menu items have no window handle here.
            Assert.Null (menu.FindMenuItem (Menu.FindHandle, new IntPtr (1)));
        }

        [Fact]
        public void The_button_image_members_are_reachable_through_the_base ()
        {
            // Item 3's rule: the derived controls keep their own property-store-backed
            // implementations, and the base declares them so a ButtonBase-typed caller can reach them.
            var button = new Button ();
            ButtonBase asBase = button;

            asBase.ImageAlign = ContentAlignment.TopLeft;
            asBase.ImageIndex = 3;

            Assert.Equal (ContentAlignment.TopLeft, button.ImageAlign);
            Assert.Equal (3, button.ImageIndex);

            // Setting one image source clears the others, as it does upstream -- so this has to be
            // checked after, not alongside.
            asBase.ImageKey = "save";
            Assert.Equal ("save", button.ImageKey);
        }

        [Fact]
        public void Clipboard_round_trips_a_file_drop_list_within_the_process ()
        {
            var paths = new StringCollection { "/tmp/one.txt" };

            Clipboard.SetFileDropList (paths);

            Assert.True (Clipboard.ContainsFileDropList ());
            Assert.Equal ("/tmp/one.txt", Clipboard.GetFileDropList ()[0]);
        }

        [Fact]
        public void Clipboard_TryGetData_reports_a_miss_rather_than_throwing ()
        {
            Assert.False (Clipboard.TryGetData<string> ("a format nothing wrote", out var missing));
            Assert.Null (missing);
        }

        [Fact]
        public void FolderBrowserDialog_SelectedPaths_reflects_the_chosen_folder ()
        {
            using var dialog = new FolderBrowserDialog ();

            Assert.Empty (dialog.SelectedPaths);

            dialog.SelectedPath = "/tmp";

            Assert.Equal (["/tmp"], dialog.SelectedPaths);
        }

        [Fact]
        public void A_cursor_reports_that_it_has_no_Win32_handle ()
        {
            // The backends set the pointer through their own API rather than an HCURSOR.
            using var cursor = Cursors.Default;

            Assert.Equal (IntPtr.Zero, cursor.Handle);
            Assert.Equal (IntPtr.Zero, cursor.CopyHandle ());
        }

        [Fact]
        public void ScrollProperties_round_trip_on_a_tool_strip ()
        {
            using var strip = new ToolStrip ();

            strip.HorizontalScroll.Value = 25;
            strip.VerticalScroll.Maximum = 400;
            strip.SetAutoScrollMargin (4, 6);

            Assert.Equal (25, strip.HorizontalScroll.Value);
            Assert.Equal (400, strip.VerticalScroll.Maximum);
            Assert.Equal (new Size (4, 6), strip.AutoScrollMargin);
        }
    }
}
