using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6.2 sweep: stored-only properties that had a consumer within reach. One test per property, driving
/// the public path that should read it.
/// </summary>
public class StoredOnlySweepTests
{
    [Fact]
    public void TimePicker_Value_is_clamped_to_MinValue_and_MaxValue ()
    {
        using var picker = new TimePicker { MinValue = new DateTime (2000, 1, 1, 9, 0, 0), MaxValue = new DateTime (2000, 1, 1, 17, 0, 0) };

        picker.Value = new DateTime (2000, 1, 1, 6, 0, 0);
        Assert.Equal (9, picker.Value!.Value.Hour);
        picker.Value = new DateTime (2000, 1, 1, 22, 30, 0);
        Assert.Equal (17, picker.Value!.Value.Hour);
        picker.Value = new DateTime (2000, 1, 1, 12, 0, 0);
        Assert.Equal (12, picker.Value!.Value.Hour);
    }

    [Fact]
    public void ToolStrip_AutoToolTip_falls_back_to_the_items_Text ()
    {
        using var strip = new ToolStrip { Size = new Size (300, 30), ShowItemToolTips = true };
        var item = new ToolStripButton ("Save") { AutoToolTip = true };
        strip.Items.Add (item);
        using var bitmap = PaintSurface.Render (strip); // lays the item out

        var inside = item.DeviceBounds;
        var at = new Point (inside.Left + inside.Width / 2, inside.Top + inside.Height / 2);

        Assert.Equal ("Save", strip.GetToolTipText (at));
        item.AutoToolTip = false;
        Assert.Null (strip.GetToolTipText (at));
        item.ToolTipText = "Save the file";
        Assert.Equal ("Save the file", strip.GetToolTipText (at));
    }

    [Fact]
    public void ShowCheckMargin_keeps_the_gutter_when_the_image_margin_is_off ()
    {
        using var menu = new ContextMenuStrip { ShowImageMargin = false };
        Assert.False (Renderers.MenuDropDownRenderer.ShowsImageMargin (menu));
        menu.ShowCheckMargin = true;
        Assert.True (Renderers.MenuDropDownRenderer.ShowsImageMargin (menu));

        using var drop = new ToolStripDropDownMenu { ShowImageMargin = false, ShowCheckMargin = true };
        Assert.True (Renderers.MenuDropDownRenderer.ShowsImageMargin (drop));
    }

    [Fact]
    public void ToolStripStatusLabel_draws_only_the_border_sides_it_is_given ()
    {
        HeadlessRenderer.Use ();
        using var strip = new StatusStrip { Size = new Size (300, 26), BackColor = Color.White };
        var label = new ToolStripStatusLabel { Text = "Ready", BorderStyle = Border3DStyle.Etched, BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Right };
        strip.Items.Add (label);

        using var with = PaintSurface.Render (strip);
        var b = label.DeviceBounds;
        var left = with.GetPixel (b.Left, b.Top + b.Height / 2);
        var top = with.GetPixel (b.Left + b.Width / 2, b.Top);

        label.BorderSides = ToolStripStatusLabelBorderSides.None;
        using var without = PaintSurface.Render (strip);
        var left_off = without.GetPixel (b.Left, b.Top + b.Height / 2);

        Assert.NotEqual (left_off, left);      // the left edge is a border line
        Assert.Equal (without.GetPixel (b.Left + b.Width / 2, b.Top), top); // the top was never asked for
    }

    private static (ToolTip tip, Button button, Form form) TipOnAButton ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (300, 200) };
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 24) };
        form.Controls.Add (button);
        form.Show ();
        return (new ToolTip (), button, form);
    }

    [Fact]
    public void ToolTip_StripAmpersands_and_ToolTipTitle_shape_the_shown_text ()
    {
        var (tip, button, form) = TipOnAButton ();
        using (form) using (tip) {
            tip.ShowItemTip (button, "&Save && exit", new Point (5, 5));
            Assert.Equal ("&Save && exit", tip.PopupText);

            tip.StripAmpersands = true;
            tip.ShowItemTip (button, "&Save && exit", new Point (5, 5));
            Assert.Equal ("Save & exit", tip.PopupText);

            tip.ToolTipTitle = "Hint";
            tip.ShowItemTip (button, "text", new Point (5, 5));
            Assert.Equal ("Hint" + Environment.NewLine + "text", tip.PopupText);
        }
    }

    [Fact]
    public void ToolTip_Popup_handler_can_resize_the_tip ()
    {
        var (tip, button, form) = TipOnAButton ();
        using (form) using (tip) {
            tip.Popup += (_, e) => e.ToolTipSize = new Size (222, 44);
            tip.ShowItemTip (button, "hello", new Point (5, 5));
            Assert.Equal (new Size (222, 44), tip.PopupSize);
        }
    }

    [Fact]
    public void Control_MouseButtons_reports_the_buttons_currently_held ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        form.Show ();

        form.HandlePointerPressed (MouseButtons.Left, 50, 50, Keys.None);
        Assert.Equal (MouseButtons.Left, Control.MouseButtons & MouseButtons.Left);
        form.HandlePointerPressed (MouseButtons.Right, 50, 50, Keys.None);
        Assert.Equal (MouseButtons.Left | MouseButtons.Right, Control.MouseButtons & (MouseButtons.Left | MouseButtons.Right));
        form.HandlePointerReleased (MouseButtons.Left, 50, 50, Keys.None);
        form.HandlePointerReleased (MouseButtons.Right, 50, 50, Keys.None);
        Assert.Equal (MouseButtons.None, Control.MouseButtons & (MouseButtons.Left | MouseButtons.Right));
    }

    [Fact]
    public void ScrollableControl_HScroll_and_VScroll_follow_the_scrollbars_shown ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 300) };
        var panel = new Panel { AutoScroll = true, Bounds = new Rectangle (0, 0, 100, 100) };
        form.Controls.Add (panel);
        panel.Controls.Add (new Button { Bounds = new Rectangle (0, 0, 20, 300) }); // taller than the panel
        form.Show ();
        panel.PerformLayout ();
        using var bitmap = PaintSurface.Render (panel);

        Assert.True (panel.VScroll);
        Assert.False (panel.HScroll);
    }

    [Fact]
    public void ControlBindingsCollection_applies_its_DefaultDataSourceUpdateMode_to_new_bindings ()
    {
        using var box = new TextBox ();
        box.DataBindings.DefaultDataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged;

        var binding = box.DataBindings.Add ("Text", new { Name = "n" }, "Name");

        Assert.Equal (DataSourceUpdateMode.OnPropertyChanged, binding.DataSourceUpdateMode);
    }

    private static DataGridView ComboGrid (DataGridViewComboBoxColumn column)
    {
        var grid = new DataGridView { Size = new Size (300, 120), ColumnHeadersVisible = false, RowHeadersVisible = false };
        column.Items.AddRange ("a", "b");
        grid.Columns.Add (column);
        grid.Rows.Add ("a");
        return grid;
    }

    [Fact]
    public void DataGridViewComboBoxColumn_DisplayStyle_Nothing_hides_the_drop_down_button ()
    {
        HeadlessRenderer.Use ();
        var column = new DataGridViewComboBoxColumn { Width = 200 };
        using var grid = ComboGrid (column);
        using var with = PaintSurface.Render (grid);
        var cell = grid.GetCellDisplayRectangle (0, 0, false);

        column.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
        using var without = PaintSurface.Render (grid);

        // The arrow lives in the right-hand band of the cell; where exactly its strokes fall depends on
        // the platform's font metrics (a single probe pixel missed on Linux), so compare the whole band.
        var band = grid.LogicalToDeviceUnits (new Rectangle (cell.Right - 20, cell.Top, 20, cell.Height));
        var differing = 0;
        for (var y = band.Top; y < band.Bottom; y++)
            for (var x = band.Left; x < band.Right; x++)
                if (with.GetPixel (x, y) != without.GetPixel (x, y))
                    differing++;

        Assert.True (differing > 0, "hiding the drop-down button changed no pixel in the button band");
    }

    [Fact]
    public void DataGridViewComboBoxColumn_MaxDropDownItems_reaches_the_editor ()
    {
        HeadlessRenderer.Use ();
        var column = new DataGridViewComboBoxColumn { Width = 200, MaxDropDownItems = 3 };
        using var grid = ComboGrid (column);
        using var form = new Form { Size = new Size (400, 300) };
        form.Controls.Add (grid);
        form.Show ();

        grid.CurrentCell = grid.Rows[0].Cells[0];
        Assert.True (grid.BeginEdit (false));

        var editor = Assert.IsAssignableFrom<ComboBox> (grid.EditingControl);
        Assert.Equal (3, editor.MaxDropDownItems);
    }

    [Fact]
    public void A_BindingSource_that_forbids_editing_stops_BeginEdit ()
    {
        HeadlessRenderer.Use ();
        var source = new BindingSource { DataSource = new List<Person> { new () { Name = "a" } }, AllowEdit = false };
        using var grid = new DataGridView { Size = new Size (300, 120), DataSource = source };
        using var form = new Form { Size = new Size (400, 300) };
        form.Controls.Add (grid);
        form.Show ();

        grid.CurrentCell = grid.Rows[0].Cells[0];
        Assert.False (grid.BeginEdit (false));

        source.AllowEdit = true;
        Assert.True (grid.BeginEdit (false));
    }

    private sealed class Person { public string Name { get; set; } = ""; }

    [Fact]
    public void TaskDialog_AllowCancel_and_AllowMinimize_shape_the_caption_boxes ()
    {
        using var plain = TaskDialog.Build (new TaskDialogPage (), _ => { });
        Assert.False (plain.ControlBox);

        using var cancellable = TaskDialog.Build (new TaskDialogPage { AllowCancel = true }, _ => { });
        Assert.True (cancellable.ControlBox);
        Assert.False (cancellable.MinimizeBox);

        using var minimisable = TaskDialog.Build (new TaskDialogPage { AllowMinimize = true }, _ => { });
        Assert.True (minimisable.MinimizeBox);
    }

    [Fact]
    public void TaskDialog_radio_button_Enabled_reaches_the_control ()
    {
        var page = new TaskDialogPage { RadioButtons = { new TaskDialogRadioButton ("on"), new TaskDialogRadioButton ("off") { Enabled = false } } };
        using var form = TaskDialog.Build (page, _ => { });
        var radios = form.Controls.GetAllControls ().OfType<RadioButton> ().ToList ();

        Assert.Equal (new[] { true, false }, radios.Select (r => r.Enabled));
    }

    [Fact]
    public void TaskDialog_expander_toggles_its_details_and_button_text ()
    {
        var expander = new TaskDialogExpander ("More here") { CollapsedButtonText = "Show", ExpandedButtonText = "Hide" };
        var page = new TaskDialogPage { Expander = expander };
        using var form = TaskDialog.Build (page, _ => { });
        var toggle = form.Controls.GetAllControls ().OfType<Button> ().First (b => b.Text == "Show");
        var details = form.Controls.GetAllControls ().OfType<Label> ().First (l => l.Text == "More here");

        Assert.False (details.Visible);
        toggle.PerformClick ();
        Assert.True (expander.Expanded);
        Assert.True (details.Visible);
        Assert.Equal ("Hide", toggle.Text);
    }

    [Fact]
    public void MaskedTextBox_HidePromptOnLeave_hides_the_prompt_while_unfocused ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var box = new MaskedTextBox { Mask = "000-000", HidePromptOnLeave = true };
        var other = new TextBox ();
        form.Controls.Add (box);
        form.Controls.Add (other);
        form.Show ();

        box.Focus ();
        box.Text = "12";
        Assert.Contains ("_", box.DisplayText);   // prompts show while focused

        other.Focus ();
        Assert.DoesNotContain ("_", box.DisplayText);

        box.Focus ();
        Assert.Contains ("_", box.DisplayText);
    }

    [Fact]
    public void CellPainting_State_carries_the_cells_selection_and_read_only_flags ()
    {
        HeadlessRenderer.Use ();
        using var grid = new DataGridView { Size = new Size (300, 120), ColumnHeadersVisible = false, RowHeadersVisible = false };
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "A", Width = 100 });
        grid.Rows.Add ("x");
        grid.Rows.Add ("y");
        grid.Rows[1].Cells[0].ReadOnly = true;
        grid.Rows[0].Cells[0].Selected = true;

        var states = new Dictionary<int, DataGridViewElementStates> ();
        grid.CellPainting += (_, e) => { if (e.RowIndex >= 0) states[e.RowIndex] = e.State; };
        using var bitmap = PaintSurface.Render (grid);

        Assert.True (states[0].HasFlag (DataGridViewElementStates.Selected));
        Assert.False (states[1].HasFlag (DataGridViewElementStates.Selected));
        Assert.True (states[1].HasFlag (DataGridViewElementStates.ReadOnly));
        Assert.True (states[0].HasFlag (DataGridViewElementStates.Displayed));
    }

    [Fact]
    public void PropertyGrid_focused_selection_uses_SelectedItemWithFocus_colours ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var grid = new PropertyGrid { Bounds = new Rectangle (0, 0, 300, 200), SelectedObject = new Person { Name = "n" }, SelectedItemWithFocusBackColor = Color.FromArgb (10, 200, 30) };
        form.Controls.Add (grid);
        form.Show ();
        grid.Focus ();
        // Row 0 is the "Misc" category band; row 1 is Name. The rows sit below the toolbar, so their
        // position comes from the grid; RowBounds is device and the mouse is logical (RC-8).
        var name_row = grid.DeviceToLogicalUnits (grid.RowBounds (1));
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, name_row.Top + (name_row.Height / 2), 0));

        using var bitmap = PaintSurface.Render (grid);
        var pixels = new HashSet<SKColor> ();
        for (var y = 0; y < bitmap.Height; y += 2)
            for (var x = 0; x < bitmap.Width; x += 2)
                pixels.Add (bitmap.GetPixel (x, y));

        Assert.Contains (new SKColor (10, 200, 30), pixels);
    }

    [Fact]
    public void DomainUpDown_Sorted_orders_the_items ()
    {
        using var spinner = new DomainUpDown ();
        spinner.Items.Add ("c");
        spinner.Items.Add ("a");
        spinner.Items.Add ("b");

        spinner.Sorted = true;

        Assert.Equal (new[] { "a", "b", "c" }, spinner.Items.Cast<object> ().Select (o => o.ToString ()));
    }
}
