using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms: the hover rest-timer, ListView.BackgroundImageTiled, DataGridView error glyphs with
/// the *ErrorTextNeeded events, and the managed caption's help button and icon switch.
/// </summary>
public class W6MechanismTests
{
    private static MouseEventArgs At (int x, int y) => new (MouseButtons.None, 0, x, y, 0);

    // ── hover rest-timer ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MouseHover_fires_once_per_rest_until_rearmed ()
    {
        using var control = new Panel { Size = new Size (50, 50) };
        var fired = 0;
        control.MouseHover += (_, _) => fired++;

        control.RaiseMouseEnter (At (5, 5));
        control.RaiseHoverAfterRest ();
        control.RaiseMouseMove (At (6, 6));
        control.RaiseHoverAfterRest ();
        Assert.Equal (1, fired);                  // movement alone does not re-arm

        control.RaiseMouseLeave (EventArgs.Empty);
        control.RaiseMouseEnter (At (5, 5));
        control.RaiseHoverAfterRest ();
        Assert.Equal (2, fired);                  // a fresh entry does
    }

    [Fact]
    public void ListView_ItemMouseHover_names_the_rested_item_and_HoverSelection_selects_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var list = new ListView { Bounds = new Rectangle (0, 0, 300, 200), View = View.Details, HoverSelection = true };
        list.Columns.Add ("A", 200);
        list.Items.Add ("first");
        list.Items.Add ("second");
        form.Controls.Add (list);
        form.Show ();
        using var bitmap = PaintSurface.Render (list);

        ListViewItem? hovered = null;
        list.ItemMouseHover += (_, e) => hovered = e.Item;

        var second = list.Items[1].Bounds;
        list.RaiseMouseMove (At (second.Left + 5, second.Top + second.Height / 2));
        list.RaiseHoverAfterRest ();

        Assert.Same (list.Items[1], hovered);
        Assert.True (list.Items[1].Selected);
    }

    [Fact]
    public void ToolStripItem_MouseHover_fires_for_the_hovered_item_after_the_rest ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30) };
        var item = new ToolStripButton ("One");
        strip.Items.Add (item);
        using var bitmap = PaintSurface.Render (strip);
        var fired = 0;
        item.MouseHover += (_, _) => fired++;

        var b = item.Bounds;
        strip.RaiseMouseMove (At (b.Left + b.Width / 2, b.Top + b.Height / 2));
        Assert.Equal (0, fired);
        strip.RaiseHoverAfterRest ();

        Assert.Equal (1, fired);
    }

    // ── ListView.BackgroundImageTiled ───────────────────────────────────────────────────────────────

    [Fact]
    public void ListView_BackgroundImageTiled_maps_onto_the_background_image_layout ()
    {
        using var list = new ListView ();
        Assert.False (list.BackgroundImageTiled);
        Assert.Equal (ImageLayout.None, list.BackgroundImageLayout);

        list.BackgroundImageTiled = true;
        Assert.Equal (ImageLayout.Tile, list.BackgroundImageLayout);
        list.BackgroundImageTiled = false;
        Assert.Equal (ImageLayout.None, list.BackgroundImageLayout);
    }

    // ── DataGridView error glyphs ───────────────────────────────────────────────────────────────────

    private static bool HasErrorRed (SKBitmap bitmap, Rectangle device)
    {
        for (var y = device.Top; y < device.Bottom; y++)
            for (var x = device.Left; x < device.Right; x++)
                if (bitmap.GetPixel (x, y) == Renderers.DataGridViewRenderer.ErrorGlyphColor)
                    return true;
        return false;
    }

    private static DataGridView Grid ()
    {
        var grid = new DataGridView { Size = new Size (300, 120), ColumnHeadersVisible = false, RowHeadersVisible = true };
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "A", Width = 150 });
        grid.Rows.Add ("x");
        return grid;
    }

    [Fact]
    public void A_cell_with_ErrorText_gets_the_glyph_unless_ShowCellErrors_is_off ()
    {
        HeadlessRenderer.Use ();
        using var grid = Grid ();
        grid.Rows[0].Cells[0].ErrorText = "bad";
        var cell = grid.LogicalToDeviceUnits (grid.GetCellDisplayRectangle (0, 0, false));

        using var with = PaintSurface.Render (grid);
        Assert.True (HasErrorRed (with, cell));

        grid.ShowCellErrors = false;
        using var without = PaintSurface.Render (grid);
        Assert.False (HasErrorRed (without, cell));
    }

    [Fact]
    public void A_row_with_ErrorText_gets_the_glyph_in_its_header_unless_ShowRowErrors_is_off ()
    {
        HeadlessRenderer.Use ();
        using var grid = Grid ();
        grid.Rows[0].ErrorText = "bad row";
        var cell = grid.GetCellDisplayRectangle (0, 0, false);
        var header = grid.LogicalToDeviceUnits (new Rectangle (0, cell.Top, cell.Left, cell.Height));

        using var with = PaintSurface.Render (grid);
        Assert.True (HasErrorRed (with, header));

        grid.ShowRowErrors = false;
        using var without = PaintSurface.Render (grid);
        Assert.False (HasErrorRed (without, header));
    }

    private sealed class Row { public string A { get; set; } = "x"; }

    [Fact]
    public void CellErrorTextNeeded_and_RowErrorTextNeeded_are_asked_for_a_bound_grid_only ()
    {
        HeadlessRenderer.Use ();
        using var bound = new DataGridView { Size = new Size (300, 120), ColumnHeadersVisible = false, DataSource = new List<Row> { new () } };
        var cells = 0; var rows = 0;
        bound.CellErrorTextNeeded += (_, e) => { cells++; e.ErrorText = "from handler"; };
        bound.RowErrorTextNeeded += (_, e) => { rows++; e.ErrorText = "from handler"; };
        using var painted = PaintSurface.Render (bound);
        Assert.True (cells > 0);
        Assert.True (rows > 0);
        var cell = bound.LogicalToDeviceUnits (bound.GetCellDisplayRectangle (0, 0, false));
        Assert.True (HasErrorRed (painted, cell));

        using var unbound = Grid ();
        var asked = 0;
        unbound.CellErrorTextNeeded += (_, _) => asked++;
        unbound.RowErrorTextNeeded += (_, _) => asked++;
        using var painted2 = PaintSurface.Render (unbound);
        Assert.Equal (0, asked);
    }

    // ── managed caption: help button and icon ───────────────────────────────────────────────────────

    [Fact]
    public void HelpButton_shows_the_caption_help_button_and_a_click_raises_HelpButtonClicked_then_HelpRequested ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        form.Show ();
        Assert.False (form.TitleBar.AllowHelp);

        form.HelpButton = true;
        Assert.True (form.TitleBar.AllowHelp);
        // Only where the library draws its own caption; under system decorations the title bar is hidden.
        if (form.TitleBar.Visible && !form.TitleBar.NativeOverlay)
            Assert.True (form.TitleBar.HelpButtonControl.Visible);

        var clicked = 0; var help = 0; var cancel = false;
        form.HelpButtonClicked += (_, e) => { clicked++; e.Cancel = cancel; };
        form.HelpRequested += (_, _) => help++;

        ((Button)form.TitleBar.HelpButtonControl).PerformClick ();
        Assert.Equal ((1, 1), (clicked, help));

        cancel = true;
        ((Button)form.TitleBar.HelpButtonControl).PerformClick ();
        Assert.Equal ((2, 1), (clicked, help));

        form.ControlBox = false;
        Assert.False (form.TitleBar.AllowHelp);
    }

    [Fact]
    public void ShowIcon_drives_the_captions_image_switch ()
    {
        using var form = new Form ();
        Assert.True (form.ShowIcon);
        form.ShowIcon = false;
        Assert.False (form.TitleBar.ShowImage);
        form.ShowIcon = true;
        Assert.True (form.TitleBar.ShowImage);
    }
}
