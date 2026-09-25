using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, fifth chunk: owner drawing. ListView.OwnerDraw with DrawColumnHeader / DrawItem /
/// DrawSubItem, ListBox and ComboBox DrawMode with DrawItem / MeasureItem, ToolTip.OwnerDraw with
/// Draw, and StatusBar panels with DrawItem and PanelClick.
/// </summary>
public class W6OwnerDrawTests
{
    private static ListView DetailsList ()
    {
        HeadlessRenderer.Use ();
        var list = new ListView { Size = new Size (400, 200), View = View.Details, OwnerDraw = true };
        list.Columns.Add ("A", 100);
        list.Columns.Add ("B", 100);
        var item = list.Items.Add ("a1");
        item.SubItems.Add ("b1");
        list.Items.Add ("a2");
        return list;
    }

    [Fact]
    public void An_owner_drawn_ListView_offers_headers_items_and_then_cells_of_a_declined_item ()
    {
        using var list = DetailsList ();
        var headers = new List<int> ();
        var items = new List<int> ();
        var cells = new List<(int item, int column)> ();
        list.DrawColumnHeader += (_, e) => headers.Add (e.ColumnIndex);
        list.DrawItem += (_, e) => { items.Add (e.ItemIndex); Assert.Same (list.Items[e.ItemIndex], e.Item); };
        list.DrawSubItem += (_, e) => cells.Add ((e.ItemIndex, e.ColumnIndex));

        using var _ = PaintSurface.Render (list);

        Assert.Equal ([0, 1], headers);
        Assert.Equal ([0, 1], items);
        // Item 0 has two sub-items, item 1 only its own text: upstream offers only the cells that exist.
        Assert.Equal ([(0, 0), (0, 1), (1, 0)], cells);
    }

    [Fact]
    public void DrawDefault_hands_a_part_back_to_the_built_in_painting ()
    {
        using var list = DetailsList ();
        var cells = 0;
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, _) => cells++;

        using var owner_default = PaintSurface.Render (list);
        Assert.Equal (0, cells);   // an item drawn by default is not offered cell by cell

        list.OwnerDraw = false;
        using var plain = PaintSurface.Render (list);

        // With every part handed back, the owner-drawn list paints exactly what the plain one does.
        var bounds = list.Items[0].Bounds;
        Assert.Equal (plain.GetPixel (bounds.Left + 10, bounds.Top + bounds.Height / 2), owner_default.GetPixel (bounds.Left + 10, bounds.Top + bounds.Height / 2));
    }

    [Fact]
    public void Without_OwnerDraw_nothing_is_offered ()
    {
        using var list = DetailsList ();
        list.OwnerDraw = false;
        var raised = 0;
        list.DrawColumnHeader += (_, _) => raised++;
        list.DrawItem += (_, _) => raised++;
        list.DrawSubItem += (_, _) => raised++;

        using var _ = PaintSurface.Render (list);

        Assert.Equal (0, raised);
    }

    // ── ListBox ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_owner_drawn_ListBox_offers_each_visible_item_with_its_state ()
    {
        HeadlessRenderer.Use ();
        using var box = new ListBox { Size = new Size (200, 100), DrawMode = DrawMode.OwnerDrawFixed };
        box.Items.Add ("a");
        box.Items.Add ("b");
        box.SelectedIndex = 1;
        var drawn = new List<(int index, bool selected)> ();
        box.DrawItem += (_, e) => drawn.Add ((e.Index, (e.State & DrawItemState.Selected) != 0));

        using var _ = PaintSurface.Render (box);

        Assert.Equal ([(0, false), (1, true)], drawn);
    }

    [Fact]
    public void OwnerDrawVariable_lays_the_rows_out_from_MeasureItem ()
    {
        HeadlessRenderer.Use ();
        using var box = new ListBox { Size = new Size (200, 200), DrawMode = DrawMode.OwnerDrawVariable };
        box.Items.Add ("a");
        box.Items.Add ("b");
        box.Items.Add ("c");
        var measured = new List<int> ();
        box.MeasureItem += (_, e) => { measured.Add (e.Index); e.ItemHeight = e.Index == 1 ? 50 : 20; };

        var first = box.GetItemRectangle (0);
        var second = box.GetItemRectangle (1);
        var third = box.GetItemRectangle (2);

        Assert.Equal ([0, 1, 2], measured);
        Assert.Equal (20, first.Height);
        Assert.Equal (50, second.Height);
        Assert.Equal (first.Bottom, second.Top);
        Assert.Equal (second.Bottom, third.Top);

        // Measured once, not on every read; RefreshItems asks again.
        box.GetItemRectangle (2);
        Assert.Equal (3, measured.Count);
        box.RefreshItems ();
        box.GetItemRectangle (0);
        Assert.Equal (6, measured.Count);
    }

    // ── ComboBox ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_owner_drawn_ComboBox_raises_DrawItem_for_the_edit_area_and_its_list ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var combo = new ComboBox { Bounds = new Rectangle (10, 10, 150, 24), DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed };
        combo.Items.Add ("x");
        combo.Items.Add ("y");
        combo.SelectedIndex = 1;
        form.Controls.Add (combo);
        var drawn = new List<(object? sender, int index, DrawItemState state)> ();
        combo.DrawItem += (s, e) => drawn.Add ((s, e.Index, e.State));
        var measured = 0;
        combo.MeasureItem += (_, _) => measured++;

        using var edit = PaintSurface.Render (combo);
        Assert.Single (drawn);
        Assert.Same (combo, drawn[0].sender);
        Assert.Equal (1, drawn[0].index);
        Assert.True ((drawn[0].state & DrawItemState.ComboBoxEdit) != 0);

        drawn.Clear ();
        combo.DrawMode = DrawMode.OwnerDrawVariable;
        combo.PopupListBox.Size = new Size (150, 100);
        using var list = PaintSurface.RenderOnForm (combo.PopupListBox);

        Assert.Equal ([0, 1], drawn.Where (d => (d.state & DrawItemState.ComboBoxEdit) == 0).Select (d => d.index));
        Assert.All (drawn, d => Assert.Same (combo, d.sender));
        Assert.Equal (2, measured);
        Assert.Equal (combo.PopupListBox.ItemHeight, combo.GetItemHeight (0));
    }

    // ── ToolTip ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_owner_drawn_ToolTip_raises_Draw_from_its_paint_pass_instead_of_drawing_text ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 24) };
        form.Controls.Add (button);
        form.Show ();
        // UseFading off: a fading tip starts transparent (W6 mechanisms), and this test reads ink.
        using var tip = new ToolTip { OwnerDraw = true, UseFading = false };
        DrawToolTipEventArgs? seen = null;
        tip.Draw += (_, e) => seen = e;

        tip.ShowItemTip (button, "hello", new Point (5, 5));

        // The popup is not laid out headlessly, so its docked label has no size; undock and size it
        // here so both paints below cover the same pixels.
        tip.PopupLabel!.Dock = DockStyle.None;
        tip.PopupLabel.Size = new Size (120, 30);
        using var owner = PaintSurface.Render (tip.PopupLabel);

        Assert.NotNull (seen);
        Assert.Equal ("hello", seen!.ToolTipText);
        Assert.Same (button, seen.AssociatedControl);
        Assert.Equal (new Size (tip.PopupLabel.ScaledWidth, tip.PopupLabel.ScaledHeight), seen.Bounds.Size);
        Assert.True (seen.Bounds.Width > 0);

        // The handler drew nothing, so the tip is blank; the built-in tip has ink.
        static int Ink (SkiaSharp.SKBitmap bitmap)
        {
            var background = bitmap.GetPixel (2, 2);
            var count = 0;
            for (var y = 2; y < bitmap.Height - 2; y++)
                for (var x = 2; x < bitmap.Width - 2; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        count++;
            return count;
        }

        tip.OwnerDraw = false;
        using var built_in = PaintSurface.Render (tip.PopupLabel);
        Assert.Equal (0, Ink (owner));
        Assert.True (Ink (built_in) > 0);
    }

    // ── StatusBar ───────────────────────────────────────────────────────────────────────────────────

    private static StatusBar Bar ()
    {
        HeadlessRenderer.Use ();
        var bar = new StatusBar { Size = new Size (600, 25), ShowPanels = true };
        bar.Panels.Add ("fixed");
        bar.Panels.Add ("spring").AutoSize = StatusBarPanelAutoSize.Spring;
        bar.Panels.Add ("fits").AutoSize = StatusBarPanelAutoSize.Contents;
        return bar;
    }

    [Fact]
    public void Panels_are_laid_out_fixed_then_contents_then_the_spring_takes_the_rest ()
    {
        using var bar = Bar ();
        bar.LayoutPanels ();

        var fixed_panel = bar.Panels[0].DeviceBounds;
        var spring = bar.Panels[1].DeviceBounds;
        var fits = bar.Panels[2].DeviceBounds;
        var area = bar.PaddedClientRectangle;

        Assert.Equal (bar.LogicalToDeviceUnits (100), fixed_panel.Width);
        Assert.Equal (fixed_panel.Right, spring.Left);
        Assert.Equal (spring.Right, fits.Left);
        Assert.True (fits.Width >= bar.LogicalToDeviceUnits (bar.Panels[2].MinWidth));
        Assert.Equal (area.Right - bar.ScaledGripWidth, fits.Right);
        Assert.Same (bar, bar.Panels[1].Parent);

        // Without the grip, the spring grows by the grip's square.
        bar.SizingGrip = false;
        bar.LayoutPanels ();
        Assert.Equal (area.Right, bar.Panels[2].DeviceBounds.Right);
        Assert.True (bar.Panels[1].DeviceBounds.Width > spring.Width);
    }

    [Fact]
    public void A_click_names_its_panel_and_the_panel_supplies_the_tip ()
    {
        using var bar = Bar ();
        bar.Panels[1].ToolTipText = "middle";
        StatusBarPanel? clicked = null;
        bar.PanelClick += (_, e) => clicked = e.StatusBarPanel;
        bar.LayoutPanels ();

        var spring = bar.Panels[1].DeviceBounds;
        var at = new Point (bar.DeviceToLogicalUnits (spring.Left + spring.Width / 2), bar.DeviceToLogicalUnits (spring.Top + spring.Height / 2));
        bar.RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));

        Assert.Same (bar.Panels[1], clicked);
        Assert.Equal ("middle", bar.GetToolTipText (at));

        bar.ShowPanels = false;
        clicked = null;
        bar.RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));
        Assert.Null (clicked);
    }

    [Fact]
    public void An_owner_drawn_panel_is_handed_to_DrawItem_with_its_rectangle ()
    {
        using var bar = Bar ();
        bar.Panels[0].Style = StatusBarPanelStyle.OwnerDraw;
        var drawn = new List<(StatusBarPanel panel, int index, Rectangle bounds)> ();
        bar.DrawItem += (_, e) => drawn.Add ((e.Panel, e.Index, e.Bounds));

        using var _ = PaintSurface.Render (bar);

        Assert.Single (drawn);
        Assert.Same (bar.Panels[0], drawn[0].panel);
        Assert.Equal (0, drawn[0].index);
        Assert.Equal (bar.Panels[0].DeviceBounds, drawn[0].bounds);
    }
}
