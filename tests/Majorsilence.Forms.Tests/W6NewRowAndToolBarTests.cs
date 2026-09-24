using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, seventh chunk: the DataGridView new-row object (DefaultValuesNeeded / NewRowNeeded /
/// UserAddedRow, real IsNewRow and NewRowIndex) and the legacy ToolBar.Buttons surface mirrored into
/// strip items (ButtonClick / ButtonDropDown and the nineteen button and bar properties).
/// </summary>
public class W6NewRowAndToolBarTests
{
    // ── DataGridView new row ────────────────────────────────────────────────────────────────────────

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;
    }

    private static DataGridView UnboundGrid (out Form form, bool virtualMode = false)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (500, 300) };
        var grid = new DataGridView { Size = new Size (450, 220), VirtualMode = virtualMode };
        grid.Columns.Add ("name", "Name");
        grid.Rows.Add ("one");
        grid.Rows.Add ("two");
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    [Fact]
    public void The_new_row_is_shown_after_the_last_row_and_named_by_NewRowIndex ()
    {
        var grid = UnboundGrid (out var form);
        using var _ = form;
        using var painted = PaintSurface.Render (grid);

        Assert.Equal (2, grid.Rows.Count);
        Assert.Equal (2, grid.NewRowIndex);
        Assert.True (grid.Rows[grid.NewRowIndex].IsNewRow);
        Assert.False (grid.Rows[0].IsNewRow);
        Assert.Equal (2, grid.Rows[grid.NewRowIndex].Index);

        // Laid out directly under the last committed row, and hit-testable there.
        var last = grid.GetCellBounds (1, 0);
        var placeholder = grid.GetCellBounds (2, 0);
        Assert.Equal (last.Bottom, placeholder.Top);
        Assert.Equal (last.Height, placeholder.Height);
        Assert.Equal (2, grid.GetRowAtLocation (new Point (placeholder.Left + 5, placeholder.Top + 5)));

        grid.AllowUserToAddRows = false;
        Assert.Equal (-1, grid.NewRowIndex);
        Assert.Equal (Rectangle.Empty, grid.GetCellBounds (2, 0));

        grid.AllowUserToAddRows = true;
        grid.ReadOnly = true;
        Assert.Equal (-1, grid.NewRowIndex);
    }

    [Fact]
    public void Entering_the_new_row_asks_DefaultValuesNeeded_and_shows_what_the_handler_put_there ()
    {
        var grid = UnboundGrid (out var form);
        using var _ = form;
        var asked = new List<DataGridViewRow> ();
        grid.DefaultValuesNeeded += (_, e) => { asked.Add (e.Row); e.Row.Cells[0].Value = "default"; };

        grid.MoveCurrentCell (1, 0);
        Assert.Empty (asked);

        grid.RaiseKeyDown (new KeyEventArgs (Keys.Down));

        Assert.Equal (2, grid.CurrentCell!.RowIndex);
        Assert.Single (asked);
        Assert.True (asked[0].IsNewRow);
        Assert.Equal ("default", grid.Rows[grid.NewRowIndex].Cells[0].Value);
        Assert.Equal (2, grid.Rows.Count);   // still uncommitted
    }

    [Fact]
    public void A_commit_in_the_new_row_promotes_it_and_raises_UserAddedRow ()
    {
        var grid = UnboundGrid (out var form);
        using var _ = form;
        var added_rows = new List<int> ();
        DataGridViewRow? user_added = null;
        var default_values = 0;
        grid.RowsAdded += (_, e) => added_rows.Add (e.RowIndex);
        grid.UserAddedRow += (_, e) => user_added = e.Row;
        grid.DefaultValuesNeeded += (_, e) => default_values++;

        grid.MoveCurrentCell (2, 0);
        Assert.True (grid.BeginEdit (true));
        ((TextBox)grid.EditingControl!).Text = "three";
        Assert.True (grid.EndEdit ());

        Assert.Equal (3, grid.Rows.Count);
        Assert.Equal ("three", grid.Rows[2].Cells[0].Value);
        Assert.False (grid.Rows[2].IsNewRow);
        Assert.Same (grid.Rows[2], user_added);
        Assert.Equal ([2], added_rows);

        // A fresh placeholder follows, empty, at the next index.
        Assert.Equal (3, grid.NewRowIndex);
        Assert.True (grid.Rows[3].IsNewRow);
        Assert.Null (grid.Rows[3].Cells[0].Value);
        Assert.Equal (1, default_values);
    }

    [Fact]
    public void Cancelling_an_edit_in_the_new_row_keeps_it_uncommitted_and_resets_it ()
    {
        var grid = UnboundGrid (out var form);
        using var _ = form;
        var user_added = 0;
        grid.UserAddedRow += (_, _) => user_added++;

        grid.MoveCurrentCell (2, 0);
        grid.Rows[2].Cells[0].Value = "typed";
        Assert.True (grid.BeginEdit (true));
        grid.CancelEdit ();

        Assert.Equal (2, grid.Rows.Count);
        Assert.Equal (0, user_added);
        Assert.Null (grid.Rows[grid.NewRowIndex].Cells[0].Value);
    }

    [Fact]
    public void A_bound_grid_adds_the_new_item_to_its_list_on_commit ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (500, 300) };
        var list = new List<Row> { new () { Name = "one" } };
        var grid = new DataGridView { Size = new Size (450, 220) };
        grid.Columns.Add (new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name" });
        grid.DataSource = list;
        form.Controls.Add (grid);
        form.Show ();
        DataGridViewRow? user_added = null;
        grid.UserAddedRow += (_, e) => user_added = e.Row;
        grid.DefaultValuesNeeded += (_, e) => e.Row.Cells[0].Value = "seeded";

        Assert.Equal (1, grid.NewRowIndex);
        grid.MoveCurrentCell (1, 0);
        Assert.Equal ("seeded", grid.Rows[1].Cells[0].Value);

        Assert.True (grid.BeginEdit (true));
        ((TextBox)grid.EditingControl!).Text = "two";
        Assert.True (grid.EndEdit ());

        Assert.Equal (2, list.Count);
        Assert.Equal ("two", list[1].Name);
        Assert.Equal (2, grid.Rows.Count);
        Assert.Same (list[1], grid.Rows[1].DataBoundItem);
        Assert.Same (grid.Rows[1], user_added);
        Assert.Equal (2, grid.NewRowIndex);
    }

    [Fact]
    public void A_virtual_grid_asks_NewRowNeeded_before_DefaultValuesNeeded ()
    {
        var grid = UnboundGrid (out var form, virtualMode: true);
        using var _ = form;
        var order = new List<string> ();
        grid.NewRowNeeded += (_, e) => { order.Add ("new"); Assert.True (e.Row.IsNewRow); };
        grid.DefaultValuesNeeded += (_, _) => order.Add ("defaults");
        grid.CellValueNeeded += (_, e) => e.Value = "v";

        grid.MoveCurrentCell (2, 0);

        Assert.Equal (["new", "defaults"], order);
        // The placeholder is not asked of CellValueNeeded: it is not yet data.
        Assert.Null (grid.Rows[2].Cells[0].Value);
        Assert.Equal ("v", grid.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void The_new_row_header_carries_an_asterisk ()
    {
        var grid = UnboundGrid (out var form);
        using var _ = form;
        using var with = PaintSurface.Render (grid);
        var placeholder = grid.GetCellBounds (2, 0);
        var header = new Rectangle (0, placeholder.Top + 2, grid.ScaledRowHeadersWidth - 2, placeholder.Height - 4);

        grid.AllowUserToAddRows = false;
        using var without = PaintSurface.Render (grid);

        static int Differences (SkiaSharp.SKBitmap a, SkiaSharp.SKBitmap b, Rectangle r)
        {
            var n = 0;
            for (var y = r.Top; y < r.Bottom; y++)
                for (var x = r.Left; x < r.Right; x++)
                    if (a.GetPixel (x, y) != b.GetPixel (x, y))
                        n++;
            return n;
        }

        Assert.True (Differences (with, without, header) > 0, "the new row's header band should be painted");
    }

    // ── ToolBar.Buttons ─────────────────────────────────────────────────────────────────────────────

    private static ToolBar Bar (out Form form, int width = 600)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (700, 200) };
        var bar = new ToolBar { Bounds = new Rectangle (0, 0, width, 34), Dock = DockStyle.None };
        form.Controls.Add (bar);
        form.Show ();
        return bar;
    }

    private static Point Centre (ToolBar bar, ToolBarButton button)
        => new (button.Rectangle.Left + button.Rectangle.Width / 2, button.Rectangle.Top + button.Rectangle.Height / 2);

    private static MouseEventArgs Left (Point p) => new (MouseButtons.Left, 1, p.X, p.Y, 0);

    [Fact]
    public void Buttons_are_mirrored_into_items_in_order_and_follow_their_properties ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        var push = bar.Buttons.Add ("Open");
        var separator = new ToolBarButton { Style = ToolBarButtonStyle.Separator };
        bar.Buttons.Add (separator);
        var toggle = new ToolBarButton { Text = "Bold", Style = ToolBarButtonStyle.ToggleButton, Pushed = true };
        bar.Buttons.Add (toggle);

        Assert.Equal (3, bar.Items.Count);
        Assert.Equal ("Open", bar.Items[0].Text);
        Assert.IsType<MenuSeparatorItem> (bar.Items[1]);
        Assert.True (bar.Items[2].Checked);
        Assert.Same (bar, toggle.Parent);

        push.Text = "Save";
        push.Enabled = false;
        toggle.Visible = false;
        Assert.Equal ("Save", bar.Items[0].Text);
        Assert.False (bar.Items[0].Enabled);
        Assert.False (bar.Items[2].Visible);

        using var laid_out = PaintSurface.Render (bar);
        Assert.Equal (bar.Items[0].Bounds, push.Rectangle);
        Assert.True (push.Rectangle.Width >= bar.ButtonSize.Width);

        bar.Buttons.Remove (separator);
        Assert.Equal (2, bar.Items.Count);
        Assert.Null (separator.Parent);
    }

    [Fact]
    public void Clicking_a_button_raises_ButtonClick_and_a_toggle_flips_Pushed ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        var open = bar.Buttons.Add ("Open");
        var bold = new ToolBarButton { Text = "Bold", Style = ToolBarButtonStyle.ToggleButton };
        bar.Buttons.Add (bold);
        using var laid_out = PaintSurface.Render (bar);
        var clicked = new List<ToolBarButton> ();
        bar.ButtonClick += (_, e) => clicked.Add (e.Button);

        bar.RaiseClick (Left (Centre (bar, open)));
        bar.RaiseClick (Left (Centre (bar, bold)));

        Assert.Equal ([open, bold], clicked);
        Assert.True (bold.Pushed);
        Assert.True (bar.Items[1].Checked);
        Assert.False (open.Pushed);
    }

    [Fact]
    public void A_drop_down_button_opens_from_its_arrow_and_clicks_from_its_body ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        bar.DropDownArrows = true;
        var menu = new ContextMenu ();
        menu.MenuItems.Add ("Recent");
        var drop = new ToolBarButton { Text = "Open", Style = ToolBarButtonStyle.DropDownButton, DropDownMenu = menu };
        bar.Buttons.Add (drop);
        using var laid_out = PaintSurface.Render (bar);
        var clicks = 0;
        var drops = 0;
        var popups = 0;
        bar.ButtonClick += (_, _) => clicks++;
        bar.ButtonDropDown += (_, e) => { drops++; Assert.Same (drop, e.Button); };
        menu.Popup += (_, _) => popups++;

        var body = new Point (drop.Rectangle.Left + 4, drop.Rectangle.Top + drop.Rectangle.Height / 2);
        var arrow = new Point (drop.Rectangle.Right - 4, body.Y);

        // MenuBase.TryBeginLeafClick treats a second click on the same leaf item within 50 ms as the
        // duplicate delivery of one release (an X11 defence), so the clicks here are spaced past it.
        static void Settle () => System.Threading.Thread.Sleep (60);

        bar.RaiseClick (Left (body));
        Assert.Equal ((1, 0), (clicks, drops));

        Settle ();
        bar.RaiseClick (Left (arrow));
        Assert.Equal ((1, 1), (clicks, drops));
        Assert.Equal (1, popups);

        // Without arrows the whole button is the drop-down.
        bar.DropDownArrows = false;
        Settle ();
        bar.RaiseClick (Left (body));
        Assert.Equal ((1, 2), (clicks, drops));
    }

    [Fact]
    public void ShowToolTips_makes_a_button_ToolTipText_the_tip ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        var open = bar.Buttons.Add ("Open");
        open.ToolTipText = "Open a file";
        using var laid_out = PaintSurface.Render (bar);
        var at = Centre (bar, open);

        Assert.Null (bar.GetToolTipText (at));

        bar.ShowToolTips = true;
        Assert.Equal ("Open a file", bar.GetToolTipText (at));
    }

    [Fact]
    public void ButtonSize_TextAlign_and_ImageList_reach_the_mirrored_item ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        using var images = new ImageList ();
        images.Images.Add ("disk", new SkiaSharp.SKBitmap (16, 16));
        bar.ImageList = images;
        var open = new ToolBarButton { Text = "Open", ImageKey = "disk" };
        bar.Buttons.Add (open);
        var item = (ToolStripItem)bar.Items[0];

        Assert.NotNull (item.ImageSK);
        Assert.Equal (TextImageRelation.ImageAboveText, item.TextImageRelation);

        bar.TextAlign = ToolBarTextAlign.Right;
        Assert.Equal (TextImageRelation.ImageBeforeText, ((ToolStripItem)bar.Items[0]).TextImageRelation);

        bar.ButtonSize = new Size (120, 30);
        using var laid_out = PaintSurface.Render (bar);
        Assert.True (open.Rectangle.Width >= 120);

        open.ImageKey = string.Empty;
        open.ImageIndex = 0;
        Assert.NotNull (((ToolStripItem)bar.Items[0]).ImageSK);
        open.ImageIndex = -1;
        Assert.Null (((ToolStripItem)bar.Items[0]).ImageSK);
    }

    [Fact]
    public void Wrappable_wraps_buttons_onto_a_second_row_and_grows_the_bar ()
    {
        var bar = Bar (out var form, width: 130);
        using var _ = form;
        bar.ButtonSize = new Size (60, 22);
        for (var i = 0; i < 4; i++)
            bar.Buttons.Add ($"B{i}");
        var height = bar.Height;

        using var wrapped = PaintSurface.Render (bar);
        Assert.True (bar.Buttons[3].Rectangle.Top > bar.Buttons[0].Rectangle.Top, "the fourth button should sit on a second row");
        Assert.True (bar.Buttons[3].Rectangle.Right <= bar.Width);
        Assert.True (bar.Height > height, "the bar should grow to hold the second row");

        bar.Wrappable = false;
        using var single = PaintSurface.Render (bar);
        Assert.Equal (bar.Buttons[0].Rectangle.Top, bar.Buttons[3].Rectangle.Top);
    }

    [Fact]
    public void Appearance_and_Divider_change_what_the_bar_paints ()
    {
        var bar = Bar (out var form);
        using var _ = form;
        bar.Buttons.Add ("Open");
        using var normal = PaintSurface.Render (bar);
        var r = bar.Buttons[0].Rectangle;
        var edge = new Point (bar.LogicalToDeviceUnits (r.Left), bar.LogicalToDeviceUnits (r.Top + r.Height / 2));

        bar.Appearance = ToolBarAppearance.Flat;
        using var flat = PaintSurface.Render (bar);
        Assert.NotEqual (normal.GetPixel (edge.X, edge.Y), flat.GetPixel (edge.X, edge.Y));

        bar.Divider = false;
        using var undivided = PaintSurface.Render (bar);
        Assert.NotEqual (flat.GetPixel (bar.ScaledWidth / 2, 0), undivided.GetPixel (bar.ScaledWidth / 2, 0));
    }
}
