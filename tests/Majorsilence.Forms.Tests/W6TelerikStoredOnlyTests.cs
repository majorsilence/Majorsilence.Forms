using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twenty-fifth chunk (#176): the Telerik stored-only baseline, sorted. These are the
// entries that were real gaps rather than legitimately inert -- a per-cell style on a wrapper thrown
// away on every access, list items whose Value and Checked nothing read, CheckOnClick, the template's
// MultiSelect, a collapsible panel with no header, a ribbon bar with no buttons, and a scheduler
// ContextMenuOpening that nothing raised.
[Collection ("Headless")]
public class W6TelerikStoredOnlyTests
{
    private static readonly Color Magenta = Color.FromArgb (255, 0, 255);

    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 200 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });
        grid.Rows.Add ();
        grid.Rows.Add ();
        grid.Rows[0].Cells["Name"].Value = "Alice";
        grid.Rows[1].Cells["Name"].Value = string.Empty;

        form = new Form { Width = 460, Height = 300 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    // The second row: not current, so no selection colour over it; empty, so no text over the probe.
    private static SKColor CellFill (RadGridView grid)
    {
        using var bitmap = PaintSurface.Render (grid);
        var cell = ((DataGridView) grid).GetCellBounds (1, 0);
        var inset = grid.LogicalToDeviceUnits (3);
        return bitmap.GetPixel (cell.Left + inset, cell.Top + inset);
    }

    private static bool IsMagenta (SKColor c) => c.Red == 255 && c.Green == 0 && c.Blue == 255;

    [Fact]
    public void A_cells_own_style_is_kept_and_painted_once_the_fill_is_customized ()
    {
        var grid = Grid (out var form);

        using (form) {
            var style = grid.Rows[1].Cells[0].Style;
            Assert.Same (style, grid.Rows[1].Cells[0].Style);   // was a new object on every access

            style.BackColor = Magenta;
            Assert.False (IsMagenta (CellFill (grid)), "Telerik paints a cell style's BackColor only once CustomizeFill is set");

            style.CustomizeFill = true;
            Assert.True (IsMagenta (CellFill (grid)), "the cell's own style should be its fill");

            style.ForeColor = Color.Red;
            PaintSurface.Render (grid).Dispose ();
            Assert.Equal (new SKColor (255, 0, 0), ((DataGridView) grid).Rows[1].Cells[0].Style.ForegroundColor);

            style.Reset ();
            Assert.False (IsMagenta (CellFill (grid)), "Reset should take the fill away again");
        }
    }

    [Fact]
    public void A_formatting_handler_can_style_through_the_elements_style ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.CellFormatting += (_, e) => {
                if (e.RowIndex == 1) {
                    e.CellElement.Style.CustomizeFill = true;
                    e.CellElement.Style.BackColor = Magenta;
                }
            };

            Assert.True (IsMagenta (CellFill (grid)));
        }
    }

    [Fact]
    public void Cell_IsSelected_is_the_cells_selection ()
    {
        var grid = Grid (out var form);

        using (form) {
            var cell = grid.Rows[1].Cells[0];
            cell.IsSelected = true;
            Assert.True (((DataGridView) grid).Rows[1].Cells[0].Selected);
            Assert.True (grid.Rows[1].Cells[0].IsSelected);
        }
    }

    [Fact]
    public void MasterTemplate_MultiSelect_reaches_the_grid ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.MasterTemplate.MultiSelect = true;
            Assert.True (((DataGridView) grid).MultiSelect);

            grid.MasterTemplate.MultiSelect = false;
            Assert.False (((DataGridView) grid).MultiSelect);
        }
    }

    [Fact]
    public void A_drop_down_list_item_keeps_its_value ()
    {
        using var list = new RadDropDownList ();
        var first = new RadListDataItem ("First", 10);
        var second = new RadListDataItem ("Second", 20);
        list.Items.Add (first);
        list.Items.Add (second);

        list.SelectedIndex = 1;
        Assert.Same (second, list.SelectedItem);   // was a new wrapper around it, with Value null
        Assert.Equal (20, list.SelectedItem!.Value);
        Assert.Equal (20, list.SelectedValue);

        list.SelectedValue = 10;
        Assert.Equal (0, list.SelectedIndex);
    }

    [Fact]
    public void A_bound_drop_down_lists_SelectedItem_carries_the_value ()
    {
        using var list = new RadDropDownList {
            DisplayMember = "Name", ValueMember = "Id",
            DataSource = new[] { new { Id = 1, Name = "One" }, new { Id = 2, Name = "Two" } },
        };

        list.SelectedIndex = 1;
        Assert.Equal ("Two", list.SelectedItem!.Text);
        Assert.Equal (2, list.SelectedItem.Value);
    }

    [Fact]
    public void Checked_items_follow_each_items_Checked ()
    {
        using var list = new RadCheckedDropDownList ();
        var a = new RadCheckedListDataItem ("a") { Checked = true };
        var b = new RadCheckedListDataItem ("b");
        list.Items.Add (a);
        list.Items.Add (b);

        Assert.Equal (new[] { a }, list.CheckedItems);   // added checked: was missing

        b.Checked = true;
        a.Checked = false;
        Assert.Equal (new[] { b }, list.CheckedItems);

        list.SetItemChecked (a, true);
        Assert.Equal (new[] { a, b }, list.CheckedItems);   // list order
    }

    [Fact]
    public void CheckOnClick_toggles_before_the_handler_runs ()
    {
        var item = new RadMenuItem ("Wrap") { CheckOnClick = true };
        bool? seen = null;
        item.Click += (_, _) => seen = item.Checked;

        item.PerformClick ();
        Assert.True (item.Checked);
        Assert.True (seen);

        item.PerformClick ();
        Assert.False (item.Checked);

        var plain = new RadMenuItem ("Plain");
        plain.PerformClick ();
        Assert.False (plain.Checked);
    }

    [Fact]
    public void The_collapsible_panels_header_shows_the_text_and_toggles_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 400 };
        var panel = new RadCollapsiblePanel { Dock = DockStyle.Top, Height = 200, HeaderText = "Details" };
        var below = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add (below);
        form.Controls.Add (panel);
        form.Show ();
        form.PerformLayout ();

        Assert.True (panel.Header.Visible);
        Assert.EndsWith ("Details", panel.Header.Text);
        Assert.Equal (0, panel.Header.Top);
        Assert.True (panel.PanelContainer.Top >= panel.Header.Bottom, "the content should sit below the header");

        var belowTop = below.Top;
        // HeadlessRenderer.Click takes logical form-client coordinates, which GetPositionInForm is.
        var header = panel.Header.GetPositionInForm ();
        var client = new Point (header.X + 5, header.Y + 5);
        HeadlessRenderer.Click (form, client.X, client.Y);

        Assert.False (panel.IsExpanded);
        Assert.Equal (RadCollapsiblePanel.HeaderHeight + panel.Padding.Vertical, panel.Height);
        Assert.True (below.Top < belowTop, "what sits below should move up when the panel collapses");

        HeadlessRenderer.Click (form, client.X, client.Y);
        Assert.True (panel.IsExpanded);
        Assert.Equal (200, panel.Height);
    }

    [Fact]
    public void The_ribbon_bars_buttons_send_their_commands_to_the_editor ()
    {
        HeadlessRenderer.Use ();
        using var editor = new RadRichTextEditor ();
        using var bar = new RichTextEditorRibbonBar { AssociatedRichTextEditor = editor };

        var sent = new List<string> ();
        editor.CommandRequested += (command, _) => sent.Add (command);

        Assert.Equal (RichTextEditorRibbonBar.Commands.Length, bar.Toolbar.Buttons.Count);

        foreach (ToolBarButton button in bar.Toolbar.Buttons)
            bar.Toolbar.RaiseButtonClick (button);

        Assert.Equal (RichTextEditorRibbonBar.Commands.Select (c => c.Command), sent);
        Assert.Contains ("bold", sent);

        // The toolbar does not hide the bar: the bar's background shows around the buttons, even under a
        // theme that colours ToolBar -- the toolbar is how the bar is built, not a second surface.
        bar.Width = 600;
        bar.Height = 46;
        bar.Style.BackgroundColor = new SKColor (255, 0, 255);
        Theme.LoadFromCss ("ToolBar { background-color: #00ff00; }");
        try {
            using (var bitmap = PaintSurface.RenderOnForm (bar)) {
                var magenta = 0;
                for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                        if (IsMagenta (bitmap.GetPixel (x, y)))
                            magenta++;
                Assert.True (magenta > bitmap.Width * bitmap.Height / 4, $"only {magenta} of {bitmap.Width * bitmap.Height} pixels show the bar's background");
            }
        } finally {
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        // No editor associated: the buttons do nothing rather than throw.
        bar.AssociatedRichTextEditor = null;
        bar.Toolbar.RaiseButtonClick (bar.Toolbar.Buttons[0]);
    }

    [Fact]
    public void A_right_click_on_the_scheduler_raises_ContextMenuOpening_and_honours_Cancel ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 700, Height = 500 };
        var scheduler = new RadScheduler { Dock = DockStyle.Fill };
        form.Controls.Add (scheduler);
        form.Show ();

        var raised = 0;
        var shown = 0;
        var cancel = false;
        scheduler.ContextMenuOpening += (_, e) => {
            raised++;
            e.Cancel = cancel;
            e.ContextMenu.DropDownOpening += (_, _) => shown++;   // raised by Show
        };

        void RightClick ()
        {
            scheduler.Agenda.RaiseMouseDown (new MouseEventArgs (MouseButtons.Right, 1, 10, 10, 0));
            scheduler.Agenda.RaiseMouseUp (new MouseEventArgs (MouseButtons.Right, 1, 10, 10, 0));
        }

        RightClick ();
        Assert.Equal (1, raised);
        Assert.Equal (1, shown);

        cancel = true;
        RightClick ();
        Assert.Equal (2, raised);
        Assert.Equal (1, shown);   // cancelled: not shown
    }

    private enum Status { Open, Closed }

    [Fact]
    public void EnumBinder_fills_its_target_whichever_is_set_first ()
    {
        var column = new GridViewComboBoxColumn ("Status");
        _ = new EnumBinder { Target = column, Source = typeof (Status) };
        Assert.Equal (new object[] { Status.Open, Status.Closed }, ((System.Collections.IList) column.DataSource!).Cast<object> ());

        using var combo = new ComboBox ();
        _ = new EnumBinder { Source = typeof (Status), Target = combo };
        Assert.Equal (2, combo.Items.Count);
    }
}
