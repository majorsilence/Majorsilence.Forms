using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// FlowLayoutPanel and TableLayoutPanel position their own children, so two things about AutoScroll went wrong for them that never did for a
// plain Panel: the content extent was measured BEFORE the layout engine ran (from positions that did not exist yet), so the scroll bar never
// appeared for content taller than the panel; and the implicit scroll bar was itself laid out as one more flow child, which put it after the last
// control -- thousands of pixels below the visible area -- once it did appear. An app reading a long article in a flow panel could not scroll it.
[Collection ("Headless")]
public class AutoScrollLayoutPanelTests
{
    private static Form Show (Control panel)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (320, 400) };
        form.Controls.Add (panel);
        form.Show ();
        HeadlessRenderer.CapturePng (form);
        return form;
    }

    private static Control VerticalBar (ScrollableControl panel)
        => panel.Controls.GetAllControls (true).First (c => c.GetType ().Name == "VerticalScrollBar");

    private static FlowLayoutPanel Column (int count, int itemHeight = 30)
    {
        var flow = new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Location = new Point (0, 0),
            Size = new Size (240, 160),
        };

        for (var i = 0; i < count; i++)
            flow.Controls.Add (new Panel { Size = new Size (180, itemHeight), Margin = new Padding (0, 0, 0, 6) });

        return flow;
    }

    [Fact]
    public void A_flow_panel_with_more_content_than_fits_shows_its_vertical_scroll_bar ()
    {
        using var form = Show (Column (count: 30));
        var flow = (FlowLayoutPanel) form.Controls[0];

        Assert.True (flow.VerticalScroll.Visible);
        // 30 rows of 30 + 6 spacing is about 1080 tall in a 160 tall panel, so the bar must be able to travel most of that.
        Assert.True (flow.VerticalScroll.Maximum > 800, $"scroll range {flow.VerticalScroll.Maximum}");
    }

    [Fact]
    public void The_scroll_bar_is_placed_inside_the_panel_not_after_the_last_control ()
    {
        using var form = Show (Column (count: 30));
        var flow = (FlowLayoutPanel) form.Controls[0];
        var bar = VerticalBar (flow);

        Assert.True (bar.Visible);
        Assert.Equal (0, bar.Top);
        Assert.Equal (flow.Width, bar.Right);
        Assert.True (bar.Height <= flow.Height);
    }

    [Fact]
    public void A_flow_panel_with_content_that_fits_shows_no_scroll_bar ()
    {
        using var form = Show (Column (count: 3));
        var flow = (FlowLayoutPanel) form.Controls[0];

        Assert.False (flow.VerticalScroll.Visible);
        Assert.False (VerticalBar (flow).Visible);
    }

    [Fact]
    public void Scrolling_a_flow_panel_moves_its_content ()
    {
        using var form = Show (Column (count: 30));
        var flow = (FlowLayoutPanel) form.Controls[0];
        var first = flow.Controls[0];
        var top = first.Top;

        // The setter takes the positive distance, as WinForms' does (LAY-33).
        flow.AutoScrollPosition = new Point (0, 300);
        HeadlessRenderer.CapturePng (form);

        Assert.Equal (top - 300, first.Top);
    }

    [Fact]
    public void The_scroll_range_follows_content_added_after_the_panel_was_shown ()
    {
        using var form = Show (Column (count: 2));
        var flow = (FlowLayoutPanel) form.Controls[0];
        Assert.False (flow.VerticalScroll.Visible);

        for (var i = 0; i < 20; i++)
            flow.Controls.Add (new Panel { Size = new Size (180, 30), Margin = new Padding (0, 0, 0, 6) });
        HeadlessRenderer.CapturePng (form);

        Assert.True (flow.VerticalScroll.Visible);
    }

    [Fact]
    public void Making_the_panel_shorter_lengthens_the_scroll_range ()
    {
        using var form = Show (Column (count: 30));
        var flow = (FlowLayoutPanel) form.Controls[0];
        // The scroll range is Maximum - LargeChange + 1 (the last offset), as upstream defines it.
        var tall = flow.VerticalScroll.Maximum - flow.VerticalScroll.LargeChange + 1;

        flow.Height = 80;
        HeadlessRenderer.CapturePng (form);

        var short_range = flow.VerticalScroll.Maximum - flow.VerticalScroll.LargeChange + 1;
        Assert.True (flow.VerticalScroll.Visible);
        Assert.True (short_range > tall, $"short {short_range} should exceed tall {tall}");
    }

    // The shape of a real reader screen: a flow panel docked to fill a page, with padding, filled after the form is showing by clearing and adding
    // controls of explicit sizes inside Suspend/ResumeLayout. This is the arrangement in which the extent was measured before the children had
    // been laid out and came out as zero, so no scroll bar ever appeared.
    [Fact]
    public void A_docked_padded_flow_panel_filled_after_the_form_is_shown_scrolls ()
    {
        HeadlessRenderer.Use ();
        var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding (12) };
        var flow = new FlowLayoutPanel {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding (0, 8, 0, 8),
        };
        page.Controls.Add (flow);
        using var form = Show (page);

        flow.SuspendLayout ();
        for (var i = 0; i < 40; i++)
            flow.Controls.Add (new Label { Text = $"Paragraph {i}", AutoSize = false, Size = new Size (200, 40), Margin = new Padding (0, 0, 0, 8) });
        flow.ResumeLayout ();
        HeadlessRenderer.CapturePng (form);

        Assert.True (flow.VerticalScroll.Visible, "40 rows of 48 are about 1900 tall in a panel of under 400");
        Assert.True (flow.VerticalScroll.Maximum > 1000, $"scroll range {flow.VerticalScroll.Maximum}");

        flow.SuspendLayout ();
        foreach (var old in flow.Controls.OfType<Control> ().ToArray ())
            flow.Controls.Remove (old);
        flow.Controls.Add (new Label { Text = "just one", AutoSize = false, Size = new Size (200, 40) });
        flow.ResumeLayout ();
        HeadlessRenderer.CapturePng (form);

        Assert.False (flow.VerticalScroll.Visible, "after clearing back to one row there is nothing to scroll");
    }

    [Fact]
    public void A_table_layout_panel_with_more_rows_than_fit_scrolls_too ()
    {
        HeadlessRenderer.Use ();
        var table = new TableLayoutPanel { AutoScroll = true, ColumnCount = 1, RowCount = 30, Size = new Size (240, 160) };
        for (var i = 0; i < 30; i++)
            table.Controls.Add (new Panel { Size = new Size (180, 30) }, 0, i);
        using var form = Show (table);

        Assert.True (table.VerticalScroll.Visible);
        var bar = VerticalBar (table);
        Assert.Equal (0, bar.Top);
        Assert.Equal (table.Width, bar.Right);
    }

    [Fact]
    public void A_plain_panel_with_positioned_children_still_scrolls_as_before ()
    {
        HeadlessRenderer.Use ();
        var panel = new Panel { AutoScroll = true, Size = new Size (240, 160) };
        for (var i = 0; i < 30; i++)
            panel.Controls.Add (new Panel { Location = new Point (0, i * 36), Size = new Size (180, 30) });
        using var form = Show (panel);

        Assert.True (panel.VerticalScroll.Visible);
        Assert.Equal (0, VerticalBar (panel).Top);
    }
}
