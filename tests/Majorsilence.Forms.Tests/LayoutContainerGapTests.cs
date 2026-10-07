using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behaviour-gap layout findings closed together (issue #346): SplitContainer (LAY-09, LAY-10,
    // LAY-11), TabControl (LAY-18, LAY-20), TableLayout RTL (LAY-23), ScrollableControl (LAY-31,
    // LAY-33), LayoutEventArgs (LAY-36) and TreeView.HitTest (LAY-39). Each test names the upstream
    // behaviour it pins; assertions are relational so they hold at any font size and display scale.
    [Collection ("Headless")]
    public sealed class LayoutContainerGapTests : IDisposable
    {
        private readonly double original_scale = Application.UiScale;

        public LayoutContainerGapTests () => HeadlessRenderer.Use ();

        public void Dispose () => Application.UiScale = original_scale;

        // ---- LAY-09: SplitContainer.AutoScroll -------------------------------------------------

        [Fact]
        public void SplitContainer_AutoScroll_always_reads_false ()
        {
            // Upstream: "Always return false ... as Splitcontainer doesn't support AutoScroll".
            using var split = new SplitContainer ();

            split.AutoScroll = true;

            Assert.False (split.AutoScroll);
            // The panels are what scroll, and they keep their own flag.
            split.Panel1.AutoScroll = true;
            Assert.True (split.Panel1.AutoScroll);
        }

        // ---- LAY-10: Orientation re-validates the split ----------------------------------------

        [Fact]
        public void Flipping_Orientation_reclamps_a_split_that_no_longer_fits ()
        {
            // The finding's own case: 300px across a 400-wide container is fine, but flipped into a
            // 100-tall one it would leave Panel2 nothing.
            using var split = new SplitContainer { Size = new Size (400, 100) };
            split.SplitterDistance = 300;
            Assert.Equal (300, split.SplitterDistance);

            split.Orientation = Orientation.Horizontal;

            Assert.True (split.SplitterDistance <= split.ClientSize.Height - split.SplitterWidth - split.Panel2MinSize,
                $"distance {split.SplitterDistance} leaves Panel2 less than its minimum in a {split.ClientSize.Height}px-tall container");
            Assert.True (split.SplitterDistance >= split.Panel1MinSize);
        }

        [Fact]
        public void Flipping_Orientation_raises_SplitterMoved ()
        {
            // Upstream's Orientation setter re-assigns SplitterDistance, which raises SplitterMoved.
            using var split = new SplitContainer { Size = new Size (400, 300) };
            split.SplitterDistance = 120;
            var moved = 0;
            split.SplitterMoved += (_, _) => moved++;

            split.Orientation = Orientation.Horizontal;

            Assert.Equal (1, moved);
            // A split that fits on both axes is carried across unchanged.
            Assert.Equal (120, split.SplitterDistance);
        }

        // ---- LAY-11: SplitterDistance validates and raises ------------------------------------

        [Fact]
        public void A_negative_SplitterDistance_throws ()
        {
            using var split = new SplitContainer { Size = new Size (400, 200) };
            var before = split.SplitterDistance;

            Assert.Throws<ArgumentOutOfRangeException> (() => split.SplitterDistance = -1);
            Assert.Equal (before, split.SplitterDistance);
        }

        [Fact]
        public void Assigning_SplitterDistance_raises_SplitterMoved_once_per_change ()
        {
            using var split = new SplitContainer { Size = new Size (400, 200) };
            var moved = new List<SplitterEventArgs> ();
            split.SplitterMoved += (_, e) => moved.Add (e);

            split.SplitterDistance = 150;

            var only = Assert.Single (moved);
            // The args describe where the bar now is.
            Assert.Equal (split.SplitterRectangle.X, only.SplitX);

            // Re-assigning the current distance is not a move.
            split.SplitterDistance = 150;
            Assert.Single (moved);
        }

        // ---- LAY-18 / LAY-20: TabControl ------------------------------------------------------

        private static (TabControl Control, TabPage First, TabPage Second) Tabs ()
        {
            var control = new TabControl { Width = 400, Height = 300 };
            var first = new TabPage ("First");
            var second = new TabPage ("Second");
            control.TabPages.Add (first);
            control.TabPages.Add (second);
            control.PerformLayout ();
            control.TabStrip.PerformLayout ();
            control.PerformLayout ();
            return (control, first, second);
        }

        [Fact]
        public void GetTabRect_throws_for_an_index_out_of_range ()
        {
            var (control, _, _) = Tabs ();
            using var _control = control;

            Assert.Throws<ArgumentOutOfRangeException> (() => control.GetTabRect (2));
            Assert.Throws<ArgumentOutOfRangeException> (() => control.GetTabRect (-1));
            Assert.False (control.GetTabRect (1).IsEmpty);
        }

        [Fact]
        public void HitTest_finds_the_page_whose_tab_header_is_under_the_point ()
        {
            var (control, first, second) = Tabs ();
            using var _control = control;

            var header = control.GetTabRect (1);
            Assert.Same (second, control.HitTest (new Point (header.X + header.Width / 2, header.Y + header.Height / 2)));

            header = control.GetTabRect (0);
            Assert.Same (first, control.HitTest (new Point (header.X + 2, header.Y + 2)));
        }

        [Fact]
        public void HitTest_misses_in_the_page_body ()
        {
            // Every body point used to "hit" whichever page came first.
            var (control, _, second) = Tabs ();
            using var _control = control;
            var body = second.Bounds;

            Assert.Null (control.HitTest (new Point (body.X + body.Width / 2, body.Y + body.Height / 2)));
        }

        // ---- LAY-23: TableLayoutPanel RightToLeft --------------------------------------------

        private static (TableLayoutPanel Table, Control First, Control Second) TwoColumns ()
        {
            var table = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Size = new Size (200, 50) };
            table.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));
            table.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));
            var first = new Panel { Size = new Size (20, 20) };
            var second = new Panel { Size = new Size (20, 20) };
            table.Controls.Add (first, 0, 0);
            table.Controls.Add (second, 1, 0);
            table.PerformLayout ();
            return (table, first, second);
        }

        [Fact]
        public void A_right_to_left_table_puts_column_zero_on_the_right ()
        {
            var (table, first, second) = TwoColumns ();
            using var _table = table;
            Assert.True (first.Left < second.Left, "baseline: left to right");

            table.RightToLeft = RightToLeft.Yes;

            Assert.True (first.Left > second.Left, $"column 0 at {first.Left} should sit right of column 1 at {second.Left}");
        }

        [Fact]
        public void A_table_inheriting_right_to_left_mirrors_too ()
        {
            var (table, first, second) = TwoColumns ();
            using var parent = new Panel { Size = new Size (300, 100) };
            parent.Controls.Add (table);

            parent.RightToLeft = RightToLeft.Yes;

            Assert.True (first.Left > second.Left);
        }

        // ---- LAY-31: DockPadding --------------------------------------------------------------

        [Fact]
        public void DockPadding_is_the_controls_Padding ()
        {
            using var panel = new Panel { Size = new Size (200, 100) };

            panel.DockPadding.All = 8;

            Assert.Equal (new Padding (8), panel.Padding);
            Assert.Equal (panel.ClientRectangle.Width - 16, panel.DisplayRectangle.Width);

            panel.DockPadding.Left = 2;
            Assert.Equal (2, panel.Padding.Left);
            // Edges that disagree read back as 0 from All, as upstream remaps Padding's -1.
            Assert.Equal (0, panel.DockPadding.All);

            panel.Padding = new Padding (3);
            Assert.Equal (3, panel.DockPadding.Top);
        }

        [Fact]
        public void DockPadding_insets_a_docked_child ()
        {
            using var panel = new Panel { Size = new Size (200, 100) };
            var child = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add (child);

            panel.DockPadding.All = 5;
            panel.PerformLayout ();

            Assert.Equal (panel.DisplayRectangle, child.Bounds);
            Assert.Equal (panel.ClientRectangle.Width - 10, child.Width);
        }

        // ---- LAY-33: AutoScrollPosition takes the positive distance ---------------------------

        private static Panel Scroller (out Form form)
        {
            var panel = new Panel { AutoScroll = true, Size = new Size (200, 150) };
            panel.Controls.Add (new Panel { Location = new Point (0, 0), Size = new Size (150, 1000) });
            form = new Form { Width = 400, Height = 400 };
            form.Controls.Add (panel);
            form.Show ();
            panel.PerformLayout ();
            return panel;
        }

        [Fact]
        public void A_positive_AutoScrollPosition_scrolls_and_the_getter_answers_it_negated ()
        {
            var panel = Scroller (out var form);
            using var _form = form;

            panel.AutoScrollPosition = new Point (0, 100);

            Assert.Equal (-100, panel.AutoScrollPosition.Y);
        }

        [Fact]
        public void A_negative_AutoScrollPosition_clamps_to_the_top ()
        {
            // Upstream negates the value and clamps, so the getter's own value assigned back does
            // not scroll: that is why WinForms code restores with new Point (-saved.X, -saved.Y).
            var panel = Scroller (out var form);
            using var _form = form;
            panel.AutoScrollPosition = new Point (0, 100);
            var saved = panel.AutoScrollPosition;

            panel.AutoScrollPosition = saved;
            Assert.Equal (0, panel.AutoScrollPosition.Y);

            panel.AutoScrollPosition = new Point (-saved.X, -saved.Y);
            Assert.Equal (saved, panel.AutoScrollPosition);
        }

        // ---- LAY-36: LayoutEventArgs ----------------------------------------------------------

        [Fact]
        public void LayoutEventArgs_built_from_a_control_reports_it_as_the_component ()
        {
            using var control = new Button ();

            var args = new LayoutEventArgs (control, "Bounds");

            Assert.Same (control, args.AffectedComponent);
            Assert.Same (control, args.AffectedControl);
        }

        [Fact]
        public void LayoutEventArgs_built_from_a_component_that_is_a_control_reports_the_control ()
        {
            using var control = new Button ();

            var args = new LayoutEventArgs ((IComponent)control, "Bounds");

            Assert.Same (control, args.AffectedControl);
        }

        [Fact]
        public void A_controls_own_layout_reports_the_affected_component ()
        {
            using var panel = new Panel { Size = new Size (100, 100) };
            var child = new Button ();
            LayoutEventArgs? seen = null;
            panel.Layout += (_, e) => seen = e;

            panel.Controls.Add (child);

            Assert.NotNull (seen);
            Assert.Same (child, seen!.AffectedComponent);
        }

        // ---- LAY-39: TreeView.HitTest regions ----------------------------------------------------

        private static List<TreeViewHitTestLocations> RowLocations (TreeView tree, TreeNode node)
        {
            var y = Enumerable.Range (0, tree.Height).First (y => tree.GetNodeAt (5, y) == node);
            // A couple of pixels into the row so no edge rounding is involved.
            y += 2;

            return Enumerable.Range (0, tree.Width)
                .Select (x => tree.HitTest (x, y))
                .Where (hit => hit.Node == node)
                .Select (hit => hit.Location)
                .ToList ();
        }

        [Theory]
        [InlineData (1.0)]
        [InlineData (2.0)]
        public void TreeView_HitTest_reports_the_expander_left_of_the_label (double scale)
        {
            Application.UiScale = scale;
            var tree = new TreeView { Width = 250, Height = 200 };
            var parent = tree.Nodes.Add ("parent");
            var leaf = parent.Nodes.Add ("leaf");
            parent.Expand ();
            using var form = new Form { Width = 400, Height = 300 };
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            var row = RowLocations (tree, parent);
            var first_plus = row.IndexOf (TreeViewHitTestLocations.PlusMinus);
            var first_label = row.IndexOf (TreeViewHitTestLocations.Label);

            Assert.True (first_plus >= 0, "an expandable node's row must have a PlusMinus band");
            Assert.True (first_label > first_plus, "the label sits right of the expander");

            // A leaf has no button: its glyph column is indent, and it is indented further than its parent.
            var leaf_row = RowLocations (tree, leaf);
            Assert.DoesNotContain (TreeViewHitTestLocations.PlusMinus, leaf_row);
            Assert.True (leaf_row.IndexOf (TreeViewHitTestLocations.Label) > first_label);
        }
    }
}
