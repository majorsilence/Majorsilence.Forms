using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Graphics.FillPie and DrawPie draw a slice: the centre, the two radii and the arc. Built with
// SKPath.AddArc they drew the arc and its chord instead, a circle segment, so a pie chart's
// slices came out as slivers along the rim with the middle of the pie left empty.
[Collection ("Headless")]
public class PieWedgeTests
{
    // A quarter pie, 0 to 90 degrees (clockwise from three o'clock), in a 100 x 100 circle
    // centred on (50, 50). (60, 60) is inside that slice but on the centre's side of its
    // chord, the line x + y = 150 from (100, 50) to (50, 100): inked only for a wedge.
    private const int InsideWedgeNotSegmentX = 60, InsideWedgeNotSegmentY = 60;

    private static Majorsilence.Forms.Drawing.Bitmap Render (System.Action<Majorsilence.Forms.Drawing.Graphics> draw)
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        var bitmap = new Majorsilence.Forms.Drawing.Bitmap (101, 101);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        graphics.Clear (Color.White);
        draw (graphics);
        return bitmap;
    }

    private static bool Inked (Majorsilence.Forms.Drawing.Bitmap bitmap, int x, int y) =>
        bitmap.GetPixel (x, y).R < 128;

    [Fact]
    public void FillPie_fills_the_slice_out_to_the_centre ()
    {
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Black);
        using var bitmap = Render (g => g.FillPie (brush, new Rectangle (0, 0, 100, 100), 0f, 90f));

        Assert.True (Inked (bitmap, InsideWedgeNotSegmentX, InsideWedgeNotSegmentY), "the slice's inner part is filled");
        Assert.True (Inked (bitmap, 52, 52), "the slice reaches the centre");
        Assert.False (Inked (bitmap, 40, 40), "the opposite quarter is not filled");
    }

    [Fact]
    public void FillPie_with_float_coordinates_fills_the_slice_out_to_the_centre ()
    {
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Black);
        using var bitmap = Render (g => g.FillPie (brush, 0f, 0f, 100f, 100f, 0f, 90f));

        Assert.True (Inked (bitmap, InsideWedgeNotSegmentX, InsideWedgeNotSegmentY));
        Assert.False (Inked (bitmap, 40, 40));
    }

    [Fact]
    public void DrawPie_outlines_both_radii ()
    {
        using var pen = new Majorsilence.Forms.Drawing.Pen (Color.Black, 2f);
        using var bitmap = Render (g => g.DrawPie (pen, new Rectangle (0, 0, 100, 100), 0f, 90f));

        // The radius to three o'clock runs along y = 50 and the one to six o'clock along
        // x = 50; a segment's outline has neither, only the chord.
        Assert.True (Inked (bitmap, 75, 50), "the radius to the arc's start");
        Assert.True (Inked (bitmap, 50, 75), "the radius to the arc's end");
        Assert.False (Inked (bitmap, InsideWedgeNotSegmentX, InsideWedgeNotSegmentY), "the outline does not fill");
    }
}
