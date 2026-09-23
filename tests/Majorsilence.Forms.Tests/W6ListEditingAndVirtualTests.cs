using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, fifth chunk: in-place label editing on ListView and TreeView, ListView column
/// reordering through DisplayIndex, ItemDrag on both lists, the divider cursor, and ListView virtual mode.
/// </summary>
public class W6ListEditingAndVirtualTests
{
    private static ListView List (bool labelEdit = true)
    {
        HeadlessRenderer.Use ();
        var list = new ListView { Size = new Size (400, 200), View = View.Details, LabelEdit = labelEdit };
        list.Columns.Add ("A", 100);
        list.Columns.Add ("B", 100);
        list.Items.Add ("one");
        list.Items.Add ("two");
        return list;
    }

    private static MouseEventArgs Left (int x, int y, int clicks = 1) => new (MouseButtons.Left, clicks, x, y, 0);

    // ── label editing: ListView ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void BeginEdit_opens_an_editor_over_the_label_and_Enter_commits_through_AfterLabelEdit ()
    {
        using var list = List ();
        var before = new List<int> ();
        var after = new List<(int item, string? label)> ();
        list.BeforeLabelEdit += (_, e) => before.Add (e.Item);
        list.AfterLabelEdit += (_, e) => after.Add ((e.Item, e.Label));

        list.Items[1].BeginEdit ();

        var editor = list.LabelEditor;
        Assert.NotNull (editor);
        Assert.Equal ([1], before);
        Assert.Equal ("two", editor!.Text);
        Assert.Equal (list.Items[1].Bounds.Top, editor.Top);
        Assert.Same (list.Items[1], list.EditingItem);

        editor.Text = "renamed";
        editor.RaiseKeyDown (new KeyEventArgs (Keys.Return));

        Assert.Equal ([(1, "renamed")], after);
        Assert.Equal ("renamed", list.Items[1].Text);
        Assert.Null (list.LabelEditor);
    }

    [Fact]
    public void Escape_cancels_with_a_null_label_and_a_refused_edit_keeps_the_text ()
    {
        using var list = List ();
        var after = new List<string?> ();
        list.AfterLabelEdit += (_, e) => { after.Add (e.Label); e.CancelEdit = e.Label == "refused"; };

        list.Items[0].BeginEdit ();
        list.LabelEditor!.Text = "dropped";
        list.LabelEditor.RaiseKeyDown (new KeyEventArgs (Keys.Escape));
        Assert.Equal ("one", list.Items[0].Text);

        list.Items[0].BeginEdit ();
        list.LabelEditor!.Text = "refused";
        list.LabelEditor.RaiseKeyDown (new KeyEventArgs (Keys.Return));
        Assert.Equal ("one", list.Items[0].Text);

        Assert.Equal ([null, "refused"], after);
    }

    [Fact]
    public void BeforeLabelEdit_can_refuse_and_LabelEdit_false_throws_as_upstream ()
    {
        using var list = List ();
        list.BeforeLabelEdit += (_, e) => e.CancelEdit = true;

        list.Items[0].BeginEdit ();
        Assert.Null (list.LabelEditor);

        list.LabelEdit = false;
        Assert.Throws<InvalidOperationException> (list.Items[0].BeginEdit);
    }

    [Fact]
    public void F2_edits_the_focused_item ()
    {
        using var list = List ();
        list.FocusedItem = list.Items[1];

        list.RaiseKeyDown (new KeyEventArgs (Keys.F2));

        Assert.Same (list.Items[1], list.EditingItem);
    }

    // ── label editing: TreeView ─────────────────────────────────────────────────────────────────────

    private static TreeView Tree ()
    {
        HeadlessRenderer.Use ();
        var tree = new TreeView { Size = new Size (300, 200), LabelEdit = true };
        tree.Nodes.Add ("root");
        tree.Nodes.Add ("second");
        return tree;
    }

    [Fact]
    public void A_node_edit_reports_through_the_node_events_and_EndEdit_can_cancel ()
    {
        using var tree = Tree ();
        var before = new List<TreeNode> ();
        var after = new List<(TreeNode node, string? label)> ();
        tree.BeforeLabelEdit += (_, e) => before.Add (e.Node);
        tree.AfterLabelEdit += (_, e) => after.Add ((e.Node, e.Label));
        var node = tree.Nodes[1];

        node.BeginEdit ();
        Assert.True (node.IsEditing);
        Assert.Equal ([node], before);
        Assert.Equal ("second", tree.LabelEditor!.Text);

        tree.LabelEditor.Text = "changed";
        node.EndEdit (cancel: true);
        Assert.False (node.IsEditing);
        Assert.Equal ("second", node.Text);

        node.BeginEdit ();
        tree.LabelEditor!.Text = "changed";
        node.EndEdit (cancel: false);
        Assert.Equal ("changed", node.Text);

        Assert.Equal ([(node, null), (node, "changed")], after);

        tree.LabelEdit = false;
        Assert.Throws<InvalidOperationException> (node.BeginEdit);
    }

    [Fact]
    public void F2_edits_the_selected_node ()
    {
        using var tree = Tree ();
        tree.SelectedNode = tree.Nodes[0];

        tree.RaiseKeyDown (new KeyEventArgs (Keys.F2));

        Assert.Same (tree.Nodes[0], tree.EditingNode);
    }

    // ── column reorder ──────────────────────────────────────────────────────────────────────────────

    private static Point HeaderCentre (ListView list, int displaySlot)
    {
        var x = list.ItemArea.Left + list.ScaledCheckWidth;
        for (var i = 0; i < displaySlot; i++)
            x += list.ScaledColumnWidth (list.DisplayColumns[i]);
        x += list.ScaledColumnWidth (list.DisplayColumns[displaySlot]) / 2;
        return new Point (list.DeviceToLogicalUnits (x), list.DeviceToLogicalUnits (list.ScaledHeaderHeight) / 2);
    }

    [Fact]
    public void Dragging_a_header_past_its_neighbour_asks_ColumnReordered_and_moves_the_DisplayIndex ()
    {
        using var list = List ();
        list.AllowColumnReorder = true;
        using var _ = PaintSurface.Render (list);
        var reordered = new List<(int old, int @new, ColumnHeader header)> ();
        var clicks = 0;
        list.ColumnReordered += (_, e) => reordered.Add ((e.OldDisplayIndex, e.NewDisplayIndex, e.Header));
        list.ColumnClick += (_, _) => clicks++;

        var from = HeaderCentre (list, 0);
        var to = HeaderCentre (list, 1);
        list.RaiseMouseDown (Left (from.X, from.Y));
        list.RaiseMouseMove (Left (to.X, to.Y));
        list.RaiseMouseUp (Left (to.X, to.Y));
        list.RaiseClick (Left (to.X, to.Y));

        Assert.Equal ([(0, 1, list.Columns[0])], reordered);
        Assert.Equal (["B", "A"], list.DisplayColumns.Select (c => c.Text));
        Assert.Equal (1, list.Columns[0].DisplayIndex);
        Assert.Equal (0, list.Columns[1].DisplayIndex);
        Assert.Equal (0, clicks);   // the release that ends a drag is not a header click

        // The cells follow their header: column 0's cell now sits in the second slot.
        list.LayoutItems ();
        var first_slot_x = list.ItemArea.Left + list.ScaledCheckWidth + 5;
        Assert.Equal (1, list.ColumnIndexAt (first_slot_x));
        Assert.Equal (list.Items[0].SubItems[0].Bounds.Left, list.DeviceToLogicalUnits (first_slot_x - 5 + list.ScaledColumnWidth (list.Columns[1])));
    }

    [Fact]
    public void A_cancelled_reorder_or_AllowColumnReorder_off_leaves_the_order_alone_and_DisplayIndex_moves_programmatically ()
    {
        using var list = List ();
        using var _ = PaintSurface.Render (list);
        var asked = 0;
        list.ColumnReordered += (_, e) => { asked++; e.Cancel = true; };

        var from = HeaderCentre (list, 0);
        var to = HeaderCentre (list, 1);
        list.RaiseMouseDown (Left (from.X, from.Y));
        list.RaiseMouseMove (Left (to.X, to.Y));
        list.RaiseMouseUp (Left (to.X, to.Y));
        Assert.Equal (0, asked);   // reordering is off

        list.AllowColumnReorder = true;
        list.RaiseMouseDown (Left (from.X, from.Y));
        list.RaiseMouseMove (Left (to.X, to.Y));
        list.RaiseMouseUp (Left (to.X, to.Y));
        Assert.Equal (1, asked);
        Assert.Equal (["A", "B"], list.DisplayColumns.Select (c => c.Text));

        list.Columns[1].DisplayIndex = 0;
        Assert.Equal (["B", "A"], list.DisplayColumns.Select (c => c.Text));
        Assert.Equal ([1, 0], list.Columns.Select (c => c.DisplayIndex));
    }

    // ── ItemDrag ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Moving_past_the_drag_threshold_with_a_button_held_raises_ItemDrag_once_per_press ()
    {
        using var list = List ();
        using var _ = PaintSurface.Render (list);
        var dragged = new List<(MouseButtons button, object? item)> ();
        list.ItemDrag += (_, e) => dragged.Add ((e.Button, e.Item));

        var b = list.Items[1].Bounds;
        var start = new Point (b.Left + 10, b.Top + b.Height / 2);
        list.RaiseMouseDown (Left (start.X, start.Y));
        list.RaiseMouseMove (Left (start.X + 2, start.Y));      // inside the threshold
        Assert.Empty (dragged);

        list.RaiseMouseMove (Left (start.X + 12, start.Y));
        list.RaiseMouseMove (Left (start.X + 30, start.Y));
        Assert.Equal ([(MouseButtons.Left, (object)list.Items[1])], dragged);

        list.RaiseMouseUp (Left (start.X + 30, start.Y));
        list.RaiseMouseMove (new MouseEventArgs (MouseButtons.None, 0, start.X + 60, start.Y, 0));
        Assert.Single (dragged);
    }

    [Fact]
    public void A_TreeView_raises_ItemDrag_for_the_pressed_node ()
    {
        using var tree = Tree ();
        using var _ = PaintSurface.Render (tree);
        var dragged = new List<object?> ();
        tree.ItemDrag += (_, e) => dragged.Add (e.Item);

        var b = tree.Nodes[1].Bounds;
        var start = new Point (tree.DeviceToLogicalUnits (b.Left + b.Width / 2), tree.DeviceToLogicalUnits (b.Top + b.Height / 2));
        tree.RaiseMouseDown (Left (start.X, start.Y));
        tree.RaiseMouseMove (Left (start.X + 12, start.Y));
        tree.RaiseMouseMove (Left (start.X + 24, start.Y));

        Assert.Equal ([tree.Nodes[1]], dragged);
    }

    // ── divider cursor ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_pointer_over_a_header_divider_shows_the_west_east_cursor ()
    {
        using var list = List ();
        using var _ = PaintSurface.Render (list);
        var edge = list.ItemArea.Left + list.ScaledCheckWidth + list.ScaledColumnWidth (list.Columns[0]);
        var y = list.DeviceToLogicalUnits (list.ScaledHeaderHeight) / 2;

        list.RaiseMouseMove (new MouseEventArgs (MouseButtons.None, 0, list.DeviceToLogicalUnits (edge), y, 0));
        Assert.Same (Cursors.VSplit, list.Cursor);

        list.RaiseMouseMove (new MouseEventArgs (MouseButtons.None, 0, list.DeviceToLogicalUnits (edge) - 30, y, 0));
        Assert.Same (Cursors.Default, list.Cursor);
    }

    // ── virtual mode ────────────────────────────────────────────────────────────────────────────────

    private static ListView VirtualList (List<int> retrieved)
    {
        HeadlessRenderer.Use ();
        var list = new ListView { Size = new Size (400, 200), View = View.Details, VirtualMode = true };
        list.Columns.Add ("A", 100);
        list.RetrieveVirtualItem += (_, e) => { retrieved.Add (e.ItemIndex); e.Item = new ListViewItem ($"v{e.ItemIndex}"); };
        list.VirtualListSize = 5;
        return list;
    }

    [Fact]
    public void Items_are_retrieved_on_demand_once_and_the_collection_refuses_direct_changes ()
    {
        var retrieved = new List<int> ();
        using var list = VirtualList (retrieved);

        Assert.Equal (5, list.Items.Count);
        Assert.Empty (retrieved);

        Assert.Equal ("v3", list.Items[3].Text);
        Assert.Equal ("v3", list.Items[3].Text);
        Assert.Equal ([3], retrieved);

        Assert.Throws<InvalidOperationException> (() => list.Items.Add ("x"));
        Assert.Throws<InvalidOperationException> (() => list.Items.RemoveAt (0));

        list.VirtualListSize = 2;
        Assert.Equal (2, list.Items.Count);
        Assert.Equal ("v1", list.Items[1].Text);
        Assert.Equal ([3, 1], retrieved);

        list.VirtualMode = false;
        Assert.Empty (list.Items);
        list.Items.Add ("plain");
        Assert.Single (list.Items);
    }

    [Fact]
    public void Painting_announces_the_visible_run_then_retrieves_it ()
    {
        var retrieved = new List<int> ();
        using var list = VirtualList (retrieved);
        var cached = new List<(int start, int end)> ();
        list.CacheVirtualItems += (_, e) => cached.Add ((e.StartIndex, e.EndIndex));

        using var first = PaintSurface.Render (list);
        Assert.Equal ([(0, 4)], cached);
        Assert.Equal ([0, 1, 2, 3, 4], retrieved);

        // Everything is resolved: a second paint asks for nothing.
        using var second = PaintSurface.Render (list);
        Assert.Single (cached);
        Assert.Equal (5, retrieved.Count);
    }

    [Fact]
    public void FindItemWithText_asks_SearchForVirtualItem_and_returns_the_named_index ()
    {
        using var list = VirtualList (new List<int> ());
        SearchForVirtualItemEventArgs? search = null;
        list.SearchForVirtualItem += (_, e) => { search = e; e.Index = e.Text == "wanted" ? 2 : -1; };

        Assert.Equal ("v2", list.FindItemWithText ("wanted")!.Text);
        Assert.True (search!.IsTextSearch);
        Assert.True (search.IsPrefixSearch);
        Assert.Null (list.FindItemWithText ("missing"));
    }

    [Fact]
    public void A_shift_range_reports_VirtualItemsSelectionRangeChanged ()
    {
        using var list = VirtualList (new List<int> ());
        using var _ = PaintSurface.Render (list);
        var ranges = new List<(int start, int end, bool selected)> ();
        list.VirtualItemsSelectionRangeChanged += (_, e) => ranges.Add ((e.StartIndex, e.EndIndex, e.IsSelected));

        var first = list.Items[0].Bounds;
        var third = list.Items[3].Bounds;
        list.DriveClick (new Point (first.Left + 10, first.Top + first.Height / 2));
        list.DriveClick (new Point (third.Left + 10, third.Top + third.Height / 2), Keys.Shift);

        Assert.Equal ([(0, 3, true)], ranges);
        Assert.Equal ([true, true, true, true, false], list.Items.Select (i => i.Selected));
    }
}
