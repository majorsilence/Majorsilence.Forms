using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, ninth chunk: DataGridView column display order (DisplayIndex,
/// ColumnDisplayIndexChanged), three-state check-box cells, image-cell layout and icons, the
/// column-text fallback for button and link cells, and the text editing control's grid plumbing.
/// </summary>
public class W6ColumnOrderAndCellsTests
{
    private static DataGridView Grid (out Form form, params string[] columns)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (600, 300) };
        var grid = new DataGridView { Size = new Size (500, 200), AllowUserToAddRows = false, RowHeadersVisible = false };
        foreach (var c in columns)
            grid.Columns.Add (c.ToLowerInvariant (), c);
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    private static Point Centre (DataGridView grid, int row, int column)
    {
        var b = grid.GetCellBounds (row, column);
        return new Point (grid.DeviceToLogicalUnits (b.Left + b.Width / 2), grid.DeviceToLogicalUnits (b.Top + b.Height / 2));
    }

    // Rows.Add (values) makes plain cells; a test about a typed cell's own members adds the typed cell.
    private static DataGridViewRow TypedRow (object first, DataGridViewCell second)
    {
        var row = new DataGridViewRow ();
        row.Cells.Add (new DataGridViewCell { Value = first });
        row.Cells.Add (second);
        return row;
    }

    private static void Click (DataGridView grid, Point at)
    {
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));
        grid.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));
    }

    // ── column display order ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Setting_DisplayIndex_moves_the_column_and_announces_every_column_that_moved ()
    {
        var grid = Grid (out var form, "A", "B", "C");
        using var _ = form;
        grid.Rows.Add ("a", "b", "c");
        var changed = new List<string> ();
        grid.ColumnDisplayIndexChanged += (_, e) => changed.Add (e.Column.HeaderText);

        grid.Columns[2].DisplayIndex = 0;

        Assert.Equal (["C", "A", "B"], grid.DisplayOrder.Select (i => grid.Columns[i].HeaderText));
        Assert.Equal ([1, 2, 0], grid.Columns.Cast<DataGridViewColumn> ().Select (c => c.DisplayIndex));
        Assert.Equal (["C", "A", "B"], changed);           // all three moved
        Assert.Equal (2, grid.Columns["c"]!.Index);          // the collection is untouched

        // Geometry follows: C's cell is at the left edge, A's after it.
        using var painted = PaintSurface.Render (grid);
        Assert.Equal (grid.GetColumnDeviceLeft (2) + grid.LogicalToDeviceUnits (grid.Columns[2].Width), grid.GetColumnDeviceLeft (0));
        Assert.True (grid.GetCellBounds (0, 2).Left < grid.GetCellBounds (0, 0).Left);
        Assert.Equal (2, grid.GetColumnAtLocation (new Point (grid.GetColumnDeviceLeft (2) + 5, grid.GetCellBounds (0, 2).Top + 5)));

        // An unchanged set announces nothing.
        changed.Clear ();
        grid.Columns[2].DisplayIndex = 0;
        Assert.Empty (changed);
    }

    [Fact]
    public void Keyboard_and_clipboard_walk_the_display_order ()
    {
        var grid = Grid (out var form, "A", "B", "C");
        using var _ = form;
        grid.Rows.Add ("a", "b", "c");
        grid.Columns[2].DisplayIndex = 0;   // C A B

        grid.MoveCurrentCell (0, 2);
        grid.RaiseKeyDown (new KeyEventArgs (Keys.Right));
        Assert.Equal (0, grid.CurrentCell!.ColumnIndex);   // A is displayed after C
        grid.RaiseKeyDown (new KeyEventArgs (Keys.End));
        Assert.Equal (1, grid.CurrentCell!.ColumnIndex);   // B is displayed last
        grid.RaiseKeyDown (new KeyEventArgs (Keys.Home));
        Assert.Equal (2, grid.CurrentCell!.ColumnIndex);   // C is displayed first
        grid.RaiseKeyDown (new KeyEventArgs (Keys.Left));
        Assert.Equal (2, grid.CurrentCell!.ColumnIndex);   // nothing before it

        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.Rows[0].Selected = true;
        grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        var text = grid.GetClipboardContent ()!.GetData (DataFormats.Text.Name) as string;
        Assert.Equal ("c\ta\tb", text!.Trim ());
    }

    [Fact]
    public void Removing_a_column_keeps_the_display_order_dense_and_a_detached_column_stores_its_value ()
    {
        var grid = Grid (out var form, "A", "B", "C");
        using var _ = form;
        grid.Columns[2].DisplayIndex = 0;   // C A B
        var changed = new List<string> ();
        grid.ColumnDisplayIndexChanged += (_, e) => changed.Add (e.Column.HeaderText);

        grid.Columns.RemoveAt (0);          // A goes

        Assert.Equal (["C", "B"], grid.DisplayOrder.Select (i => grid.Columns[i].HeaderText));
        Assert.Equal ([1, 0], grid.Columns.Cast<DataGridViewColumn> ().Select (c => c.DisplayIndex));
        Assert.Equal (["B"], changed);      // C stayed at 0; B moved from 2 to 1

        var detached = new DataGridViewTextBoxColumn { DisplayIndex = 5 };
        Assert.Equal (5, detached.DisplayIndex);
    }

    // ── three-state check-box cells ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_three_state_column_cycles_unchecked_checked_indeterminate_and_maps_IndeterminateValue ()
    {
        var grid = Grid (out var form, "A");
        using var _ = form;
        var flag = new DataGridViewCheckBoxColumn { HeaderText = "Flag", ThreeState = true, TrueValue = "Y", FalseValue = "N", IndeterminateValue = "?" };
        grid.Columns.Add (flag);
        grid.Rows.Add ("x", "N");
        using var laid_out = PaintSurface.Render (grid);
        var at = Centre (grid, 0, 1);
        var cell = grid.Rows[0].Cells[1];
        var dirty_seen = new List<bool> ();
        grid.CellValueChanged += (_, e) => { if (e.ColumnIndex == 1) dirty_seen.Add (grid.IsCurrentCellDirty); };

        Click (grid, at);
        Assert.Equal ("Y", cell.Value);
        Assert.Equal (CheckState.Checked, DataGridView.CheckStateOf (flag, cell.Value, cell));

        Click (grid, at);
        Assert.Equal ("?", cell.Value);
        Assert.Equal (CheckState.Indeterminate, DataGridView.CheckStateOf (flag, cell.Value, cell));

        Click (grid, at);
        Assert.Equal ("N", cell.Value);
        Assert.Equal (CheckState.Unchecked, DataGridView.CheckStateOf (flag, cell.Value, cell));
        Assert.All (dirty_seen, Assert.True);   // the toggle reports dirty while it commits

        // Two-state: checked goes straight back to unchecked; nothing is ever indeterminate.
        flag.ThreeState = false;
        Click (grid, at);
        Assert.Equal ("Y", cell.Value);
        Click (grid, at);
        Assert.Equal ("N", cell.Value);
        Assert.NotEqual (CheckState.Indeterminate, DataGridView.CheckStateOf (flag, null, cell));
    }

    [Fact]
    public void An_indeterminate_cell_paints_a_third_glyph ()
    {
        var grid = Grid (out var form, "A");
        using var _ = form;
        var flag = new DataGridViewCheckBoxColumn { HeaderText = "Flag", ThreeState = true };
        grid.Columns.Add (flag);
        grid.Rows.Add ("x", false);
        using var unchecked_paint = PaintSurface.Render (grid);
        grid.Rows[0].Cells[1].Value = CheckState.Indeterminate;
        using var indeterminate = PaintSurface.Render (grid);
        grid.Rows[0].Cells[1].Value = true;
        using var checked_paint = PaintSurface.Render (grid);

        var b = grid.GetCellBounds (0, 1);
        static int Differences (SkiaSharp.SKBitmap x, SkiaSharp.SKBitmap y, Rectangle r)
        {
            var n = 0;
            for (var yy = r.Top; yy < r.Bottom; yy++)
                for (var xx = r.Left; xx < r.Right; xx++)
                    if (x.GetPixel (xx, yy) != y.GetPixel (xx, yy))
                        n++;
            return n;
        }

        Assert.True (Differences (unchecked_paint, indeterminate, b) > 0, "indeterminate should differ from unchecked");
        Assert.True (Differences (checked_paint, indeterminate, b) > 0, "indeterminate should differ from checked");
    }

    // ── image cells ─────────────────────────────────────────────────────────────────────────────────

    private static int RedPixels (SkiaSharp.SKBitmap bitmap, Rectangle r)
    {
        var n = 0;
        for (var y = r.Top; y < r.Bottom; y++)
            for (var x = r.Left; x < r.Right; x++)
                if (bitmap.GetPixel (x, y) == SkiaSharp.SKColors.Red)
                    n++;
        return n;
    }

    [Fact]
    public void ImageLayout_scales_the_image_as_Normal_Zoom_or_Stretch_and_the_column_sets_the_default ()
    {
        var grid = Grid (out var form, "A");
        using var _ = form;
        var column = new DataGridViewImageColumn { HeaderText = "Pic", Width = 80 };
        grid.Columns.Add (column);
        grid.RowTemplate.Height = 40;
        var red = new SkiaSharp.SKBitmap (8, 8);
        red.Erase (SkiaSharp.SKColors.Red);
        var cell = new DataGridViewImageCell { Value = new Majorsilence.Forms.Drawing.Bitmap (red) };
        grid.Rows.Add (TypedRow ("x", cell));

        using var normal = PaintSurface.Render (grid);
        var box = grid.GetCellBounds (0, 1);
        var normal_pixels = RedPixels (normal, box);
        Assert.InRange (normal_pixels, 60, 70);   // drawn at its own 8x8

        column.ImageLayout = DataGridViewImageCellLayout.Zoom;
        using var zoomed = PaintSurface.Render (grid);
        var zoomed_pixels = RedPixels (zoomed, box);
        Assert.True (zoomed_pixels > normal_pixels * 4, $"zoom should scale up ({zoomed_pixels} vs {normal_pixels})");

        cell.ImageLayout = DataGridViewImageCellLayout.Stretch;   // the cell outranks the column
        using var stretched = PaintSurface.Render (grid);
        var stretched_pixels = RedPixels (stretched, box);
        Assert.True (stretched_pixels > zoomed_pixels, $"stretch should fill the cell ({stretched_pixels} vs {zoomed_pixels})");
    }

    [Fact]
    public void Icon_values_and_the_column_Icon_are_drawn_when_values_are_icons ()
    {
        var grid = Grid (out var form, "A");
        using var _ = form;
        var red = new SkiaSharp.SKBitmap (8, 8);
        red.Erase (SkiaSharp.SKColors.Red);
        var column = new DataGridViewImageColumn (valuesAreIcons: true) { HeaderText = "Pic", Width = 80, Icon = new Majorsilence.Forms.Drawing.Icon (red) };
        grid.Columns.Add (column);
        var icon_cell = new DataGridViewImageCell { Value = new Majorsilence.Forms.Drawing.Icon (red) };
        grid.Rows.Add (TypedRow ("x", new DataGridViewImageCell ()));
        grid.Rows.Add (TypedRow ("y", icon_cell));

        using var painted = PaintSurface.Render (grid);
        Assert.True (RedPixels (painted, grid.GetCellBounds (0, 1)) > 0, "the column's Icon is the fallback");
        Assert.True (RedPixels (painted, grid.GetCellBounds (1, 1)) > 0, "an Icon value is drawn");

        column.ValuesAreIcons = false;
        icon_cell.ValueIsIcon = true;
        using var per_cell = PaintSurface.Render (grid);
        Assert.Equal (0, RedPixels (per_cell, grid.GetCellBounds (0, 1)));   // no fallback icon now
        Assert.True (RedPixels (per_cell, grid.GetCellBounds (1, 1)) > 0);     // the cell says its value is one
    }

    // ── button and link column text ─────────────────────────────────────────────────────────────────

    private static int Ink (SkiaSharp.SKBitmap bitmap, Rectangle r)
    {
        var background = bitmap.GetPixel (r.Left + 2, r.Top + 2);
        var n = 0;
        for (var y = r.Top + 3; y < r.Bottom - 3; y++)
            for (var x = r.Left + 3; x < r.Right - 3; x++)
                if (bitmap.GetPixel (x, y) != background)
                    n++;
        return n;
    }

    [Fact]
    public void UseColumnTextForButtonValue_and_UseColumnTextForLinkValue_draw_the_column_Text ()
    {
        var grid = Grid (out var form, "A");
        using var _ = form;
        var button = new DataGridViewButtonColumn { HeaderText = "Go", Text = "WWWWWWWW", FlatStyle = FlatStyle.Flat };
        var link = new DataGridViewLinkColumn { HeaderText = "Open", Text = "WWWWWWWW" };
        grid.Columns.Add (button);
        grid.Columns.Add (link);
        var button_cell = new DataGridViewButtonCell { Value = string.Empty };
        var link_cell = new DataGridViewLinkCell { Value = string.Empty };
        var row = new DataGridViewRow ();
        row.Cells.Add (new DataGridViewCell { Value = "x" });
        row.Cells.Add (button_cell);
        row.Cells.Add (link_cell);
        grid.Rows.Add (row);

        // With an empty value the cells carry only their chrome; the column's Text adds ink.
        using var plain = PaintSurface.Render (grid);
        var button_chrome = Ink (plain, grid.GetCellBounds (0, 1));
        var link_chrome = Ink (plain, grid.GetCellBounds (0, 2));

        button.UseColumnTextForButtonValue = true;
        link.UseColumnTextForLinkValue = true;
        using var column_text = PaintSurface.Render (grid);
        Assert.True (Ink (column_text, grid.GetCellBounds (0, 1)) > button_chrome, "the button should show the column's Text");
        Assert.True (Ink (column_text, grid.GetCellBounds (0, 2)) > link_chrome, "the link should show the column's Text");

        // The per-cell flag works alone too.
        button.UseColumnTextForButtonValue = false;
        link.UseColumnTextForLinkValue = false;
        button_cell.UseColumnTextForButtonValue = true;
        link_cell.UseColumnTextForLinkValue = true;
        using var cell_text = PaintSurface.Render (grid);
        Assert.True (Ink (cell_text, grid.GetCellBounds (0, 1)) > button_chrome);
        Assert.True (Ink (cell_text, grid.GetCellBounds (0, 2)) > link_chrome);
    }

    // ── editing control plumbing ────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_text_editor_knows_its_grid_and_row_and_reports_a_change ()
    {
        var grid = Grid (out var form, "A", "B");
        using var _ = form;
        grid.Rows.Add ("a", "b");
        grid.Rows.Add ("c", "d");

        grid.MoveCurrentCell (1, 0);
        Assert.True (grid.BeginEdit (true));

        var editor = Assert.IsType<DataGridViewTextBoxEditingControl> (grid.EditingControl);
        Assert.Same (grid, editor.EditingControlDataGridView);
        Assert.Equal (1, editor.EditingControlRowIndex);
        Assert.False (editor.EditingControlValueChanged);
        Assert.False (grid.IsCurrentCellDirty);

        editor.Text = "changed";
        Assert.True (editor.EditingControlValueChanged);
        Assert.True (grid.IsCurrentCellDirty);
    }
}
