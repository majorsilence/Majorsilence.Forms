using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, tenth chunk: stored-only properties across the control set that now do what they
// say -- ListBox multi-column / horizontal scrolling / integral height / tab stops, TabControl
// Appearance and single-row scrolling, ToolTip icon / fade / ShowAlways, GroupBox.FlatStyle, the
// professional renderer's RoundedEdges, NavigationPaneItem.Padding, DataGridView cell tips and the
// editing pencil, DataError.Cancel, MessageBox icon / default button / options, UpDownBase.ChangingText,
// menu column breaks and DropDownDirection, pressed states, colour keys, image and control alignment,
// RTL mirroring and ListViewItem.IndentCount.
[Collection ("Headless")]
public class W6ControlFeaturesTests
{
    // ── ListBox ─────────────────────────────────────────────────────────────────────────────────────

    private sealed class KeyedListBox : ListBox
    {
        internal void Key (Keys key) => OnKeyUp (new KeyEventArgs (key));
    }

    private static KeyedListBox List (out Form form, int items, int width = 200, int height = 60)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 400, Height = 300 };
        var box = new KeyedListBox { Width = width, Height = height, IntegralHeight = false, ItemHeight = 20 };

        for (var i = 0; i < items; i++)
            box.Items.Add ($"Item {i}");

        form.Controls.Add (box);
        form.Show ();
        PaintSurface.RenderOnForm (box, 1f).Dispose ();
        return box;
    }

    [Fact]
    public void MultiColumn_lays_items_down_columns_and_Left_Right_cross_them ()
    {
        var box = List (out var form, items: 20, height: 100);

        using (form) {
            box.MultiColumn = true;
            box.ColumnWidth = 60;
            var rows = box.RowsPerColumn;

            Assert.True (rows >= 2, $"rows per column {rows}");
            Assert.Equal (box.GetItemRectangle (0).Top, box.GetItemRectangle (rows).Top);
            Assert.Equal (box.GetItemRectangle (0).Left + 60, box.GetItemRectangle (rows).Left);
            Assert.Equal (box.GetItemRectangle (0).Top + 20, box.GetItemRectangle (1).Top);

            box.SelectedIndex = 0;
            box.Key (Keys.Right);
            Assert.Equal (rows, box.SelectedIndex);
            box.Key (Keys.Down);
            Assert.Equal (rows + 1, box.SelectedIndex);
            box.Key (Keys.Left);
            Assert.Equal (1, box.SelectedIndex);

            // Scrolling by columns: the last item's column is scrolled into view, whole columns at a time.
            box.TopIndex = 19;
            Assert.Equal (0, box.TopIndex % rows);
            Assert.True (box.GetItemRectangle (19).Right <= box.DeviceToLogicalUnits (box.ItemsArea.Right), "the column was not scrolled into view");
        }
    }

    [Fact]
    public void HorizontalScrollbar_appears_for_a_wide_extent_and_scrolls_the_items ()
    {
        var box = List (out var form, items: 3);

        using (form) {
            HorizontalScrollBar Bar () => box.Controls.GetAllControls ().OfType<HorizontalScrollBar> ().Single ();

            // The bar's value and the offset are device pixels, so the check is against the device
            // rectangle; the public GetItemRectangle is the same thing in logical units.
            var at_rest = box.GetItemRectangleDevice (0).Left;

            box.HorizontalExtent = 600;
            Assert.False (Bar ().Visible);

            box.HorizontalScrollbar = true;
            Assert.True (Bar ().Visible);

            Bar ().Value = 50;
            Assert.Equal (at_rest - 50, box.GetItemRectangleDevice (0).Left);
            Assert.Equal (50, box.HorizontalOffset);
            Assert.Equal (box.DeviceToLogicalUnits (at_rest - 50), box.GetItemRectangle (0).Left);

            box.HorizontalScrollbar = false;
            Assert.False (Bar ().Visible);
            Assert.Equal (at_rest, box.GetItemRectangleDevice (0).Left);
        }
    }

    [Fact]
    public void IntegralHeight_snaps_the_height_to_whole_items ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var box = new ListBox { Width = 200, ItemHeight = 20, IntegralHeight = false };
        form.Controls.Add (box);
        form.Show ();

        box.Height = 95;
        Assert.Equal (95, box.Height);

        box.IntegralHeight = true;
        var chrome = box.Height - box.DeviceToLogicalUnits (box.ClientRectangle.Height);
        Assert.Equal (0, (box.Height - chrome) % 20);
        Assert.InRange (box.Height, 95 - 19, 95);

        box.Height = 115;
        Assert.Equal (0, (box.Height - chrome) % 20);
        Assert.InRange (box.Height, 115 - 19, 115);
    }

    [Fact]
    public void UseTabStops_expands_tabs_and_CustomTabOffsets_place_them ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var box = new ListBox { Width = 300, Height = 60, IntegralHeight = false };
        box.Items.Add ("\tX");
        form.Controls.Add (box);
        form.Show ();

        // LeftmostInk answers in device pixels.
        var plain = LeftmostInk (box);

        box.UseTabStops = false;
        var unexpanded = LeftmostInk (box);
        Assert.True (plain > unexpanded + box.LogicalToDeviceUnits (20), $"tab stop at {plain}, unexpanded at {unexpanded}");

        box.UseTabStops = true;
        box.UseCustomTabOffsets = true;
        box.CustomTabOffsets.Add (150);
        var custom = LeftmostInk (box);
        Assert.InRange (custom, box.LogicalToDeviceUnits (150), box.LogicalToDeviceUnits (170));
    }

    [Fact]
    public void IndexFromPoint_is_the_real_hit_test ()
    {
        var box = List (out var form, items: 20);

        using (form) {
            box.TopIndex = 2;
            Assert.Equal (2, box.IndexFromPoint (5, 2));
            Assert.Equal (3, box.IndexFromPoint (new Point (5, 22)));
        }
    }

    // ── TabControl ──────────────────────────────────────────────────────────────────────────────────

    private static TabStrip StripOf (TabControl tc)
        => (TabStrip) typeof (TabControl).GetField ("tab_strip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue (tc)!;

    [Fact]
    public void A_single_row_strip_scrolls_its_overflow_and_the_selected_tab_into_view ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 420, Height = 400 };
        var tc = new TabControl { Width = 400, Height = 300 };
        for (var i = 0; i < 12; i++)
            tc.TabPages.Add (new TabPage ($"Long Page Caption {i}"));
        form.Controls.Add (tc);
        form.Show ();
        PaintSurface.RenderOnForm (tc, 1f).Dispose ();

        var strip = StripOf (tc);
        Assert.False (tc.Multiline);
        Assert.Equal (1, tc.RowCount);
        Assert.True (strip.TabsOverflow);
        Assert.True (tc.GetTabRect (11).Right > 400, "the last tab should start off the right edge");
        Assert.Equal (0, strip.ScrollOffset);

        tc.SelectedIndex = 11;
        Assert.True (strip.ScrollOffset > 0);
        Assert.True (tc.GetTabRect (11).Right <= 400 - TabStrip.ScrollArrowBandWidth, $"tab 11 at {tc.GetTabRect (11)}");
        Assert.True (tc.GetTabRect (0).Left < 0, "the first tab should have scrolled off the left");

        tc.SelectedIndex = 0;
        Assert.Equal (0, strip.ScrollOffset);
    }

    [Fact]
    public void Appearance_Buttons_draws_a_frame_and_no_underline ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 420, Height = 300 };
        var tc = new TabControl { Width = 300, Height = 150 };
        tc.TabPages.Add (new TabPage ("One"));
        tc.TabPages.Add (new TabPage ("Two"));
        form.Controls.Add (tc);
        form.Show ();

        // GetTabRect is logical; the bitmap is device (RC-8).
        var selected = tc.LogicalToDeviceUnits (tc.GetTabRect (0));
        var other = tc.LogicalToDeviceUnits (tc.GetTabRect (1));
        var inset = tc.LogicalToDeviceUnits (1);

        using (var normal = PaintSurface.RenderOnForm (tc))
            Assert.True (CountIn (normal, selected, Theme.AccentColor2) > 0, "Normal draws the accent underline");

        tc.Appearance = TabAppearance.Buttons;

        using (var buttons = PaintSurface.RenderOnForm (tc)) {
            Assert.Equal (0, CountIn (buttons, selected, Theme.AccentColor2));
            Assert.Equal (Theme.ControlHighColor, buttons.GetPixel (other.Left + inset, other.Top + inset));
        }

        tc.Appearance = TabAppearance.FlatButtons;

        using (var flat = PaintSurface.RenderOnForm (tc)) {
            Assert.Equal (0, CountIn (flat, selected, Theme.AccentColor2));
            Assert.NotEqual (Theme.ControlHighColor, flat.GetPixel (other.Left + inset, other.Top + inset));
        }
    }

    // ── ToolTip ─────────────────────────────────────────────────────────────────────────────────────

    private static (Form form, Button button) TipHost ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (300, 200) };
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 24) };
        form.Controls.Add (button);
        form.Show ();
        return (form, button);
    }

    private static SKBitmap RenderTip (ToolTip tip)
    {
        tip.PopupLabel!.Dock = DockStyle.None;
        tip.PopupLabel.Size = new Size (140, 30);
        return PaintSurface.Render (tip.PopupLabel);
    }

    [Fact]
    public void ToolTipIcon_draws_a_glyph_at_the_leading_edge ()
    {
        var (form, button) = TipHost ();

        using (form) {
            using var tip = new ToolTip { UseFading = false, ToolTipIcon = ToolTipIcon.Info };
            tip.ShowItemTip (button, "hello", new Point (5, 5));

            using var with_icon = RenderTip (tip);
            var glyph = new Rectangle (0, 0, 20, with_icon.Height);
            Assert.True (CountIn (with_icon, glyph, MessageGlyphs.InformationColor) > 0, "no information glyph in the leading band");

            tip.ToolTipIcon = ToolTipIcon.None;
            tip.ShowItemTip (button, "hello again", new Point (5, 5));

            using var without = RenderTip (tip);
            Assert.Equal (0, CountIn (without, glyph, MessageGlyphs.InformationColor));
        }
    }

    [Fact]
    public void UseFading_starts_the_tip_transparent_and_the_fade_brings_it_in ()
    {
        var (form, button) = TipHost ();

        using (form) {
            using var tip = new ToolTip ();
            tip.ShowItemTip (button, "hello", new Point (5, 5));

            Assert.Equal (0, tip.TipAlpha);
            using (var faded = RenderTip (tip))
                Assert.Equal (0, Ink (faded));

            for (var i = 0; i < 5; i++)
                tip.AdvanceFade ();

            Assert.Equal (255, tip.TipAlpha);
            using (var opaque = RenderTip (tip))
                Assert.True (Ink (opaque) > 0);

            tip.UseAnimation = false;
            tip.ShowItemTip (button, "at once", new Point (5, 5));
            Assert.Equal (255, tip.TipAlpha);
        }
    }

    [Fact]
    public void ShowAlways_off_shows_no_tip_for_an_inactive_form ()
    {
        var (form, button) = TipHost ();

        using (form) {
            using var other = new Form { Size = new Size (100, 100) };
            other.Show ();   // the active form is the one shown last
            Assert.Same (other, Form.ActiveForm);

            using var tip = new ToolTip { UseFading = false };
            tip.ShowItemTip (button, "hidden", new Point (5, 5));
            Assert.Null (tip.PopupText);

            tip.ShowAlways = true;
            tip.ShowItemTip (button, "shown", new Point (5, 5));
            Assert.Equal ("shown", tip.PopupText);
        }
    }

    // ── GroupBox, professional renderer, navigation pane ────────────────────────────────────────────

    [Fact]
    public void GroupBox_FlatStyle_Flat_drops_the_etched_highlight_line ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var group = new GroupBox { Width = 120, Height = 80, Text = "Box" };
        form.Controls.Add (group);
        form.Show ();

        // The highlight line sits one device pixel inside the frame, which starts one pixel in.
        var y = group.LogicalToDeviceUnits (group.CaptionHeight) / 2 + 1;

        using (var standard = PaintSurface.RenderOnForm (group))
            Assert.Equal (Theme.ControlHighColor, standard.GetPixel (2, y));

        group.FlatStyle = FlatStyle.Flat;

        using (var flat = PaintSurface.RenderOnForm (group))
            Assert.NotEqual (Theme.ControlHighColor, flat.GetPixel (2, y));
    }

    [Fact]
    public void RoundedEdges_cuts_the_corners_of_the_professional_strip_border ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var renderer = new ToolStripProfessionalRenderer { RoundedEdges = false };
        var strip = new ToolStrip { Width = 200, Height = 30, Dock = DockStyle.None, Renderer = renderer };
        strip.Items.Add (new ToolStripButton ("A"));
        form.Controls.Add (strip);
        form.Show ();

        var border = ToSK (renderer.ColorTable.ToolStripBorder);

        // The renderer's border runs along the bottom of the client rectangle, inside the control's
        // own frame.
        var client = strip.ClientRectangle;
        var bottom = client.Bottom - 1;

        using (var square = Strip (strip)) {
            Assert.Equal (border, square.GetPixel (client.Right - 1, bottom));
            Assert.Equal (border, square.GetPixel (client.Left, bottom));
        }

        renderer.RoundedEdges = true;

        using (var rounded = Strip (strip)) {
            Assert.NotEqual (border, rounded.GetPixel (client.Right - 1, bottom));
            Assert.NotEqual (border, rounded.GetPixel (client.Left, bottom));
            Assert.Equal (border, rounded.GetPixel (client.Left + client.Width / 2, bottom));
        }
    }

    [Fact]
    public void NavigationPaneItem_Padding_insets_the_text_and_adds_to_the_height ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 320 };
        var pane = new NavigationPane { Width = 120, Height = 200 };
        var item = new NavigationPaneItem (new SKBitmap (16, 16), "One") { Padding = Padding.Empty };
        pane.Items.Add (item);
        form.Controls.Add (pane);
        form.Show ();
        PaintSurface.Render (pane).Dispose ();

        var height = item.Bounds.Height;
        var centred = InkMeanX (PaintSurface.Render (pane), pane.LogicalToDeviceUnits (item.Bounds));

        item.Padding = new Padding (60, 0, 0, 0);
        PaintSurface.Render (pane).Dispose ();
        var pushed = InkMeanX (PaintSurface.Render (pane), pane.LogicalToDeviceUnits (item.Bounds));
        Assert.True (pushed > centred + 20, $"text centre moved from {centred} to {pushed}");

        item.Padding = new Padding (0, 10, 0, 10);
        PaintSurface.Render (pane).Dispose ();
        Assert.Equal (height + 20, item.Bounds.Height);
    }

    // ── DataGridView ────────────────────────────────────────────────────────────────────────────────

    private sealed class EditableGrid : DataGridView
    {
    }

    private static EditableGrid Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 520, Height = 340 };
        var grid = new EditableGrid { Width = 400, Height = 200 };

        for (var c = 0; c < 2; c++)
            grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Width = 80 });

        for (var r = 0; r < 3; r++) {
            var row = new DataGridViewRow ();
            for (var c = 0; c < 2; c++)
                row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });
            grid.Rows.Add (row);
        }

        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.RenderOnForm (grid, 1f).Dispose ();
        grid.SelectedRowIndex = 0;
        grid.SelectedColumnIndex = 0;
        return grid;
    }

    private static Point Centre (DataGridView grid, int row, int column)
    {
        var b = grid.GetCellBounds (row, column);
        return grid.DeviceToLogicalUnits (new Point (b.Left + b.Width / 2, b.Top + b.Height / 2));
    }

    [Fact]
    public void ShowCellToolTips_and_CellToolTipTextNeeded_supply_the_cell_tip ()
    {
        var grid = Grid (out var form);

        using (form) {
            var at = Centre (grid, 1, 1);
            Assert.Null (grid.GetToolTipText (at));

            grid.Columns[1].ToolTipText = "column";
            Assert.Equal ("column", grid.GetToolTipText (at));

            grid.Rows[1].Cells[1].ToolTipText = "cell";
            Assert.Equal ("cell", grid.GetToolTipText (at));

            grid.CellToolTipTextNeeded += (_, e) => e.ToolTipText = $"needed {e.RowIndex},{e.ColumnIndex} was {e.ToolTipText}";
            Assert.Equal ("needed 1,1 was cell", grid.GetToolTipText (at));

            grid.ShowCellToolTips = false;
            Assert.Null (grid.GetToolTipText (at));
        }
    }

    [Fact]
    public void ShowEditingIcon_draws_the_pencil_on_the_row_being_edited ()
    {
        var grid = Grid (out var form);

        using (form) {
            var header = new Rectangle (grid.GetContentArea ().Left, grid.Rows[0].Bounds.Top, grid.ScaledRowHeadersWidth, grid.Rows[0].Bounds.Height);
            var box = DataGridViewRenderer.EditingPencilBox (header, grid.LogicalToDeviceUnits (8));
            var ink = DataGridView.DefaultRowHeaderStyle.GetForegroundColor ();

            Assert.True (grid.BeginEdit (true));

            using (var editing = PaintSurface.RenderOnForm (grid))
                Assert.Equal (ink, editing.GetPixel (box.Right - 1, box.Top));

            grid.ShowEditingIcon = false;

            using (var plain = PaintSurface.RenderOnForm (grid))
                Assert.NotEqual (ink, plain.GetPixel (box.Right - 1, box.Top));
        }
    }

    [Fact]
    public void A_DataError_handler_that_clears_Cancel_abandons_the_edit ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.Columns[0].ValueType = typeof (int);
            var seen = new List<bool> ();
            grid.DataError += (_, e) => { seen.Add (e.Cancel); e.Cancel = false; };

            grid.BeginEdit (true);
            grid.Controls.OfType<TextBox> ().First ().Text = "not a number";

            Assert.False (grid.EndEdit ());
            Assert.True (Assert.Single (seen), "the grid raises DataError with Cancel set");
            Assert.False (grid.IsCurrentCellInEditMode);
            Assert.Equal ("r0c0", grid.Rows[0].Cells[0].Value);
        }
    }

    // ── MessageBox, spinners ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MessageBoxForm_honours_the_icon_the_default_button_and_the_options ()
    {
        HeadlessRenderer.Use ();
        using var box = new MessageBoxForm ("Title", "Save changes?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        Assert.Equal (MessageGlyph.Question, box.Glyph);
        Assert.Equal ("No", ((Button) box.AcceptButton!).Text);
        Assert.Equal (ContentAlignment.TopRight, box.Controls.GetAllControls ().OfType<Label> ().First ().TextAlign);
        Assert.Equal (RightToLeft.Yes, box.RightToLeft);

        using var plain = new MessageBoxForm ("Title", "Done", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button3, MessageBoxOptions.None);
        Assert.Null (plain.Glyph);
        Assert.Equal ("OK", ((Button) plain.AcceptButton!).Text);   // no third button: falls back to the first
    }

    private sealed class WatchingSpinner : NumericUpDown
    {
        internal readonly List<bool> Changing = new ();
        protected override void OnTextChanged (EventArgs e) { Changing.Add (ChangingText); base.OnTextChanged (e); }
    }

    private sealed class WatchingDomain : DomainUpDown
    {
        internal readonly List<bool> Changing = new ();
        protected override void OnTextChanged (EventArgs e) { Changing.Add (ChangingText); base.OnTextChanged (e); }
    }

    [Fact]
    public void ChangingText_is_set_while_a_spinner_writes_its_own_text ()
    {
        using var spinner = new WatchingSpinner ();
        spinner.Value = 5;
        Assert.Contains (true, spinner.Changing);
        Assert.Equal ("5", spinner.Text);

        spinner.Changing.Clear ();
        spinner.Text = "typed";
        Assert.Equal (new[] { false }, spinner.Changing);

        using var domain = new WatchingDomain ();
        domain.Items.Add ("alpha");
        domain.SelectedIndex = 0;
        Assert.Contains (true, domain.Changing);
        Assert.Equal ("alpha", domain.Text);
    }

    // ── Menus ───────────────────────────────────────────────────────────────────────────────────────

    private static ToolStripDropDownButton DropDownButton (out Form form, out ToolStrip strip, int items = 4)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 500, Height = 400 };
        strip = new ToolStrip { Width = 400, Height = 30 };
        var button = new ToolStripDropDownButton ("File");

        for (var i = 0; i < items; i++)
            button.DropDownItems.Add (new ToolStripMenuItem ($"Item {i}"));

        strip.Items.Add (button);
        form.Controls.Add (strip);
        form.Show ();
        Strip (strip).Dispose ();
        return button;
    }

    [Fact]
    public void Break_and_BarBreak_start_new_columns_in_a_drop_down ()
    {
        var button = DropDownButton (out var form, out _);

        using (form) {
            var items = button.DropDownItems.Cast<MenuItem> ().ToList ();

            button.ShowDropDown ();
            var open = button.OpenDropDown!;
            Assert.Equal (items[0].Bounds.Left, items[2].Bounds.Left);
            Assert.Empty (open.ColumnBars);
            button.HideDropDown ();

            ((ToolStripMenuItem) items[2]).Break = true;
            button.ShowDropDown ();
            Assert.Equal (items[0].Bounds.Top, items[2].Bounds.Top);
            Assert.True (items[2].Bounds.Left > items[0].Bounds.Right - 1, $"third item at {items[2].Bounds}, first at {items[0].Bounds}");
            Assert.Equal (items[2].Bounds.Left, items[3].Bounds.Left);
            Assert.Empty (open.ColumnBars);
            button.HideDropDown ();

            ((ToolStripMenuItem) items[2]).Break = false;
            ((ToolStripMenuItem) items[2]).BarBreak = true;
            button.ShowDropDown ();
            var bar = Assert.Single (open.ColumnBars);
            Assert.InRange (bar, items[0].Bounds.Right, items[2].Bounds.Left);
            button.HideDropDown ();
        }
    }

    [Fact]
    public void DropDownDirection_places_the_drop_down_on_the_named_side ()
    {
        var button = DropDownButton (out var form, out var strip);

        using (form) {
            button.ShowDropDown ();
            var below = button.OpenDropDown!.LastShowLocation;
            var size = button.OpenDropDown!.PreferredPopupSize;
            button.HideDropDown ();

            button.DropDownDirection = ToolStripDropDownDirection.Left;
            button.ShowDropDown ();
            var left = button.OpenDropDown!.LastShowLocation;
            button.HideDropDown ();

            // Screen points are device; the item's Bounds and the popup size are logical (RC-8).
            Assert.Equal (below.X - strip.LogicalToDeviceUnits (1 + size.Width), left.X);
            Assert.True (left.Y < below.Y);

            button.DropDownDirection = ToolStripDropDownDirection.Default;
            strip.DefaultDropDownDirection = ToolStripDropDownDirection.AboveRight;
            button.ShowDropDown ();
            var above = button.OpenDropDown!.LastShowLocation;
            button.HideDropDown ();

            Assert.Equal (below.X, above.X);
            Assert.Equal (below.Y - strip.LogicalToDeviceUnits (button.Bounds.Height + size.Height), above.Y);
        }
    }

    // ── Pressed states ──────────────────────────────────────────────────────────────────────────────

    private sealed class HoverButton : Button
    {
        internal void Hover () => OnMouseEnter (EventArgs.Empty);
    }

    [Fact]
    public void MouseDownBackColor_paints_while_the_flat_button_is_held ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var button = new HoverButton { Bounds = new Rectangle (10, 10, 80, 24), FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.MouseDownBackColor = Color.Red;
        form.Controls.Add (button);
        form.Show ();

        button.Hover ();
        Assert.NotEqual (SKColors.Red, button.CurrentStyle.BackgroundColor);

        button.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, Point.Empty));
        Assert.True (button.IsPressed);
        Assert.Equal (SKColors.Red, button.CurrentStyle.BackgroundColor);

        button.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, Point.Empty));
        Assert.False (button.IsPressed);
        Assert.NotEqual (SKColors.Red, button.CurrentStyle.BackgroundColor);
    }

    // ── Images ──────────────────────────────────────────────────────────────────────────────────────

    private static SKBitmap HalfRedHalfMagenta ()
    {
        var bitmap = new SKBitmap (16, 16, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
                bitmap.SetPixel (x, y, x < 8 ? SKColors.Red : SKColors.Magenta);

        return bitmap;
    }

    [Fact]
    public void TransparentColor_keys_images_added_to_an_ImageList ()
    {
        using var plain = new ImageList ();
        plain.Images.Add (HalfRedHalfMagenta ());
        Assert.Equal (255, plain.Images[0].GetPixel (12, 8).Alpha);

        using var keyed = new ImageList { TransparentColor = Color.Magenta };
        keyed.Images.Add (HalfRedHalfMagenta ());
        Assert.Equal (0, keyed.Images[0].GetPixel (12, 8).Alpha);
        Assert.Equal (SKColors.Red, keyed.Images[0].GetPixel (3, 8));
    }

    private static ToolStripButton ImagedItem (out Form form, out ToolStrip strip, int stripHeight = 30)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 400, Height = 200 };
        strip = new ToolStrip { Width = 300, Height = stripHeight };
        var item = new ToolStripButton { ImageScaling = ToolStripItemImageScaling.None, DisplayStyle = ToolStripItemDisplayStyle.Image };
        ((MenuItem) item).SetImageSK (HalfRedHalfMagenta ());
        strip.Items.Add (item);
        form.Controls.Add (strip);
        form.Show ();
        PaintSurface.Render (strip).Dispose ();
        return item;
    }

    // The strip rendered by itself at its own scale, so item.DeviceBounds addresses the bitmap: a
    // strip docked in a headless form renders 0 wide through RenderOnForm, so the strip tests draw
    // the control directly, as the link tests do.
    private static SKBitmap Strip (ToolStrip strip) => PaintSurface.Render (strip);

    [Fact]
    public void ImageTransparentColor_and_RightToLeftAutoMirrorImage_change_the_drawn_image ()
    {
        var item = ImagedItem (out var form, out var strip);

        using (form) {
            using (var plain = Strip (strip)) {
                Assert.True (CountIn (plain, item.DeviceBounds, SKColors.Magenta) > 0, "the magenta half should be drawn");
                Assert.True (CountIn (plain, item.DeviceBounds, SKColors.Red) > 0);
            }

            item.ImageTransparentColor = Color.Magenta;

            using (var keyed = Strip (strip))
                Assert.Equal (0, CountIn (keyed, item.DeviceBounds, SKColors.Magenta));

            var red_x = MeanX (Strip (strip), item.DeviceBounds, SKColors.Red);

            item.RightToLeftAutoMirrorImage = true;
            strip.RightToLeft = RightToLeft.Yes;

            var mirrored_x = MeanX (Strip (strip), item.DeviceBounds, SKColors.Red);
            Assert.True (mirrored_x > red_x + 4, $"red half at {red_x}, mirrored at {mirrored_x}");
        }
    }

    [Fact]
    public void ImageAlign_moves_the_image_within_the_item ()
    {
        var item = ImagedItem (out var form, out var strip, stripHeight: 70);

        using (form) {
            item.ImageAlign = ContentAlignment.TopLeft;
            var top = MeanY (Strip (strip), item.DeviceBounds, SKColors.Red);

            item.ImageAlign = ContentAlignment.BottomLeft;
            var bottom = MeanY (Strip (strip), item.DeviceBounds, SKColors.Red);

            Assert.True (bottom > top + 10, $"top-aligned at {top}, bottom-aligned at {bottom}");
        }
    }

    [Fact]
    public void ControlAlign_places_a_short_hosted_control_in_its_box ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { ClientSize = new Size (400, 200) };
        var strip = new StatusStrip ();
        var hosted = new Button { Size = new Size (90, 12) };
        var host = new ToolStripControlHost (hosted) { ControlAlign = ContentAlignment.TopLeft };
        strip.Items.Add (host);
        form.Controls.Add (strip);
        form.Show ();
        Strip (strip).Dispose ();

        Assert.Equal (12, hosted.Height);
        Assert.Equal (host.Bounds.Top, hosted.Top);

        host.ControlAlign = ContentAlignment.BottomLeft;
        Assert.Equal (host.Bounds.Bottom, hosted.Bottom);
    }

    // ── ListView ────────────────────────────────────────────────────────────────────────────────────

    private static ListView Details (out Form form)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 400, Height = 300 };
        var list = new ListView { View = View.Details, Width = 300, Height = 150 };
        list.Columns.Add ("A", 100);
        list.Columns.Add ("B", 80);
        list.Items.Add (new ListViewItem (new[] { "one", "two" }));
        form.Controls.Add (list);
        form.Show ();
        PaintSurface.RenderOnForm (list).Dispose ();
        return list;
    }

    [Fact]
    public void RightToLeftLayout_mirrors_the_Details_columns ()
    {
        var list = Details (out var form);

        using (form) {
            // ItemArea and ColumnIndexAt are device; the column widths are logical (RC-8).
            var area = list.ItemArea;
            int D (int logical) => list.LogicalToDeviceUnits (logical);
            Assert.Equal (0, list.ColumnIndexAt (area.Left + D (10)));
            Assert.Equal (1, list.ColumnIndexAt (area.Left + D (110)));

            var raised = 0;
            list.RightToLeftLayoutChanged += (_, _) => raised++;
            list.RightToLeftLayout = true;
            Assert.Equal (1, raised);
            Assert.False (list.MirrorsColumns);   // RightToLeft itself is still No

            list.RightToLeft = RightToLeft.Yes;
            Assert.True (list.MirrorsColumns);
            Assert.Equal (0, list.ColumnIndexAt (area.Right - D (10)));
            Assert.Equal (1, list.ColumnIndexAt (area.Right - D (110)));
            Assert.Equal (-1, list.ColumnIndexAt (area.Left + D (10)));

            PaintSurface.RenderOnForm (list, 1f).Dispose ();   // the mirrored layout paints
        }
    }

    [Fact]
    public void IndentCount_indents_the_first_cell ()
    {
        var list = Details (out var form);

        using (form) {
            var item = list.Items[0];
            var plain = LeftmostInk (PaintSurface.RenderOnForm (list), item.DeviceBounds);

            item.IndentCount = 2;
            var indented = LeftmostInk (PaintSurface.RenderOnForm (list), item.DeviceBounds);

            Assert.Equal (plain + 2 * list.ScaledIndentUnit, indented);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static SKColor ToSK (Color c) => new SKColor (c.R, c.G, c.B, c.A);

    private static int CountIn (SKBitmap bitmap, Rectangle area, SKColor colour)
    {
        var count = 0;

        for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++) {
                var p = bitmap.GetPixel (x, y);

                if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue && p.Alpha == colour.Alpha)
                    count++;
            }

        return count;
    }

    private static double MeanX (SKBitmap bitmap, Rectangle area, SKColor colour)
    {
        using (bitmap) {
            double sum = 0;
            var n = 0;

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue) { sum += x; n++; }
                }

            Assert.True (n > 0, "no pixels of the colour in the area");
            return sum / n;
        }
    }

    private static double MeanY (SKBitmap bitmap, Rectangle area, SKColor colour)
    {
        using (bitmap) {
            double sum = 0;
            var n = 0;

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue) { sum += y; n++; }
                }

            Assert.True (n > 0, "no pixels of the colour in the area");
            return sum / n;
        }
    }

    // Ink is anything that differs from the pixel at the area's top-left corner.
    private static int Ink (SKBitmap bitmap)
    {
        var background = bitmap.GetPixel (2, 2);
        var count = 0;

        for (var y = 2; y < bitmap.Height - 2; y++)
            for (var x = 2; x < bitmap.Width - 2; x++)
                if (bitmap.GetPixel (x, y) != background)
                    count++;

        return count;
    }

    private static int LeftmostInk (ListBox box)
    {
        using var bitmap = PaintSurface.RenderOnForm (box);
        return LeftmostInk (bitmap, box.GetItemRectangleDevice (0));
    }

    private static int LeftmostInk (SKBitmap bitmap, Rectangle area)
    {
        using (bitmap) {
            var background = bitmap.GetPixel (area.Left + 1, area.Top + 1);

            for (var x = Math.Max (0, area.Left + 1); x < Math.Min (bitmap.Width, area.Right - 1); x++)
                for (var y = Math.Max (0, area.Top + 1); y < Math.Min (bitmap.Height, area.Bottom - 1); y++)
                    if (bitmap.GetPixel (x, y) != background)
                        return x;

            return -1;
        }
    }

    private static double InkMeanX (SKBitmap bitmap, Rectangle area)
    {
        using (bitmap) {
            var background = bitmap.GetPixel (area.Left + 1, area.Top + 1);
            double sum = 0;
            var n = 0;

            for (var y = Math.Max (0, area.Top + 1); y < Math.Min (bitmap.Height, area.Bottom - 1); y++)
                for (var x = Math.Max (0, area.Left + 1); x < Math.Min (bitmap.Width, area.Right - 1); x++)
                    if (bitmap.GetPixel (x, y) != background) { sum += x; n++; }

            Assert.True (n > 0, "no ink in the area");
            return sum / n;
        }
    }
}
