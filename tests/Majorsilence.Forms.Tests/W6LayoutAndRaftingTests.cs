using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Printing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, fifteenth chunk: the items earlier chunks recorded as not done -- the strip's
// LayoutSettings and a real Table arrangement, the rafting drag that ToolStripPanel.Locked refuses,
// the grid's frozen-column ordering rule, the print controller that discarded what it wrapped, and
// the keyboard activation a strip item lacked.
[Collection ("Headless")]
public class W6LayoutAndRaftingTests
{
    private static ToolStrip Strip (out Form form, int items = 4, int width = 300)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = width + 80, Height = 260 };
        var strip = new ToolStrip { Width = width, Height = 120, Dock = DockStyle.None };

        for (var i = 0; i < items; i++)
            strip.Items.Add (new ToolStripButton ($"B{i}") { AutoSize = false, Size = new Size (40, 22) });

        form.Controls.Add (strip);
        form.Show ();
        PaintSurface.Render (strip).Dispose ();
        return strip;
    }

    // ── LayoutSettings ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LayoutSettings_answers_the_object_the_layout_style_calls_for ()
    {
        var strip = Strip (out var form);

        using (form) {
            // The stacking styles have no settings, as upstream's do not.
            Assert.Null (strip.LayoutSettings);

            strip.LayoutStyle = ToolStripLayoutStyle.Flow;
            var flow = Assert.IsType<FlowLayoutSettings> (strip.LayoutSettings);
            Assert.Same (flow, strip.LayoutSettings);   // created once, not per read

            strip.LayoutStyle = ToolStripLayoutStyle.Table;
            Assert.IsType<TableLayoutSettings> (strip.LayoutSettings);

            strip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            Assert.Null (strip.LayoutSettings);
        }
    }

    [Fact]
    public void A_flow_strip_reads_its_FlowDirection_and_WrapContents ()
    {
        var strip = Strip (out var form, items: 4, width: 100);

        using (form) {
            strip.LayoutStyle = ToolStripLayoutStyle.Flow;
            var settings = (FlowLayoutSettings) strip.LayoutSettings!;

            // Left to right in a narrow strip: the items wrap onto a second row.
            PaintSurface.Render (strip).Dispose ();
            Assert.True (strip.Items[3].Bounds.Top > strip.Items[0].Bounds.Top, "the items should have wrapped");

            settings.WrapContents = false;
            PaintSurface.Render (strip).Dispose ();
            Assert.Equal (strip.Items[0].Bounds.Top, strip.Items[3].Bounds.Top);

            // Top down stacks them instead, whatever the width.
            settings.FlowDirection = FlowDirection.TopDown;
            PaintSurface.Render (strip).Dispose ();

            Assert.True (strip.Items[3].Bounds.Top > strip.Items[0].Bounds.Top, "TopDown should stack the items");
            Assert.Equal (strip.Items[0].Bounds.Left, strip.Items[3].Bounds.Left);
        }
    }

    [Fact]
    public void A_table_strip_lays_its_items_out_in_a_grid ()
    {
        var strip = Strip (out var form, items: 4, width: 300);

        using (form) {
            strip.LayoutStyle = ToolStripLayoutStyle.Table;
            var settings = (TableLayoutSettings) strip.LayoutSettings!;
            settings.ColumnCount = 2;

            PaintSurface.Render (strip).Dispose ();

            // Two columns: items 0 and 1 share a row, 2 and 3 the next, and the columns line up.
            Assert.Equal (strip.Items[0].Bounds.Top, strip.Items[1].Bounds.Top);
            Assert.True (strip.Items[2].Bounds.Top > strip.Items[0].Bounds.Top, "the third item should start a new row");
            Assert.Equal (strip.Items[0].Bounds.Left, strip.Items[2].Bounds.Left);
            Assert.Equal (strip.Items[1].Bounds.Left, strip.Items[3].Bounds.Left);
            Assert.True (strip.Items[1].Bounds.Left > strip.Items[0].Bounds.Left);

            // One column stacks them.
            settings.ColumnCount = 1;
            PaintSurface.Render (strip).Dispose ();

            Assert.Equal (strip.Items[0].Bounds.Left, strip.Items[1].Bounds.Left);
            Assert.True (strip.Items[1].Bounds.Top > strip.Items[0].Bounds.Top);
        }
    }

    // ── rafting ─────────────────────────────────────────────────────────────────────────────────────

    private static ToolStripPanel Panel (out Form form, out ToolStrip first, out ToolStrip second)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 420, Height = 300 };
        var panel = new ToolStripPanel { Bounds = new Rectangle (0, 0, 380, 200) };

        first = new ToolStrip { GripStyle = ToolStripGripStyle.Visible, Height = 26 };
        first.Items.Add (new ToolStripButton ("one"));
        second = new ToolStrip { GripStyle = ToolStripGripStyle.Visible, Height = 26 };
        second.Items.Add (new ToolStripButton ("two"));

        form.Controls.Add (panel);
        panel.Join (first, 0);
        panel.Join (second, 1);
        form.Show ();
        PaintSurface.Render (panel).Dispose ();
        return panel;
    }

    [Fact]
    public void Dragging_a_strip_by_its_grip_moves_it_to_another_row ()
    {
        var panel = Panel (out var form, out var first, out var second);

        using (form) {
            Assert.Equal (0, panel.RowIndexOf (first));
            Assert.Equal (1, panel.RowIndexOf (second));

            var row = panel.Rows[1];
            Assert.True (panel.BeginRaft (first, new Point (4, 2)));
            Assert.Same (first, panel.DraggingStrip);

            Assert.True (panel.EndRaft (new Point (4, row.Bounds.Top + (row.Bounds.Height / 2))));
            Assert.Equal (1, panel.RowIndexOf (first));
            Assert.Null (panel.DraggingStrip);
        }
    }

    [Fact]
    public void A_locked_panel_refuses_the_drag_but_still_takes_a_Join ()
    {
        var panel = Panel (out var form, out var first, out _);

        using (form) {
            panel.Locked = true;

            Assert.False (panel.BeginRaft (first, new Point (4, 2)));
            Assert.Null (panel.DraggingStrip);

            var row = panel.Rows[1];
            Assert.False (panel.EndRaft (new Point (4, row.Bounds.Top + 2)));
            Assert.Equal (0, panel.RowIndexOf (first));

            // Join is the programmatic path and, as upstream, is not subject to the lock.
            panel.Join (first, 1);
            Assert.Equal (1, panel.RowIndexOf (first));
        }
    }

    [Fact]
    public void A_press_and_release_in_the_same_place_is_not_a_drag ()
    {
        var panel = Panel (out var form, out var first, out _);

        using (form) {
            Assert.True (panel.BeginRaft (first, new Point (4, 2)));
            Assert.False (panel.EndRaft (new Point (4, 2)));
            Assert.Equal (0, panel.RowIndexOf (first));
        }
    }

    // ── frozen columns ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_display_index_that_interleaves_frozen_and_scrolling_columns_is_refused ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 500, Height = 300 };
        var grid = new DataGridView { Width = 420, Height = 200, AllowUserToAddRows = false };

        for (var i = 0; i < 4; i++)
            grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{i}", Width = 70 });

        grid.Columns[0].Frozen = true;
        grid.Columns[1].Frozen = true;
        form.Controls.Add (grid);
        form.Show ();

        // Moving a frozen column within the frozen band is fine.
        grid.Columns[0].DisplayIndex = 1;
        Assert.Equal (1, grid.Columns[0].DisplayIndex);
        Assert.Equal (0, grid.Columns[1].DisplayIndex);

        // Pushing a frozen column past a scrolling one is refused, and nothing moves.
        Assert.Throws<InvalidOperationException> (() => grid.Columns[0].DisplayIndex = 3);
        Assert.Equal (1, grid.Columns[0].DisplayIndex);

        // And so is pulling a scrolling column in front of a frozen one.
        Assert.Throws<InvalidOperationException> (() => grid.Columns[3].DisplayIndex = 0);
        Assert.Equal (3, grid.Columns[3].DisplayIndex);

        // A scrolling column still moves among the scrolling ones.
        grid.Columns[3].DisplayIndex = 2;
        Assert.Equal (2, grid.Columns[3].DisplayIndex);
    }

    // ── the print controller wrapper ─────────────────────────────────────────────────────────────────

    [Fact]
    public void PrintControllerWithStatusDialog_forwards_to_the_controller_it_wraps ()
    {
        using var document = new PrintDocument ();
        var page = 0;
        document.PrintPage += (_, e) => e.HasMorePages = ++page < 2;

        var preview = new PreviewPrintController ();
        var wrapper = new PrintControllerWithStatusDialog (preview, "Printing");

        Assert.Equal ("Printing", wrapper.DialogTitle);
        Assert.True (wrapper.IsPreview);

        document.PrintController = wrapper;
        document.RunThroughController (PrintAction.PrintToPreview);

        // The wrapped controller saw every page, which it could not when the wrapper discarded it.
        Assert.Equal (2, preview.GetPreviewPageInfo ().Length);
    }

    // ── keyboard activation ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Space_activates_the_selected_strip_item_as_Enter_does ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 200 };
        var strip = new ToolStrip { Width = 300, Height = 26 };
        var label = new ToolStripLabel ("Open the manual") { IsLink = true };
        strip.Items.Add (label);
        form.Controls.Add (strip);
        form.Show ();

        var clicks = 0;
        label.Click += (_, _) => clicks++;

        strip.SelectItemFromKeyboard (label);

        Assert.True (strip.HandleNavigationKey (Keys.Space));
        Assert.Equal (1, clicks);

        strip.SelectItemFromKeyboard (label);
        Assert.True (strip.HandleNavigationKey (Keys.Enter));
        Assert.Equal (2, clicks);
    }
}
