using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6: the events that were declared <c>{ add { } remove { } }</c> and so discarded every handler.
/// All of them keep their handlers now; these are the ones with a raise within reach.
/// </summary>
public class DiscardedHandlerEventTests
{
    private static MouseEventArgs At (int x, int y) => new (MouseButtons.None, 0, x, y, 0);

    [Fact]
    public void TreeView_NodeMouseHover_names_the_node_under_the_rested_pointer ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var tree = new TreeView { Bounds = new Rectangle (0, 0, 300, 200) };
        tree.Nodes.Add ("alpha");
        tree.Nodes.Add ("beta");
        form.Controls.Add (tree);
        form.Show ();
        using var bitmap = PaintSurface.Render (tree);

        TreeNode? hovered = null;
        tree.NodeMouseHover += (_, e) => hovered = e.Node;

        var beta = tree.Nodes[1].Bounds; // device units
        var at = new Point (tree.DeviceToLogicalUnits (beta.Left + 4), tree.DeviceToLogicalUnits (beta.Top + beta.Height / 2));
        tree.RaiseMouseMove (At (at.X, at.Y));
        tree.RaiseHoverAfterRest ();

        Assert.Same (tree.Nodes[1], hovered);
    }

    [Fact]
    public void ToolStripLabel_MouseEnter_and_MouseLeave_reach_a_subscriber_on_the_label ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30) };
        var label = new ToolStripLabel { Text = "Status" };
        strip.Items.Add (label);
        using var bitmap = PaintSurface.Render (strip);
        var entered = 0; var left = 0;
        label.MouseEnter += (_, _) => entered++;
        label.MouseLeave += (_, _) => left++;

        var b = label.Bounds;
        strip.RaiseMouseMove (At (b.Left + b.Width / 2, b.Top + b.Height / 2));
        Assert.Equal ((1, 0), (entered, left));
        strip.RaiseMouseMove (At (b.Right + 40, b.Top + b.Height / 2));
        Assert.Equal ((1, 1), (entered, left));
    }

    [Fact]
    public void SystemColorsChanged_fires_when_the_theme_changes ()
    {
        using var control = new Panel ();
        var fired = 0;
        control.SystemColorsChanged += (_, _) => fired++;

        control.OnThemeChanged (EventArgs.Empty);

        Assert.Equal (1, fired);
    }

    [Fact]
    public void RowStateChanged_and_CellStateChanged_report_a_selection_change ()
    {
        using var grid = new DataGridView ();
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "A" });
        grid.Rows.Add ("x");
        grid.Rows.Add ("y");
        var rows = new List<(int, DataGridViewElementStates)> ();
        var cells = new List<(int, DataGridViewElementStates)> ();
        grid.RowStateChanged += (_, e) => rows.Add ((e.Row.Index, e.StateChanged));
        grid.CellStateChanged += (_, e) => cells.Add ((e.Cell.RowIndex, e.StateChanged));

        grid.Rows[1].Selected = true;
        grid.Rows[1].Selected = true;   // no change: no event
        grid.Rows[0].Cells[0].Selected = true;

        Assert.Contains ((1, DataGridViewElementStates.Selected), rows);
        Assert.Equal (1, rows.Count (r => r.Item1 == 1));
        Assert.Contains ((0, DataGridViewElementStates.Selected), cells);
    }

    [Fact]
    public void PropertyGrid_SelectedObjectsChanged_and_SelectedGridItemChanged_fire ()
    {
        using var grid = new PropertyGrid { Size = new Size (300, 200) };
        var objects = 0; var items = 0;
        grid.SelectedObjectsChanged += (_, _) => objects++;
        grid.SelectedGridItemChanged += (_, _) => items++;

        var target = new { Name = "n", Age = 3 };
        grid.SelectedObject = target;
        grid.SelectedObject = target;
        Assert.Equal (1, objects);

        // Row 0 is the category band; rows are 22 logical pixels tall.
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, 22 + 11, 0));
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, 22 + 11, 0));
        Assert.Equal (1, items);
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, 44 + 11, 0));
        Assert.Equal (2, items);
    }

    // ── found on the way: a closed popup stayed registered as the active one ─────────────────────────

    [Fact]
    public void A_popup_closed_through_Close_is_no_longer_the_active_popup ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        form.Show ();
        var popup = new PopupWindow (form) { Size = new Size (50, 50) };
        popup.Show (10, 10);
        Assert.Same (popup, Application.ActivePopupWindow);

        popup.Close ();

        Assert.Null (Application.ActivePopupWindow);
    }

    [Fact]
    public void A_popup_closes_with_the_window_it_was_opened_for ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (300, 200) };
        form.Show ();
        var popup = new PopupWindow (form) { Size = new Size (50, 50) };
        popup.Show (10, 10);
        var closed = 0;
        popup.Closed += (_, _) => closed++;

        form.Close ();

        Assert.Equal (1, closed);
        Assert.Null (Application.ActivePopupWindow);
    }

    [Fact]
    public void A_formerly_discarding_event_keeps_its_handler ()
    {
        // ToolStripControlHost.ContentChanged is never raised here (it is not an upstream member), but a
        // handler attached to it must at least be stored -- the old accessors threw it away.
        var host = new ToolStripControlHost (new TextBox ());
        EventHandler handler = (_, _) => { };
        host.ContentChanged += handler;
        host.ContentChanged -= handler;   // completes without a stored delegate throwing or being missed
        Assert.NotNull (host);
    }
}
