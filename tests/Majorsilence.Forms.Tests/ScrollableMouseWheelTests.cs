using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// ScrollableControl did nothing with the mouse wheel: only a scroll bar reacted to it, and only with the pointer over the bar itself. Worse, the wheel
// went to the deepest control under the pointer and stopped there, so over a label inside a scrolling panel it reached nobody. WinForms scrolls the
// panel, and bubbles an unhandled wheel up to the nearest container that can scroll; a control that uses the wheel itself (a list, a grid) keeps it.
[Collection ("Headless")]
public class ScrollableMouseWheelTests
{
    private static MouseEventArgs Wheel (int x, int y, int delta) => new (MouseButtons.None, 0, x, y, delta);

    private static int Notch (Control c) => SystemInformation.MouseWheelScrollLines * System.Math.Max (16, c.Font.Height);

    // A panel 240x160 with 30 rows of 30px at 36px pitch (a plain Panel, positioned children, as in any scrolling page).
    private static Panel TallPanel (out Label firstRow)
    {
        var panel = new Panel { AutoScroll = true, Location = new Point (0, 0), Size = new Size (240, 160) };
        firstRow = new Label { Text = "row 0", Location = new Point (0, 0), Size = new Size (180, 30) };
        panel.Controls.Add (firstRow);
        for (var i = 1; i < 30; i++)
            panel.Controls.Add (new Label { Text = $"row {i}", Location = new Point (0, i * 36), Size = new Size (180, 30) });
        return panel;
    }

    private static Form Show (Control c)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (320, 400) };
        form.Controls.Add (c);
        form.Show ();
        HeadlessRenderer.CapturePng (form);
        return form;
    }

    [Fact]
    public void The_wheel_over_a_label_inside_a_scrolling_panel_scrolls_the_panel ()
    {
        var panel = TallPanel (out var row);
        using var form = Show (panel);

        var consumed = panel.RaiseMouseWheel (Wheel (20, 15, -120));   // over row 0, which does nothing with the wheel

        Assert.True (consumed);
        Assert.Equal (-Notch (panel), panel.AutoScrollPosition.Y);
        Assert.Equal (-Notch (panel), row.Top);
    }

    [Fact]
    public void The_wheel_over_empty_panel_space_scrolls_it ()
    {
        var panel = TallPanel (out _);
        using var form = Show (panel);

        panel.RaiseMouseWheel (Wheel (200, 20, -120));   // right of the labels and left of the scroll bar: the panel itself

        Assert.Equal (-Notch (panel), panel.AutoScrollPosition.Y);
    }

    [Fact]
    public void Wheel_up_scrolls_back_and_never_past_the_top ()
    {
        var panel = TallPanel (out _);
        using var form = Show (panel);

        panel.RaiseMouseWheel (Wheel (20, 15, -120));
        panel.RaiseMouseWheel (Wheel (20, 15, 120));
        Assert.Equal (0, panel.AutoScrollPosition.Y);

        panel.RaiseMouseWheel (Wheel (20, 15, 120));
        Assert.Equal (0, panel.AutoScrollPosition.Y);
    }

    [Fact]
    public void A_partial_notch_is_carried_over_until_it_adds_up ()
    {
        var panel = TallPanel (out _);
        using var form = Show (panel);

        panel.RaiseMouseWheel (Wheel (20, 15, -60));
        Assert.Equal (0, panel.AutoScrollPosition.Y);

        panel.RaiseMouseWheel (Wheel (20, 15, -60));
        Assert.Equal (-Notch (panel), panel.AutoScrollPosition.Y);
    }

    [Fact]
    public void A_panel_with_nothing_to_scroll_leaves_the_wheel_for_the_one_around_it ()
    {
        var outer = TallPanel (out _);
        var inner = new Panel { Location = new Point (0, 0), Size = new Size (150, 20), AutoScroll = true };
        inner.Controls.Add (new Label { Text = "short", Location = new Point (0, 0), Size = new Size (100, 10) });
        outer.Controls.Add (inner);
        using var form = Show (outer);

        var consumed = outer.RaiseMouseWheel (Wheel (20, 5, -120));

        Assert.True (consumed);
        Assert.Equal (0, inner.AutoScrollPosition.Y);
        Assert.Equal (-Notch (outer), outer.AutoScrollPosition.Y);
    }

    [Fact]
    public void A_list_inside_a_scrolling_panel_keeps_the_wheel_for_itself ()
    {
        HeadlessRenderer.Use ();
        var panel = new Panel { AutoScroll = true, Location = new Point (0, 0), Size = new Size (240, 160) };
        // First added is topmost, so the list goes in before the rows that overlap it.
        var list = new ListBox { Location = new Point (0, 0), Size = new Size (150, 80) };
        for (var i = 0; i < 60; i++)
            list.Items.Add ($"item {i}");
        panel.Controls.Add (list);
        for (var i = 0; i < 30; i++)
            panel.Controls.Add (new Label { Text = $"row {i}", Location = new Point (0, i * 36), Size = new Size (180, 30) });
        using var form = Show (panel);

        var before = list.FirstVisibleIndex;
        panel.RaiseMouseWheel (Wheel (20, 10, -120));

        Assert.True (list.FirstVisibleIndex > before, "the list scrolled");
        Assert.Equal (0, panel.AutoScrollPosition.Y);   // and the panel stayed put
    }

    [Fact]
    public void A_panel_whose_content_fits_does_not_consume_the_wheel ()
    {
        HeadlessRenderer.Use ();
        var panel = new Panel { AutoScroll = true, Size = new Size (240, 160) };
        panel.Controls.Add (new Label { Text = "short", Location = new Point (0, 0), Size = new Size (100, 20) });
        using var form = Show (panel);

        Assert.False (panel.RaiseMouseWheel (Wheel (20, 10, -120)));
        Assert.Equal (0, panel.AutoScrollPosition.Y);
    }

    [Fact]
    public void A_panel_without_auto_scroll_ignores_the_wheel ()
    {
        HeadlessRenderer.Use ();
        var panel = new Panel { AutoScroll = false, Size = new Size (240, 160) };
        panel.Controls.Add (new Label { Text = "x", Location = new Point (0, 300), Size = new Size (100, 20) });
        using var form = Show (panel);

        Assert.False (panel.RaiseMouseWheel (Wheel (20, 10, -120)));
    }
}
