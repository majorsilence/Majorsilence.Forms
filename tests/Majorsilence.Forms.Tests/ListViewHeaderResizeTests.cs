using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6: the ListView header divider drag -- ColumnWidthChanging on every notification (cancellable),
/// ColumnWidthChanged when a width takes -- and the programmatic width change behind it.
/// </summary>
public class ListViewHeaderResizeTests
{
    private static ListView List ()
    {
        HeadlessRenderer.Use ();
        var list = new ListView { Size = new Size (400, 200), View = View.Details };
        list.Columns.Add ("A", 100);
        list.Columns.Add ("B", 100);
        list.Items.Add ("x");
        return list;
    }

    // The divider is at the right edge of column 0, in the header band; both in logical units, which
    // is what mouse events carry.
    private static Point Divider (ListView list, int column)
    {
        var edge = list.ItemArea.Left + list.ScaledCheckWidth;
        for (var i = 0; i <= column; i++)
            edge += list.ScaledColumnWidth (list.Columns[i]);
        return new Point (list.DeviceToLogicalUnits (edge), list.DeviceToLogicalUnits (list.ScaledHeaderHeight) / 2);
    }

    private static MouseEventArgs Left (Point p) => new (MouseButtons.Left, 1, p.X, p.Y, 0);

    [Fact]
    public void A_programmatic_width_change_raises_ColumnWidthChanged_once ()
    {
        using var list = List ();
        var changed = new List<int> ();
        list.ColumnWidthChanged += (_, e) => changed.Add (e.ColumnIndex);

        list.Columns[1].Width = 140;
        list.Columns[1].Width = 140;

        Assert.Equal (new[] { 1 }, changed);
        Assert.Equal (140, list.Columns[1].Width);
    }

    [Fact]
    public void Dragging_a_divider_asks_ColumnWidthChanging_and_resizes_the_column ()
    {
        using var list = List ();
        using var laid_out = PaintSurface.Render (list);
        var changing = new List<(int, int)> (); var changed = 0;
        list.ColumnWidthChanging += (_, e) => changing.Add ((e.ColumnIndex, e.NewWidth));
        list.ColumnWidthChanged += (_, _) => changed++;
        var at = Divider (list, 0);

        list.RaiseMouseDown (Left (at));
        list.RaiseMouseMove (Left (new Point (at.X + 30, at.Y)));
        list.RaiseMouseUp (Left (new Point (at.X + 30, at.Y)));

        Assert.Equal (new[] { (0, 130) }, changing);
        Assert.Equal (130, list.Columns[0].Width);
        Assert.Equal (1, changed);
    }

    [Fact]
    public void A_cancelled_ColumnWidthChanging_keeps_the_width ()
    {
        using var list = List ();
        using var laid_out = PaintSurface.Render (list);
        list.ColumnWidthChanging += (_, e) => e.Cancel = true;
        var changed = 0;
        list.ColumnWidthChanged += (_, _) => changed++;
        var at = Divider (list, 0);

        list.RaiseMouseDown (Left (at));
        list.RaiseMouseMove (Left (new Point (at.X + 30, at.Y)));
        list.RaiseMouseUp (Left (new Point (at.X + 30, at.Y)));

        Assert.Equal (100, list.Columns[0].Width);
        Assert.Equal (0, changed);
    }

    [Fact]
    public void A_handler_can_substitute_the_new_width ()
    {
        using var list = List ();
        using var laid_out = PaintSurface.Render (list);
        list.ColumnWidthChanging += (_, e) => e.NewWidth = 200;   // snap
        var at = Divider (list, 0);

        list.RaiseMouseDown (Left (at));
        list.RaiseMouseMove (Left (new Point (at.X + 5, at.Y)));

        Assert.Equal (200, list.Columns[0].Width);
    }

    [Fact]
    public void A_press_away_from_a_divider_does_not_resize ()
    {
        using var list = List ();
        using var laid_out = PaintSurface.Render (list);
        var at = Divider (list, 0);
        var middle = new Point (at.X - 50, at.Y);              // the middle of column 0's header

        list.RaiseMouseDown (Left (middle));
        list.RaiseMouseMove (Left (new Point (middle.X + 30, middle.Y)));
        list.RaiseMouseUp (Left (new Point (middle.X + 30, middle.Y)));

        Assert.Equal (100, list.Columns[0].Width);
    }

    [Fact]
    public void The_release_that_ends_a_drag_is_not_a_column_click ()
    {
        using var list = List ();
        using var laid_out = PaintSurface.Render (list);
        var clicks = 0;
        list.ColumnClick += (_, _) => clicks++;
        var at = Divider (list, 0);

        list.RaiseMouseDown (Left (at));
        list.RaiseMouseMove (Left (new Point (at.X + 30, at.Y)));
        list.RaiseMouseUp (Left (new Point (at.X + 30, at.Y)));
        list.RaiseClick (Left (new Point (at.X + 30, at.Y)));
        Assert.Equal (0, clicks);

        list.RaiseClick (Left (new Point (at.X - 50, at.Y)));   // an ordinary header click still counts
        Assert.Equal (1, clicks);
    }
}
