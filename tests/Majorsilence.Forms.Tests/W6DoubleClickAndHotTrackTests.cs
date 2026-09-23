using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, third chunk: item double-clicks on strips and grid dividers, and the hot-tracking
/// visuals (ListView, TreeView, TabControl).
/// </summary>
public class W6DoubleClickAndHotTrackTests
{
    private static MouseEventArgs Double (int x, int y) => new (MouseButtons.Left, 2, x, y, 0);
    private static MouseEventArgs Move (int x, int y) => new (MouseButtons.None, 0, x, y, 0);

    // ── strip items ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_strip_double_click_reaches_the_item_only_with_DoubleClickEnabled ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30) };
        var item = new ToolStripButton ("One");
        strip.Items.Add (item);
        using var bitmap = PaintSurface.Render (strip);
        var fired = 0;
        item.DoubleClick += (_, _) => fired++;
        var b = item.Bounds;

        strip.RaiseDoubleClick (Double (b.Left + b.Width / 2, b.Top + b.Height / 2));
        Assert.Equal (0, fired);                       // upstream: a double-click is two clicks

        item.DoubleClickEnabled = true;
        strip.RaiseDoubleClick (Double (b.Left + b.Width / 2, b.Top + b.Height / 2));
        Assert.Equal (1, fired);
    }

    [Fact]
    public void ButtonDoubleClick_fires_for_the_button_part_of_a_split_button_only ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30) };
        var split = new ToolStripSplitButton { Text = "Save", DoubleClickEnabled = true };
        strip.Items.Add (split);
        using var bitmap = PaintSurface.Render (strip);
        var fired = 0;
        split.ButtonDoubleClick += (_, _) => fired++;
        var b = split.Bounds;

        strip.RaiseDoubleClick (Double (b.Left + 4, b.Top + b.Height / 2));                 // button part
        Assert.Equal (1, fired);
        strip.RaiseDoubleClick (Double (b.Right - 3, b.Top + b.Height / 2));                // drop-down arrow
        Assert.Equal (1, fired);
    }

    // ── grid dividers ───────────────────────────────────────────────────────────────────────────────

    private static DataGridView Grid ()
    {
        var grid = new DataGridView { Size = new Size (400, 200), RowHeadersVisible = true, ColumnHeadersVisible = true };
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "A", Width = 100 });
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "B", Width = 100 });
        grid.Rows.Add ("a much longer value than a hundred pixels holds", "b");
        grid.Rows.Add ("x", "y");
        return grid;
    }

    [Fact]
    public void Double_clicking_a_column_divider_raises_the_event_and_auto_sizes_unless_handled ()
    {
        HeadlessRenderer.Use ();
        using var grid = Grid ();
        using var bitmap = PaintSurface.Render (grid);
        // The divider hit-test compares the event location against device geometry (the same way the
        // resize drag does), so the location is computed in device units.
        var cell = grid.LogicalToDeviceUnits (grid.GetCellDisplayRectangle (0, 0, false));
        var header_y = cell.Top - grid.LogicalToDeviceUnits (grid.ColumnHeadersHeight) / 2;

        var fired = 0; var handled = true;
        grid.ColumnDividerDoubleClick += (_, e) => { fired++; Assert.Equal (0, e.ColumnIndex); e.Handled = handled; };

        grid.RaiseDoubleClick (Double (cell.Right, header_y));
        Assert.Equal (1, fired);
        Assert.Equal (100, grid.Columns[0].Width);      // handled: no resize

        handled = false;
        grid.RaiseDoubleClick (Double (cell.Right, header_y));
        Assert.Equal (2, fired);
        Assert.NotEqual (100, grid.Columns[0].Width);   // auto-sized to the long value
    }

    [Fact]
    public void Double_clicking_a_row_divider_raises_the_event ()
    {
        HeadlessRenderer.Use ();
        using var grid = Grid ();
        using var bitmap = PaintSurface.Render (grid);
        var cell = grid.LogicalToDeviceUnits (grid.GetCellDisplayRectangle (0, 0, false));
        var header_x = cell.Left / 2;                    // inside the row header column

        var rows = new List<int> ();
        grid.RowDividerDoubleClick += (_, e) => { rows.Add (e.RowIndex); e.Handled = true; };

        grid.RaiseDoubleClick (Double (header_x, cell.Bottom));

        Assert.Equal (new[] { 0 }, rows);
    }

    // ── hot tracking ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ListView_HotTracking_tracks_the_item_under_the_pointer_and_colours_it ()
    {
        HeadlessRenderer.Use ();
        using var list = new ListView { Size = new Size (300, 200), View = View.Details, HotTracking = true };
        list.Columns.Add ("A", 200);
        list.Items.Add ("first");
        list.Items.Add ("second");
        using var bitmap = PaintSurface.Render (list);
        var second = list.Items[1].Bounds;

        list.RaiseMouseMove (Move (second.Left + 5, second.Top + second.Height / 2));
        Assert.Same (list.Items[1], list.HotItem);
        Assert.Equal (SystemColors.HotTrack.ToSKColor (), Renderers.ListViewRenderer.ItemForeColour (list, list.Items[1], SkiaSharp.SKColors.Black));
        Assert.Equal (SkiaSharp.SKColors.Black, Renderers.ListViewRenderer.ItemForeColour (list, list.Items[0], SkiaSharp.SKColors.Black));

        list.RaiseMouseLeave (EventArgs.Empty);
        Assert.Null (list.HotItem);

        list.HotTracking = false;
        list.RaiseMouseMove (Move (second.Left + 5, second.Top + second.Height / 2));
        Assert.Null (list.HotItem);                     // off: nothing is tracked
    }

    [Fact]
    public void TreeView_HotTracking_tracks_the_node_under_the_pointer ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var tree = new TreeView { Bounds = new Rectangle (0, 0, 300, 200), HotTracking = true };
        tree.Nodes.Add ("alpha");
        tree.Nodes.Add ("beta");
        form.Controls.Add (tree);
        form.Show ();
        using var bitmap = PaintSurface.Render (tree);
        var beta = tree.Nodes[1].Bounds; // device units
        var at = new Point (tree.DeviceToLogicalUnits (beta.Left + 4), tree.DeviceToLogicalUnits (beta.Top + beta.Height / 2));

        tree.RaiseMouseMove (Move (at.X, at.Y));
        Assert.Same (tree.Nodes[1], tree.HotNode);
        tree.RaiseMouseLeave (EventArgs.Empty);
        Assert.Null (tree.HotNode);

        tree.HotTracking = false;
        tree.RaiseMouseMove (Move (at.X, at.Y));
        Assert.Null (tree.HotNode);
    }

    [Fact]
    public void TabControl_HotTrack_colours_the_hovered_tabs_text ()
    {
        HeadlessRenderer.Use ();
        using var tabs = new TabControl { Size = new Size (300, 200) };
        tabs.TabPages.Add ("One");
        tabs.TabPages.Add ("Two");
        var strip = tabs.TabStrip;
        using var laid_out = PaintSurface.Render (strip);
        strip.Tabs.HoveredIndex = 1;
        Assert.True (strip.Tabs[1].Hovered, "the premise: the second tab is hovered");

        using var hovered_off = PaintSurface.Render (strip);
        tabs.HotTrack = true;
        using var hovered_on = PaintSurface.Render (strip);

        var band = strip.LogicalToDeviceUnits (strip.Tabs[1].Bounds);
        Assert.False (band.IsEmpty, "the premise: the tab has bounds");
        var differing = 0;
        for (var y = band.Top; y < band.Bottom; y++)
            for (var x = band.Left; x < band.Right; x++)
                if (hovered_off.GetPixel (x, y) != hovered_on.GetPixel (x, y))
                    differing++;

        Assert.True (differing > 0, "HotTrack changed no pixel of the hovered tab");
    }

}
